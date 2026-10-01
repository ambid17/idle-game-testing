using System.Collections.Generic;
using Events;
using Persistence;
using UnityEngine;

namespace Processing
{
    public enum GoodsMarketEventKind
    {
        None,
        Boom,
        Crash
    }

    // Stock-market style pricing for Processing Center goods: every good carries a price
    // multiplier on its recipe's SaleValue that drifts over time and is sold at whatever it is
    // right now (Depot.SellGood). The multiplier is a mean-reverting random walk in log space, so
    // it wanders around 1x (the preset SaleValue) without sticking at either bound, and now and
    // then a good hits a boom or crash that drags it towards an extreme for a minute or two.
    // Player sales never move the price.
    //
    // The market is world state rather than run state: prestige leaves it running, and time spent
    // with the game closed advances it (RestoreFromSaveData).
    public class GoodsMarket : Singleton<GoodsMarket>
    {
        public const float MinMultiplier = 0.25f;
        public const float MaxMultiplier = 3f;

        // Samples kept per good for the Exchange tab's graph - HistoryLength * tickSeconds of
        // history (6 minutes at the defaults).
        public const int HistoryLength = 90;

        [Header("Drift")]
        [Tooltip("Seconds between price updates.")]
        [SerializeField] private float tickSeconds = 4f;
        [Tooltip("Fraction of the gap back to the base price closed each tick.")]
        [SerializeField] private float reversion = 0.05f;
        [Tooltip("Size of the random step each tick, in log-price (0.06 is roughly +/-6%).")]
        [SerializeField] private float volatility = 0.06f;

        [Header("Booms and crashes")]
        [Tooltip("Chance per good per tick of a boom or crash starting.")]
        [SerializeField] private float eventChancePerTick = 0.01f;
        [Tooltip("Fraction of the gap to the event's target price closed each tick.")]
        [SerializeField] private float eventReversion = 0.15f;
        [SerializeField] private Vector2 eventDurationSeconds = new(60f, 150f);
        [SerializeField] private Vector2 boomMultiplier = new(1.8f, MaxMultiplier);
        [SerializeField] private Vector2 crashMultiplier = new(MinMultiplier, 0.5f);

        private static readonly float MinLogPrice = Mathf.Log(MinMultiplier);
        private static readonly float MaxLogPrice = Mathf.Log(MaxMultiplier);

        private class GoodState
        {
            public float LogPrice;
            public float EventLogTarget;
            public float EventSecondsRemaining;
            public readonly List<float> History = new();
        }

        private readonly Dictionary<ProcessingRecipeId, GoodState> states = new();
        private float tickTimer;

        public float Multiplier(ProcessingRecipeId id) => Mathf.Exp(GetState(id).LogPrice);

        // Oldest-to-newest price multipliers, one per tick; the last entry is the current price.
        public IReadOnlyList<float> History(ProcessingRecipeId id) => GetState(id).History;

        public GoodsMarketEventKind ActiveEvent(ProcessingRecipeId id)
        {
            var state = GetState(id);
            if (state.EventSecondsRemaining <= 0f) return GoodsMarketEventKind.None;
            return state.EventLogTarget > 0f ? GoodsMarketEventKind.Boom : GoodsMarketEventKind.Crash;
        }

        private void Update()
        {
            tickTimer += Time.deltaTime;
            if (tickTimer < tickSeconds) return;
            tickTimer -= tickSeconds;

            foreach (var recipe in GameManager.ProcessingRecipeDatabase.Recipes)
            {
                Tick(GetState(recipe.Id));
            }
            GameManager.EventService.Dispatch<GoodsMarketTickedEvent>();
        }

        // Created on first use, with a full window of already-simulated history so the graph
        // never opens on a flat line.
        private GoodState GetState(ProcessingRecipeId id)
        {
            if (states.TryGetValue(id, out var state)) return state;

            state = new GoodState();
            states[id] = state;
            for (int i = 0; i < HistoryLength; i++) Tick(state);
            return state;
        }

        private void Tick(GoodState state)
        {
            bool inEvent = state.EventSecondsRemaining > 0f;
            float target = inEvent ? state.EventLogTarget : 0f;
            float pull = inEvent ? eventReversion : reversion;

            state.LogPrice += pull * (target - state.LogPrice) + volatility * NextGaussian();
            state.LogPrice = Mathf.Clamp(state.LogPrice, MinLogPrice, MaxLogPrice);

            if (inEvent) state.EventSecondsRemaining -= tickSeconds;
            else if (Random.value < eventChancePerTick) StartEvent(state);

            state.History.Add(Mathf.Exp(state.LogPrice));
            if (state.History.Count > HistoryLength) state.History.RemoveAt(0);
        }

        private void StartEvent(GoodState state)
        {
            var range = Random.value < 0.5f ? boomMultiplier : crashMultiplier;
            state.EventLogTarget = Mathf.Log(Random.Range(range.x, range.y));
            state.EventSecondsRemaining = Random.Range(eventDurationSeconds.x, eventDurationSeconds.y);
        }

        // Box-Muller: a standard normal sample from two uniform ones.
        private static float NextGaussian()
        {
            float u1 = Mathf.Max(Random.value, 1e-6f);
            float u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        public List<GoodsMarketSaveEntry> ToSaveData()
        {
            var entries = new List<GoodsMarketSaveEntry>();
            foreach (var kvp in states)
            {
                entries.Add(new GoodsMarketSaveEntry
                {
                    Id = kvp.Key,
                    History = new List<float>(kvp.Value.History),
                    EventMultiplierTarget = Mathf.Exp(kvp.Value.EventLogTarget),
                    EventSecondsRemaining = kvp.Value.EventSecondsRemaining
                });
            }
            return entries;
        }

        // Restore for SaveService. The market kept trading while the game was closed, so every
        // good is advanced by elapsedSeconds (real time since the last save) - capped at one full
        // history window, since anything older has scrolled off the graph anyway.
        public void RestoreFromSaveData(IReadOnlyList<GoodsMarketSaveEntry> entries, float elapsedSeconds)
        {
            states.Clear();
            if (entries == null) return;

            int offlineTicks = Mathf.Min(HistoryLength, Mathf.FloorToInt(elapsedSeconds / tickSeconds));
            foreach (var entry in entries)
            {
                if (entry.History == null || entry.History.Count == 0) continue;

                var state = new GoodState
                {
                    EventLogTarget = Mathf.Log(Mathf.Clamp(entry.EventMultiplierTarget, MinMultiplier, MaxMultiplier)),
                    EventSecondsRemaining = entry.EventSecondsRemaining
                };
                foreach (float multiplier in entry.History)
                {
                    state.History.Add(Mathf.Clamp(multiplier, MinMultiplier, MaxMultiplier));
                }
                state.LogPrice = Mathf.Log(state.History[state.History.Count - 1]);
                states[entry.Id] = state;

                for (int i = 0; i < offlineTicks; i++) Tick(state);
            }
        }
    }
}
