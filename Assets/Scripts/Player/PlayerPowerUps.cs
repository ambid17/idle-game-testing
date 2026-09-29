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
    // Economy_PowerUpEffectivenessBonus. Timed buffs aren't saved - a reload drops them.
    // Lasting buffs (Drill Overdrive, Lucky Strike) are exposed via GetActiveBuffs for the HUD's
    // UI.PowerUpBuffBarUI.
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
        private float overdriveDuration;
        private Sprite overdriveIcon;
        private int remainingLuckyStrikeCharges;
        // Charge count right after the most recent pickup - the denominator for the buff bar's
        // remaining fraction, since pickups stack onto whatever charges are left.
        private int luckyStrikeChargesAtPickup;
        private Sprite luckyStrikeIcon;

        private float Effectiveness => 1f + PrestigeUpgradeManager.Instance.Economy_PowerUpEffectivenessBonus;

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

        // Fills results (cleared first) with every currently active lasting buff, in a stable order.
        public void GetActiveBuffs(List<PowerUpBuffStatus> results)
        {
            results.Clear();
            if (IsOverdriveActive)
            {
                float remaining = overdriveEndTime - Time.time;
                results.Add(new PowerUpBuffStatus(overdriveIcon, remaining / overdriveDuration, Mathf.CeilToInt(remaining), PowerUpBuffUnit.Seconds));
            }
            if (remainingLuckyStrikeCharges > 0)
            {
                results.Add(new PowerUpBuffStatus(luckyStrikeIcon, (float)remainingLuckyStrikeCharges / luckyStrikeChargesAtPickup, remainingLuckyStrikeCharges, PowerUpBuffUnit.Charges));
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

            string message = $"Treasure Chest: +{addedToInventory} ores from the next layer down";
            if (overflow.Count > 0)
            {
                GameManager.EventService.Dispatch(new ChestSpawnRequestedEvent(mapGenerationService.CellToWorldCenter(layerIndex, x, y), overflow));
                message += $" ({count - addedToInventory} more spilled into a chest)";
            }
            Notify(message, chestBlock);
        }

        // Only real Ore-category entries count as treasure.
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
            overdriveDuration = duration;
            overdriveEndTime = Time.time + duration;
            overdriveIcon = block.Icon;
            Notify($"Drill Overdrive: you mine {overdriveSpeedMultiplier:0.#}x faster for {duration:0}s", block);
        }

        private void ApplyFuelCanister(BlockType block)
        {
            float amount = playerController.FuelMax * fuelCanisterFraction * Effectiveness;
            playerController.AddFuel(amount);
            Notify($"Fuel Canister: restored {amount:0} fuel", block);
        }

        private void ApplyRepairKit(BlockType block)
        {
            float amount = playerHealth.MaxHp * repairKitFraction * Effectiveness;
            playerHealth.AddHp(amount);
            Notify($"Repair Kit: restored {amount:0} HP", block);
        }

        private void ApplyLuckyStrike(BlockType block)
        {
            int charges = Mathf.Max(1, Mathf.RoundToInt(luckyStrikeCharges * Effectiveness));
            remainingLuckyStrikeCharges += charges;
            luckyStrikeChargesAtPickup = remainingLuckyStrikeCharges;
            luckyStrikeIcon = block.Icon;
            Notify($"Lucky Strike: the next {remainingLuckyStrikeCharges} ores you mine are doubled", block);
        }

        // Queued, not TimeSensitive - a pickup is informational, it never needs to cut in front of
        // warnings like low fuel.
        private static void Notify(string message, BlockType block) =>
            GameManager.EventService.Dispatch(new NotificationEvent(message, NotificationUrgency.Queued, block.Icon));
    }

    public enum PowerUpBuffUnit { Seconds, Charges }

    // Snapshot of one active lasting power-up buff for the HUD. RemainingFraction is 1 when fresh
    // and falls toward 0; Remaining is whole seconds or charges, per Unit.
    public readonly struct PowerUpBuffStatus
    {
        public readonly Sprite Icon;
        public readonly float RemainingFraction;
        public readonly int Remaining;
        public readonly PowerUpBuffUnit Unit;

        public PowerUpBuffStatus(Sprite icon, float remainingFraction, int remaining, PowerUpBuffUnit unit)
        {
            Icon = icon;
            RemainingFraction = remainingFraction;
            Remaining = remaining;
            Unit = unit;
        }
    }
}
