using System.Collections.Generic;
using System.Linq;
using MapGeneration;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "idle": tracks a trailing rolling average of ore gained per minute, per
    // mineral type, from Mining Automaton deposits only (not the player, not Storage/Fuel Drones).
    // Persistence.SaveService reads AveragePerMinute to save, and multiplies it by minutes-away to
    // compute the offline-earnings screen on load.
    public class IdleEarningsTracker : Singleton<IdleEarningsTracker>
    {
        private const float WindowMinutes = 10f;

        private readonly Dictionary<BlockTypeId, Queue<(float time, int amount)>> recordsByOre = new();
        private float trackerStartTime;

        protected override void Initialize()
        {
            base.Initialize();
            trackerStartTime = Time.time;
        }

        public void RecordOreMined(BlockTypeId id, int amount)
        {
            if (amount <= 0) return;

            if (!recordsByOre.TryGetValue(id, out var queue))
            {
                queue = new Queue<(float, int)>();
                recordsByOre[id] = queue;
            }

            queue.Enqueue((Time.time, amount));
            Prune(queue);
        }

        // Averages over min(WindowMinutes, time-since-tracker-started) so a fresh session doesn't
        // divide by the full 10-minute window before that much time has actually elapsed.
        public IReadOnlyDictionary<BlockTypeId, float> AveragePerMinute
        {
            get
            {
                var result = new Dictionary<BlockTypeId, float>();
                float elapsedMinutes = Mathf.Min(WindowMinutes, (Time.time - trackerStartTime) / 60f);
                if (elapsedMinutes <= 0f) return result;

                foreach (var kvp in recordsByOre)
                {
                    Prune(kvp.Value);
                    int total = kvp.Value.Sum(r => r.amount);
                    if (total > 0) result[kvp.Key] = total / elapsedMinutes;
                }

                return result;
            }
        }

        private void Prune(Queue<(float time, int amount)> queue)
        {
            float cutoff = Time.time - WindowMinutes * 60f;
            while (queue.Count > 0 && queue.Peek().time < cutoff)
            {
                queue.Dequeue();
            }
        }

        // Seeds a freshly started session's buffer with the saved average so restored data takes
        // effect immediately, rather than needing a full window of new play to catch up.
        public void RestoreFromSaveData(IReadOnlyDictionary<BlockTypeId, float> averages)
        {
            if (averages == null) return;

            foreach (var kvp in averages)
            {
                if (kvp.Value <= 0f) continue;
                int seedAmount = Mathf.Max(1, Mathf.RoundToInt(kvp.Value * WindowMinutes));
                RecordOreMined(kvp.Key, seedAmount);
            }
        }
    }
}
