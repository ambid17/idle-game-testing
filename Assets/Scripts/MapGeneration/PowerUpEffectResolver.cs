using System.Collections.Generic;
using Economy;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // GameDesignDoc "Randomness blocks > positive": resolves the two power-up block effects.
    // Minimal version - see the implementation plan for the SightPotion simplification (a one-time
    // large reveal burst rather than a genuinely temporary one, since MineWorld.RevealFog has no
    // "hide again" path). Singleton so PlayerMining/MapGenerationService don't need a scene
    // reference to it, matching HazardDamageHandler's event-only coupling.
    public class PowerUpEffectResolver : Singleton<PowerUpEffectResolver>
    {
        [SerializeField] private int treasureChestBaseOreCount = 5;
        [SerializeField] private int sightPotionBaseRevealRadius = 15;

        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        private void OnEnable() => GameManager.EventService.Add<PowerUpTriggeredEvent>(OnPowerUpTriggered);
        private void OnDisable() => GameManager.EventService.Remove<PowerUpTriggeredEvent>(OnPowerUpTriggered);

        private void OnPowerUpTriggered(PowerUpTriggeredEvent evt)
        {
            switch (evt.Behavior)
            {
                case CustomBehavior.TreasureChest: ResolveTreasureChest(evt); break;
                case CustomBehavior.SightPotion: ResolveSightPotion(evt); break;
            }
        }

        // "contains a treasure trove of materials in the next layer" - deposits a weighted-picked
        // batch from the next layer's OreTable directly into the Depot.
        private void ResolveTreasureChest(PowerUpTriggeredEvent evt)
        {
            var layerConfigProvider = GameManager.LayerConfigProvider;
            var nextLayerConfig = layerConfigProvider != null ? layerConfigProvider.GetConfig(evt.LayerIndex + 1) : null;
            if (nextLayerConfig == null || nextLayerConfig.OreTable.Count == 0) return;

            float effectiveness = PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.Progression_PowerUpEffectivenessBonus : 0f;
            int count = Mathf.Max(1, Mathf.RoundToInt(treasureChestBaseOreCount * (1f + effectiveness)));

            var rng = new System.Random();
            var reward = new Dictionary<BlockTypeId, int>();
            for (int i = 0; i < count; i++)
            {
                var entry = PickRandomOreEntry(nextLayerConfig, rng);
                if (entry?.BlockType == null) continue;
                reward.TryGetValue(entry.BlockType.Id, out var current);
                reward[entry.BlockType.Id] = current + 1;
            }

            if (reward.Count == 0 || Depot.Instance == null) return;
            Depot.Instance.Deposit(reward);
            GameManager.EventService.Dispatch(new NotificationEvent("Treasure chest found!", NotificationUrgency.TimeSensitive));
        }

        private static WeightedBlockEntry PickRandomOreEntry(LayerConfig config, System.Random rng)
        {
            float total = 0f;
            foreach (var entry in config.OreTable) total += entry.Weight;
            if (total <= 0f) return null;

            float target = (float)rng.NextDouble() * total;
            float cumulative = 0f;
            foreach (var entry in config.OreTable)
            {
                cumulative += entry.Weight;
                if (target <= cumulative) return entry;
            }
            return config.OreTable[^1];
        }

        // Simplification (see plan): MineWorld.RevealFog only reveals permanently, so this is a
        // one-time large reveal burst rather than a genuinely temporary one.
        private void ResolveSightPotion(PowerUpTriggeredEvent evt)
        {
            if (mapGenerationService == null) return;

            float effectiveness = PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.Progression_PowerUpEffectivenessBonus : 0f;
            int radius = Mathf.Max(1, Mathf.RoundToInt(sightPotionBaseRevealRadius * (1f + effectiveness)));
            mapGenerationService.RevealAround(evt.LayerIndex, evt.X, evt.Y, radius);

            GameManager.EventService.Dispatch(new NotificationEvent("Sight potion activated!", NotificationUrgency.TimeSensitive));
        }
    }
}
