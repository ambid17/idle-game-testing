using System.Collections.Generic;
using System.Linq;
using Economy;
using MapGeneration;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "idle": tracks a trailing rolling average of ore gained per minute, per
    // mineral type, fed by every Depot-bound automation deposit (Mining Automatons and Storage
    // Drones, via AutomationDepositService) - not the player, and not Fuel Drones, which never
    // carry ore. Persistence.SaveService reads AveragePerMinute to save, and multiplies it by
    // minutes-away to compute the offline-earnings screen on load. Also doubles as the live data
    // source for the Control Center's Miner Dashboard ore/min table (MinerDashboardUI).
    public class IdleEarningsTracker : Singleton<IdleEarningsTracker>
    {
        private const float WindowMinutes = 2f;

        private readonly Dictionary<BlockTypeId, Queue<(float time, int amount)>> recordsByOre = new();
        private float trackerStartTime;

        protected override void Initialize()
        {
            base.Initialize();
            trackerStartTime = Time.time;
        }

        public void RecordOreDeposited(BlockTypeId id, int amount)
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
                    // If we only have the record from save, just keep that value otherwise the average will get multiplied
                    if (kvp.Value.Count == 1 && kvp.Value.All(record => record.time == 0))
                    {
                        result[kvp.Key] = kvp.Value.Sum(r => r.amount) / WindowMinutes;
                        continue;
                    } 
                    Prune(kvp.Value);
                    int total = kvp.Value.Sum(r => r.amount);
                    if (total > 0) result[kvp.Key] = total / elapsedMinutes;
                }

                return result;
            }
        }

        // Offline earnings curve: the ore/min average is kept at OfflineBaseRetention (plus the
        // Market value upgrade) for OfflineFullRateMinutes (plus the Market duration upgrade), then
        // falls linearly to 0 over OfflineFalloffMinutes (times the Museum fall-off perk).
        private const float OfflineBaseRetention = 0.5f;
        private const float OfflineFullRateMinutes = 10f;
        private const float OfflineFalloffMinutes = 30f;

        // Integrates the offline curve over minutesAway: returns the equivalent number of
        // full-rate (100%) minutes, so ore gained = average/min * this.
        public static float ComputeOfflineEffectiveMinutes(float minutesAway)
        {
            if (minutesAway <= 0f) return 0f;

            float retention = Mathf.Min(1f, OfflineBaseRetention + UpgradeManager.Instance.Automation_OfflineEarningsValueBonus);
            float fullRateMinutes = OfflineFullRateMinutes + UpgradeManager.Instance.Automation_OfflineEarningsDurationBonusMinutes;
            float falloffMinutes = OfflineFalloffMinutes * PrestigeUpgradeManager.Instance.Idle_OfflineFalloffDurationMultiplier;

            float fullRatePortion = Mathf.Min(minutesAway, fullRateMinutes);
            float falloffElapsed = Mathf.Clamp(minutesAway - fullRateMinutes, 0f, falloffMinutes);
            // Area under the linear 1 -> 0 ramp from 0 to falloffElapsed.
            float falloffPortion = falloffElapsed - falloffElapsed * falloffElapsed / (2f * falloffMinutes);

            return retention * (fullRatePortion + falloffPortion);
        }

        // Ore gained per type for minutesAway offline, given per-minute averages (the saved ones on
        // load, or the live AveragePerMinute for the Dev Panel's simulate button).
        public static Dictionary<BlockTypeId, int> ComputeOfflineOre(IReadOnlyDictionary<BlockTypeId, float> averages, float minutesAway)
        {
            var oreGained = new Dictionary<BlockTypeId, int>();
            float effectiveMinutes = ComputeOfflineEffectiveMinutes(minutesAway);
            if (effectiveMinutes <= 0f) return oreGained;

            foreach (var kvp in averages)
            {
                int amount = Mathf.RoundToInt(kvp.Value * effectiveMinutes);
                if (amount > 0) oreGained[kvp.Key] = amount;
            }
            return oreGained;
        }

        private void Prune(Queue<(float time, int amount)> queue)
        {
            float cutoff = Time.time - (WindowMinutes * 60f);
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
                RecordOreDeposited(kvp.Key, seedAmount);
            }
        }
    }
}
