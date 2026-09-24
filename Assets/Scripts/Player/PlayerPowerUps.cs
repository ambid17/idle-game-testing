using System.Collections.Generic;
using Economy;
using Events;
using MapGeneration;
using UnityEngine;

namespace Player
{
    // GameDesignDoc "Randomness blocks > positive": resolves every PowerUp-category block the
    // player mines. PowerUps are player-only (MineWorld.TryMineCell refuses them for automatons
    // and explosions), so PlayerMining applies them directly here instead of routing through
    // CustomBlockTriggeredEvent like hazards do. Every magnitude scales with the Museum's
    // Progression_PowerUpEffectivenessBonus. Timed buffs aren't saved - a reload drops them.
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerPowerUps : MonoBehaviour
    {
        [Header("Treasure Chest")]
        [Tooltip("Ores rolled from the next layer's OreTable. Whatever doesn't fit in inventory spills into a lootable Chest.")]
        [SerializeField] private int treasureChestOreCount = 12;

        [Header("Drill Overdrive")]
        [SerializeField] private float overdriveSpeedMultiplier = 3f;
        [SerializeField] private float overdriveDurationSeconds = 15f;

        [Header("Fuel Canister")]
        [Tooltip("Fraction of max fuel restored.")]
        [SerializeField] private float fuelCanisterFraction = 1f;

        [Header("Repair Kit")]
        [Tooltip("Fraction of max HP restored.")]
        [SerializeField] private float repairKitFraction = 0.4f;

        [Header("Lucky Strike")]
        [Tooltip("Number of subsequent ores that are doubled.")]
        [SerializeField] private int luckyStrikeCharges = 10;

        private PlayerInventory playerInventory;
        private PlayerHealth playerHealth;
        private PlayerController playerController;
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        private float overdriveEndTime;
        private int remainingLuckyStrikeCharges;

        private float Effectiveness => 1f + PrestigeUpgradeManager.Instance.Progression_PowerUpEffectivenessBonus;

        public bool IsOverdriveActive => Time.time < overdriveEndTime;
        public float MiningSpeedMultiplier => IsOverdriveActive ? overdriveSpeedMultiplier : 1f;

        private void Awake()
        {
            playerInventory = GetComponent<PlayerInventory>();
            if (playerInventory == null) Debug.LogError($"{nameof(PlayerPowerUps)} on {name} requires a PlayerInventory component.");

            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null) Debug.LogError($"{nameof(PlayerPowerUps)} on {name} requires a PlayerHealth component.");

            playerController = GetComponent<PlayerController>();
            if (playerController == null) Debug.LogError($"{nameof(PlayerPowerUps)} on {name} requires a PlayerController component.");
        }

        public void Apply(BlockType blockType, int layerIndex, int x, int y)
        {
            switch (blockType.CustomBehavior)
            {
                case CustomBehavior.TreasureChest: ApplyTreasureChest(blockType, layerIndex, x, y); break;
                case CustomBehavior.DrillOverdrive: ApplyDrillOverdrive(blockType); break;
                case CustomBehavior.FuelCanister: ApplyFuelCanister(blockType); break;
                case CustomBehavior.RepairKit: ApplyRepairKit(blockType); break;
                case CustomBehavior.LuckyStrike: ApplyLuckyStrike(blockType); break;
                default:
                    Debug.LogError($"{nameof(PlayerPowerUps)}: PowerUp block '{blockType.name}' has unhandled CustomBehavior {blockType.CustomBehavior}.");
                    break;
            }
        }

        // Called once per mined ore by PlayerMining: 2 while Lucky Strike charges remain (spending
        // one), otherwise 1.
        public int ConsumeLuckyStrikeMultiplier()
        {
            if (remainingLuckyStrikeCharges <= 0) return 1;
            remainingLuckyStrikeCharges--;
            return 2;
        }

        // "contains a treasure trove of materials in the next layer". LayerConfigProvider.GetConfig
        // falls back to the deepest authored layer, so the last layer's chest still rolls from
        // its own table rather than coming up empty.
        private void ApplyTreasureChest(BlockType chestBlock, int layerIndex, int x, int y)
        {
            var nextLayerConfig = GameManager.LayerConfigProvider.GetConfig(layerIndex + 1);
            if (nextLayerConfig == null || !nextLayerConfig.OreTable.Exists(IsTreasureOre))
            {
                Debug.LogError($"{nameof(PlayerPowerUps)}: no OreTable to roll Treasure Chest loot from for layer {layerIndex + 1}.");
                return;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(treasureChestOreCount * Effectiveness));
            var overflow = new Dictionary<BlockTypeId, int>();
            int addedToInventory = 0;

            for (int i = 0; i < count; i++)
            {
                var ore = PickRandomOre(nextLayerConfig);
                if (ore == null) continue;

                if (playerInventory.CurrentWeight + ore.Weight <= playerInventory.MaxWeight)
                {
                    playerInventory.AddOre(ore);
                    addedToInventory++;
                    continue;
                }

                overflow.TryGetValue(ore.Id, out var current);
                overflow[ore.Id] = current + 1;
            }

            string message = $"Treasure chest! +{addedToInventory} ores";
            if (overflow.Count > 0)
            {
                GameManager.EventService.Dispatch(new ChestSpawnRequestedEvent(mapGenerationService.CellToWorldCenter(layerIndex, x, y), overflow));
                message += " (overflow left in a chest)";
            }
            Notify(message, chestBlock);
        }

        // OreTable also carries the layer's Dirt filler entry (weight 100) - only real Ore-category
        // entries count as treasure.
        private static BlockType PickRandomOre(LayerConfig config)
        {
            float total = 0f;
            foreach (var entry in config.OreTable)
            {
                if (IsTreasureOre(entry)) total += entry.Weight;
            }
            if (total <= 0f) return null;

            float target = Random.value * total;
            float cumulative = 0f;
            BlockType last = null;
            foreach (var entry in config.OreTable)
            {
                if (!IsTreasureOre(entry)) continue;
                cumulative += entry.Weight;
                last = entry.BlockType;
                if (target <= cumulative) return entry.BlockType;
            }
            return last;
        }

        private static bool IsTreasureOre(WeightedBlockEntry entry) =>
            entry.BlockType != null && entry.BlockType.Category == BlockCategory.Ore;

        // Picking up another while one is active restarts the full duration rather than stacking.
        private void ApplyDrillOverdrive(BlockType block)
        {
            float duration = overdriveDurationSeconds * Effectiveness;
            overdriveEndTime = Time.time + duration;
            Notify($"Drill overdrive! {overdriveSpeedMultiplier:0.#}x mining speed for {duration:0}s", block);
        }

        private void ApplyFuelCanister(BlockType block)
        {
            playerController.AddFuel(playerController.FuelMax * fuelCanisterFraction * Effectiveness);
            Notify("Fuel canister! Fuel refilled", block);
        }

        private void ApplyRepairKit(BlockType block)
        {
            float amount = playerHealth.MaxHp * repairKitFraction * Effectiveness;
            playerHealth.AddHp(amount);
            Notify($"Repair kit! +{amount:0} HP", block);
        }

        private void ApplyLuckyStrike(BlockType block)
        {
            int charges = Mathf.Max(1, Mathf.RoundToInt(luckyStrikeCharges * Effectiveness));
            remainingLuckyStrikeCharges += charges;
            Notify($"Lucky strike! Next {remainingLuckyStrikeCharges} ores doubled", block);
        }

        private static void Notify(string message, BlockType block) =>
            GameManager.EventService.Dispatch(new NotificationEvent(message, NotificationUrgency.TimeSensitive, block.Icon));
    }
}
