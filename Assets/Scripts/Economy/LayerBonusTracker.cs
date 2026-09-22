using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Economy
{
    // GameDesignDoc "Passive upgrades": mining 50%/75%/95% of a layer's cells multiplies mined
    // ore's value by 2x/4x/8x for the rest of that layer (DoublePassiveLayerBonus doubles those
    // tiers further). Applied as bonus Dollars credited immediately when ore is mined (see
    // PlayerMining.CollectMinedBlock) rather than a Depot/inventory rework, since Depot has no
    // concept of which layer a stored ore came from. Reuses ChunkData.MinedCount (already tracked
    // by MineWorld.TryMineCell) as the "how much of this layer is cleared" signal instead of a
    // second parallel counter.
    public class LayerBonusTracker : Singleton<LayerBonusTracker>
    {
        private static readonly float[] TierThresholds = { 0.5f, 0.75f, 0.95f };
        private static readonly float[] TierMultipliers = { 2f, 4f, 8f };

        // "Keep the passive layer bonus between prestiges" (PrestigeUpgradeEffect.KeepPassiveLayerBonus):
        // remembers the highest tier a layer index ever reached, since the map (and every chunk's
        // live MinedCount) is wiped on every prestige.
        private readonly Dictionary<int, float> keptTierByLayer = new();
        private readonly Dictionary<int, float> lastNotifiedTierByLayer = new();

        public float CurrentTierMultiplier(int layerIndex)
        {
            float liveTier = LiveTierMultiplier(layerIndex);
            keptTierByLayer.TryGetValue(layerIndex, out float kept);
            float tier = Mathf.Max(liveTier, kept);

            if (tier > kept && PrestigeUpgradeManager.Instance != null && PrestigeUpgradeManager.Instance.KeepPassiveLayerBonusUnlocked)
            {
                keptTierByLayer[layerIndex] = tier;
            }

            NotifyIfNewTier(layerIndex, tier);
            return tier;
        }

        private void NotifyIfNewTier(int layerIndex, float tier)
        {
            lastNotifiedTierByLayer.TryGetValue(layerIndex, out float lastNotified);
            if (tier <= lastNotified) return;

            lastNotifiedTierByLayer[layerIndex] = tier;
            if (tier > 1f) GameManager.EventService.Dispatch(new NotificationEvent($"Layer bonus: {tier:0.#}x!", NotificationUrgency.TimeSensitive));
        }

        private float LiveTierMultiplier(int layerIndex)
        {
            var world = GameManager.MapGenerationService != null ? GameManager.MapGenerationService.World : null;
            var chunk = world?.GetOrGenerateChunk(layerIndex);
            if (chunk == null || chunk.Width <= 0 || chunk.Height <= 0) return 1f;

            float minedFraction = (float)chunk.MinedCount / (chunk.Width * chunk.Height);

            float tier = 1f;
            for (int i = 0; i < TierThresholds.Length; i++)
            {
                if (minedFraction >= TierThresholds[i]) tier = TierMultipliers[i];
            }

            bool doubled = PrestigeUpgradeManager.Instance != null && PrestigeUpgradeManager.Instance.DoublePassiveLayerBonusUnlocked;
            return doubled ? tier * 2f : tier;
        }

        // Called by PrestigeManager.ExecutePrestige. Mirrors UpgradeManager.ResetAllLevels's
        // "wipe unless a perk says otherwise" shape.
        public void ClearUnlessKept()
        {
            if (PrestigeUpgradeManager.Instance != null && PrestigeUpgradeManager.Instance.KeepPassiveLayerBonusUnlocked) return;
            keptTierByLayer.Clear();
            lastNotifiedTierByLayer.Clear();
        }
    }
}
