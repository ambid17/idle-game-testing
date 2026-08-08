using System.Collections.Generic;
using Events;
using MapGeneration;
using UnityEngine;

namespace Automation
{
    // Feeds MinerDashboardUI's per-automaton $/min graph. Listens for
    // OreDepositedByAutomationEvent, converts deposited ore to a dollar-equivalent via
    // BlockType.Value at deposit time (not re-priced later if Marketing upgrades change sell value
    // afterward), and buckets it per minute per automaton index. Storage-drone deposits
    // (AutomatonIndex == -1) are ignored - only automaton output is graphed.
    public class AutomatonEarningsTracker : Singleton<AutomatonEarningsTracker>
    {
        private const int MaxBuckets = 10;

        private readonly Dictionary<int, MinuteBucketSeries> seriesByAutomatonIndex = new();
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void OnEnable() => GameManager.EventService.Add<OreDepositedByAutomationEvent>(OnOreDeposited);
        private void OnDisable() => GameManager.EventService.Remove<OreDepositedByAutomationEvent>(OnOreDeposited);

        private void OnOreDeposited(OreDepositedByAutomationEvent evt)
        {
            if (evt.AutomatonIndex < 0 || evt.Deposited == null) return;

            float dollarValue = 0f;
            foreach (var kvp in evt.Deposited)
            {
                var blockType = blockTypeDatabase != null ? blockTypeDatabase.Get((byte)kvp.Key) : null;
                if (blockType != null) dollarValue += blockType.Value * kvp.Value;
            }
            if (dollarValue <= 0f) return;

            if (!seriesByAutomatonIndex.TryGetValue(evt.AutomatonIndex, out var series))
            {
                series = new MinuteBucketSeries();
                seriesByAutomatonIndex[evt.AutomatonIndex] = series;
            }
            series.Record(dollarValue);
        }

        // Oldest-to-newest one-minute $/min buckets, up to MaxBuckets, with the current
        // in-progress minute appended so the graph updates live rather than only once per minute.
        public IReadOnlyList<float> RecentDollarsPerMinute(int automatonIndex) =>
            seriesByAutomatonIndex.TryGetValue(automatonIndex, out var series) ? series.Snapshot() : System.Array.Empty<float>();

        private class MinuteBucketSeries
        {
            private readonly List<float> completedBuckets = new();
            private float currentBucketValue;
            private float currentBucketStart = -1f;

            public void Record(float value)
            {
                if (currentBucketStart < 0f) currentBucketStart = Time.time;
                RollIfStale();
                currentBucketValue += value;
            }

            public IReadOnlyList<float> Snapshot()
            {
                RollIfStale();

                var snapshot = new List<float>(completedBuckets);
                if (currentBucketStart >= 0f) snapshot.Add(currentBucketValue);
                while (snapshot.Count > MaxBuckets) snapshot.RemoveAt(0);
                return snapshot;
            }

            // Rolls over stale buckets even without a new deposit, so the graph doesn't show a
            // stuck in-progress value forever once an automaton goes quiet.
            private void RollIfStale()
            {
                while (currentBucketStart >= 0f && Time.time - currentBucketStart >= 60f)
                {
                    completedBuckets.Add(currentBucketValue);
                    if (completedBuckets.Count > MaxBuckets) completedBuckets.RemoveAt(0);
                    currentBucketValue = 0f;
                    currentBucketStart += 60f;
                }
            }
        }
    }
}
