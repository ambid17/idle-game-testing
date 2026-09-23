using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Economy
{
    // GameDesignDoc "Passive upgrades": once the Economy_PassiveLayerBonus prestige perk is owned,
    // mining 50%/75%/95% of a layer's cells multiplies mined ore's value by
    // PrestigeUpgradeManager.PassiveLayerBonusPerTier once per threshold reached (e.g. 1.5x at 50%,
    // 1.5^2x at 75%, 1.5^3x at 95%) for the rest of that layer. Applied as bonus Dollars credited
    // immediately when ore is mined (see PlayerMining.CollectMinedBlock) rather than a
    // Depot/inventory rework, since Depot has no concept of which layer a stored ore came from.
    // Reuses ChunkData.MinedCount (already tracked by MineWorld.TryMineCell) as the "how much of
    // this layer is cleared" signal instead of a second parallel counter.
    public class LayerBonusTracker : Singleton<LayerBonusTracker>
    {
        private static readonly float[] TierThresholds = { 0.5f, 0.75f, 0.95f };

        private readonly Dictionary<int, float> lastNotifiedTierByLayer = new();

        public float CurrentTierMultiplier(int layerIndex)
        {
            float tier = LiveTierMultiplier(layerIndex);
            NotifyIfNewTier(layerIndex, tier);
            return tier;
        }

        private void NotifyIfNewTier(int layerIndex, float tier)
        {
            lastNotifiedTierByLayer.TryGetValue(layerIndex, out float lastNotified);
            if (tier <= lastNotified) return;

            lastNotifiedTierByLayer[layerIndex] = tier;
            if (tier > 1f) GameManager.EventService.Dispatch(new NotificationEvent($"Layer bonus: {tier:0.##}x!", NotificationUrgency.TimeSensitive));
        }

        private float LiveTierMultiplier(int layerIndex)
        {
            float perTier = PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.PassiveLayerBonusPerTier : 1f;
            if (perTier <= 1f) return 1f;

            var world = GameManager.MapGenerationService != null ? GameManager.MapGenerationService.World : null;
            var chunk = world?.GetOrGenerateChunk(layerIndex);
            if (chunk == null || chunk.Width <= 0 || chunk.Height <= 0) return 1f;

            float minedFraction = (float)chunk.MinedCount / (chunk.Width * chunk.Height);

            int tiersReached = 0;
            foreach (float threshold in TierThresholds)
            {
                if (minedFraction >= threshold) tiersReached++;
            }

            return Mathf.Pow(perTier, tiersReached);
        }

        // Called by PrestigeManager.ExecutePrestige - the map (and every chunk's MinedCount) is
        // wiped on prestige, so the per-layer notification state has to be too.
        public void ResetForPrestige()
        {
            lastNotifiedTierByLayer.Clear();
        }
    }
}
