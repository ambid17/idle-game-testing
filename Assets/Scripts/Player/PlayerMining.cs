using System.Collections.Generic;
using Audio;
using Economy;
using Events;
using MapGeneration;
using Settings;
using Tutorial;
using UnityEngine;

namespace Player
{
    // Directional mining per GameDesignDoc "Mechanics": holding A/S/D (and W, with the DigUp
    // prestige perk) mines in that direction,
    // but only while grounded (PlayerController.IsGrounded). Resolves the targeted grid cell
    // through MapGenerationService's world<->cell helpers and mines it once BlockType.MiningTime
    // (scaled by the layer's BlockHealth and the Mining Speed upgrade) has elapsed. Per
    // "Inventory": once the carried weight is full, Ore-category blocks can no longer be mined
    // unless the Overflow upgrade is unlocked, in which case they're auto-sold instead.
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [RequireComponent(typeof(PlayerPowerUps))]
    [RequireComponent(typeof(DigFeedback))]
    public class PlayerMining : MonoBehaviour
    {
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;
        [SerializeField] private MiningCrackIndicator crackIndicator;
        [Tooltip("Seconds between pickaxe-hit sounds while working on a block.")]
        [SerializeField] private float miningHitInterval = 0.25f;
        [Tooltip("Minimum seconds between inventory-full/too-heavy notifications, so repeatedly bumping an ore (e.g. jetpacking into the ceiling with DigUp) doesn't spam the toast.")]
        [SerializeField] private float inventoryBlockNotifyCooldown = 3f;
        [SerializeField] private bool debug;

        private PlayerController playerController;
        private PlayerInventory playerInventory;
        private PlayerPowerUps playerPowerUps;
        private DigFeedback digFeedback;
        private CapsuleCollider2D capsuleCollider;
        private bool hasTarget;
        private int targetLayer, targetX, targetY;
        private float miningProgress;
        private float miningHitTimer;
        private enum InventoryBlockReason { None, Full, TooHeavy }
        private InventoryBlockReason lastInventoryBlock;
        private float nextInventoryBlockNotifyTime;
        private UpgradeManager upgradeManager => UpgradeManager.Instance;

        // True only while actually working on a mineable block - PlayerAnimation plays the drill
        // frames off this rather than off raw input, so bumping an unmineable block doesn't drill.
        public bool IsMining => hasTarget;

        private bool CanOverflow => UpgradeManager.Instance != null && UpgradeManager.Instance.Economy_OverflowUnlocked;
        

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerInventory = GetComponent<PlayerInventory>();
            capsuleCollider = GetComponent<CapsuleCollider2D>();
            playerPowerUps = GetComponent<PlayerPowerUps>();
            digFeedback = GetComponent<DigFeedback>();

            if (playerPowerUps == null) Debug.LogError($"{nameof(PlayerMining)} on {name} requires a PlayerPowerUps component.");
            if (digFeedback == null) Debug.LogError($"{nameof(PlayerMining)} on {name} requires a DigFeedback component.");
            if (crackIndicator == null) Debug.LogError($"{nameof(PlayerMining)} on {name} is missing its crackIndicator reference.");
        }

        private void Update()
        {
            streamingManager.SetFocusDepth(gameObject.name, transform.position.y);
            Vector2Int? direction = ResolveDirection();
            // GameDesignDoc "Prestige > Mining": the DigWhileFlying perk lifts the normal
            // grounded-only mining restriction. Digging up (DigUp perk) is always exempt from it -
            // holding W fires the jetpack, so the player is usually pressed against the ceiling
            // rather than grounded. Mining also burns fuel per tick (same tank as flying/idle
            // drain - see PlayerController.ConsumeMiningFuel), so an empty tank blocks it too.
            bool isDiggingUp = direction == Vector2Int.up;
            bool canMine = (playerController.IsGrounded || isDiggingUp || PrestigeUpgradeManager.Instance.Mining_DigWhileFlyingUnlocked) && playerController.HasFuel;
            if (!canMine || direction == null || InputBlocker.IsBlocked || playerController.IsInPortal)
            {
                if(debug) Debug.Log($"PlayerMining: not mining because: IsGrounded={playerController.IsGrounded}, direction={direction}, InputBlocker.IsBlocked={InputBlocker.IsBlocked}");
                lastInventoryBlock = InventoryBlockReason.None;
                ResetTarget();
                return;
            }

            if (!TryResolveTargetCell(direction.Value, out int layerIndex, out int targetCellX, out int targetCellY))
            {
                if (debug) Debug.LogWarning($"PlayerMining: failed to resolve target cell (playerPos: {transform.position.ToFormattedString()}, direction {direction.ToFormattedString()}). Resolved Cell: ({targetCellX}, {targetCellY})");
                lastInventoryBlock = InventoryBlockReason.None;
                ResetTarget();
                return;
            }

            if(debug) Debug.Log($"PlayerMining: resolved target cell at (x,y,layer): ({targetCellX},{targetCellY},{layerIndex}) (playerPos: {transform.position.ToFormattedString()}, direction {direction.ToFormattedString()})");

            bool isNewTarget = !hasTarget || layerIndex != targetLayer || targetCellX != targetX || targetCellY != targetY;
            if (isNewTarget)
            {
                targetLayer = layerIndex;
                targetX = targetCellX;
                targetY = targetCellY;
                hasTarget = true;
                miningProgress = 0f;
                // Due immediately, so the first hit lands the moment the player starts digging.
                miningHitTimer = 0f;
            }

            var blockType = mapGenerationService.GetBlockTypeAt(layerIndex, targetCellX, targetCellY);
            // Blocked when the ore wouldn't fit in the remaining capacity, not just when the bag is
            // at 100% - otherwise a heavy ore could push the player over their max weight (Chest
            // looting already refuses to overfill, so mining matches it).
            var inventoryBlock = InventoryBlockReason.None;
            if (blockType != null && blockType.Category == BlockCategory.Ore && !CanOverflow)
            {
                if (playerInventory.IsFull) inventoryBlock = InventoryBlockReason.Full;
                else if (!playerInventory.CanFit(blockType)) inventoryBlock = InventoryBlockReason.TooHeavy;
            }
            bool blockedByFullInventory = inventoryBlock != InventoryBlockReason.None;

            // Edge-triggered like PlayerController's low-fuel check: fires once when mining first
            // becomes blocked, not every frame it stays blocked, so it can't drown out other HUD
            // notifications sharing the same toast. The edge resets whenever the player stops
            // pushing into the block, so a cooldown also gates it - otherwise bobbing against the
            // ceiling while flying up re-triggers it every bump.
            if (blockedByFullInventory && inventoryBlock != lastInventoryBlock && Time.time >= nextInventoryBlockNotifyTime)
            {
                nextInventoryBlockNotifyTime = Time.time + inventoryBlockNotifyCooldown;
                string message = inventoryBlock == InventoryBlockReason.Full
                    ? "Inventory is full!"
                    : $"Not enough space for {blockType.DisplayName}! (needs {blockType.Weight:0.#} weight, {playerInventory.RemainingWeight:0.#} free)";
                GameManager.EventService.Dispatch(new NotificationEvent(message, NotificationUrgency.TimeSensitive));
                GameManager.AudioService.Play(SoundId.Warning);
                TutorialManager.Instance.TryShow(TutorialId.InventoryFull);
            }
            lastInventoryBlock = inventoryBlock;

            if (blockType == null
                || (blockType.Id == (byte)BlockTypeId.GrassyDirt)
                || (blockType.Id == BlockTypeId.FallingRock && !PrestigeUpgradeManager.Instance.Mining_CanMineRocks)
                || blockType.Unmineable
                || blockedByFullInventory
                )
            {
                if (debug) Debug.LogWarning($"PlayerMining: cannot mine target cell at (x,y,layer): ({targetCellX},{targetCellY},{layerIndex}) (blockType {(blockType == null ? "none" : blockType.name)}), inventory full {playerInventory.IsFull}, can overflow {CanOverflow})");
                ResetTarget();
                return;
            }

            miningProgress += Time.deltaTime * upgradeManager.Mining_SpeedMultiplier * playerPowerUps.MiningSpeedMultiplier;
            playerController.ConsumeMiningFuel(Time.deltaTime);
            float targetBlockHealth = blockType.Health * mapGenerationService.GetBlockHealthMultiplier(layerIndex);

            // GameDesignDoc "Insta-mine chance": rolled once per newly-acquired target.
            var canInstaMine = isNewTarget && upgradeManager != null && upgradeManager.Mining_InstaMineChance > 0f && Random.value < upgradeManager.Mining_InstaMineChance;
            // GameDesignDoc "the final upgrade makes dirt/stone an instant mine".
            var canInstaMineDirt = blockType.Category == BlockCategory.Dirt && upgradeManager != null && upgradeManager.Mining_InstantMineDirt;
            // Mining_ScrapAlloyInstaMine's capstone.
            var canInstaMineScrapAlloy = blockType.Id == BlockTypeId.ScrapAlloy && upgradeManager != null && upgradeManager.Mining_InstantMineScrapAlloy;
            var finishedMining =  miningProgress >= targetBlockHealth;
            if (canInstaMine || canInstaMineDirt || canInstaMineScrapAlloy || finishedMining)
            {
                if (debug) Debug.Log($"PlayerMining: finishing mine at (x,y,layer): ({targetCellX},{targetCellY},{layerIndex})");
                MineTarget(layerIndex, targetCellX, targetCellY, blockType, direction.Value, targetBlockHealth);
                ResetTarget();
                return;
            }

            miningHitTimer -= Time.deltaTime;
            if (miningHitTimer <= 0f)
            {
                GameManager.AudioService.Play(SoundId.MiningHit);
                digFeedback.Hit(mapGenerationService.CellToWorldCenter(layerIndex, targetCellX, targetCellY), direction.Value, blockType);
                miningHitTimer = miningHitInterval;
            }

            if (crackIndicator != null)
            {
                crackIndicator.Show(mapGenerationService.CellToWorldCenter(layerIndex, targetCellX, targetCellY), miningProgress / targetBlockHealth);
            }
        }

        // The grid cell mining in `direction` would hit. Also used by PlayerAnalyzer so a scan
        // always targets the same block the player would dig.
        public bool TryResolveTargetCell(Vector2Int direction, out int layerIndex, out int x, out int y)
        {
            // The player's pivot sits at chest height, above the row they're standing on top of,
            // so targeting always resolves against that standing row's vertical center rather
            // than the raw pivot Y - otherwise horizontal targets resolve one row too high and
            // never hit a block (only "down" ever happened to land in-bounds by coincidence).
            float cellSize = mapGenerationService.CellSize;

            // Digging up targets the cell just above the top of the collider instead.
            var bottomOfCollider = transform.position.y - (capsuleCollider.size.y / 2);
            var topOfCollider = transform.position.y + (capsuleCollider.size.y / 2);
            var digDownDepth = direction.y < 0 ? cellSize / 2 : 0;
            float targetYPos = direction == Vector2Int.up ? topOfCollider + cellSize / 2 : bottomOfCollider - digDownDepth;

            Vector3 targetWorldPos = new Vector3(transform.position.x + direction.x * cellSize, targetYPos, 0f);
            return mapGenerationService.TryWorldToCellInBounds(targetWorldPos, out layerIndex, out x, out y);
        }

        private static Vector2Int? ResolveDirection()
        {
            var keybinds = GameManager.KeybindService;
            if (keybinds.IsPressed(GameAction.MoveLeft)) return Vector2Int.left;
            if (keybinds.IsPressed(GameAction.MoveRight)) return Vector2Int.right;
            if (keybinds.IsPressed(GameAction.MoveDown)) return Vector2Int.down;
            // FlyUp also fires the jetpack (PlayerController), so digging up only happens while
            // the player is holding it against a block overhead.
            if (keybinds.IsPressed(GameAction.FlyUp) && PrestigeUpgradeManager.Instance.Mining_DigUpUnlocked) return Vector2Int.up;
            return null;
        }

        private void ResetTarget()
        {
            hasTarget = false;
            miningProgress = 0f;
            if (crackIndicator != null) crackIndicator.Hide();
        }

        private void MineTarget(int layerIndex, int x, int y, BlockType blockType, Vector2Int direction, float effectiveHealth)
        {
            if (!mapGenerationService.MineCell(layerIndex, x, y, minedByPlayer: true, canMineFallingRock: PrestigeUpgradeManager.Instance.Mining_CanMineRocks)) return;

            digFeedback.Break(mapGenerationService.CellToWorldCenter(layerIndex, x, y), direction, blockType, effectiveHealth, primary: true);
            CollectMinedBlock(blockType, layerIndex, x, y);
            MineAreaBonusCells(layerIndex, x, y, blockType, direction);
            MineExcavatorCells(layerIndex, x, y, direction);
        }

        // Excavator upgrade: after any successful dig, keeps breaking Dirt blocks further along
        // the dig direction, one per level, for free. Stops at the first cell that isn't mineable
        // Dirt (air, ore, rock, etc.) so it never tunnels through gaps or grabs anything valuable.
        // Steps in world space so a dig down can cross a layer boundary.
        private void MineExcavatorCells(int layerIndex, int x, int y, Vector2Int direction)
        {
            int depth = upgradeManager.Mining_ExcavatorDepth;
            if (depth <= 0) return;

            Vector3 step = new Vector3(direction.x, direction.y, 0f) * mapGenerationService.CellSize;
            Vector3 worldPos = mapGenerationService.CellToWorldCenter(layerIndex, x, y);
            for (int i = 0; i < depth; i++)
            {
                worldPos += step;
                if (!mapGenerationService.TryWorldToCellInBounds(worldPos, out int cellLayer, out int cellX, out int cellY)) return;

                var block = mapGenerationService.GetBlockTypeAt(cellLayer, cellX, cellY);
                if (block == null
                    || block.Category != BlockCategory.Dirt
                    || block.Id == BlockTypeId.GrassyDirt
                    || block.Unmineable) return;

                if (!mapGenerationService.MineCell(cellLayer, cellX, cellY, minedByPlayer: true)) return;

                float health = block.Health * mapGenerationService.GetBlockHealthMultiplier(cellLayer);
                digFeedback.Break(mapGenerationService.CellToWorldCenter(cellLayer, cellX, cellY), direction, block, health, primary: false);
                CollectMinedBlock(block, cellLayer, cellX, cellY);
            }
        }

        private void CollectMinedBlock(BlockType blockType, int layerIndex, int x, int y)
        {
            GameManager.EventService.Dispatch(new BlockMinedEvent(blockType, layerIndex));

            if (blockType.Category == BlockCategory.PowerUp)
            {
                playerPowerUps.Apply(blockType, layerIndex, x, y);
                return;
            }
            if (blockType.Category == BlockCategory.Artifact)
            {
                GameManager.EventService.Dispatch(new NotificationEvent($"+1 <color=purple>Artifact</color>", NotificationUrgency.Queued, blockType.Icon, blockType.IconBackground));
                Wallet.Instance.AddArtifact();
                digFeedback.Pickup(mapGenerationService.CellToWorldCenter(layerIndex, x, y), blockType, 1);
                return;
            }
            if (blockType.Category != BlockCategory.Ore) return;

            // Lucky Strike power-up (see PlayerPowerUps): 2 while charges remain, else 1.
            // Run modifier (Dark Layer) multiplies on top.
            int amount = playerPowerUps.ConsumeLuckyStrikeMultiplier() * GameManager.RunModifierService.OreYieldMultiplier(layerIndex);
            GameManager.EventService.Dispatch(new NotificationEvent($"+{amount} {blockType.DisplayName}", NotificationUrgency.Queued, blockType.Icon, blockType.IconBackground));
            digFeedback.Pickup(mapGenerationService.CellToWorldCenter(layerIndex, x, y), blockType, amount);

            for (int i = 0; i < amount; i++) ApplyLayerBonus(blockType, layerIndex);

            // Only what fits goes in the bag, so a multi-unit yield (Lucky Strike, Dark Layer) can't
            // overfill it. The rest is auto-sold with Overflow, else spilled into a chest at the
            // mined cell (same as the Treasure Chest power-up's overflow).
            int toInventory = playerInventory.MaxAmountThatFits(blockType, amount);
            int excess = amount - toInventory;
            if (toInventory > 0) playerInventory.AddOre(blockType, toInventory);
            if (excess <= 0) return;

            if (CanOverflow)
            {
                var upgrades = UpgradeManager.Instance;
                double value = blockType.Value * excess * upgrades.Economy_OverflowSellFraction * upgrades.Economy_SellValueMultiplier * GameManager.RunModifierService.SellValueMultiplier(blockType.Id);
                if (value > 0 && Wallet.Instance != null) Wallet.Instance.Add(value);
            }
            else
            {
                var spilled = new Dictionary<BlockTypeId, int> { { blockType.Id, excess } };
                GameManager.EventService.Dispatch(new ChestSpawnRequestedEvent(mapGenerationService.CellToWorldCenter(layerIndex, x, y), spilled));
            }
        }

        // GameDesignDoc "Passive upgrades": credits the bonus portion of the layer's value
        // multiplier immediately as Dollars - the base ore value still flows through the normal
        // carry-to-Depot-then-sell path untouched (see Economy.LayerBonusTracker).
        private void ApplyLayerBonus(BlockType blockType, int layerIndex)
        {
            var tracker = LayerBonusTracker.Instance;
            if (tracker == null) return;

            float tierMultiplier = tracker.CurrentTierMultiplier(layerIndex);
            if (tierMultiplier <= 1f) return;

            double bonus = blockType.Value * (tierMultiplier - 1f);
            if (bonus > 0 && Wallet.Instance != null) Wallet.Instance.Add(bonus);
        }

        // GameDesignDoc "Market Upgrades > Mining > Increase mining size" (vein mining): only
        // triggers off mining an Ore block, then chains into adjacent Ore blocks for free (no
        // extra time cost - the upgrade IS the free hit), up to MiningAreaLevel of them.
        private void MineAreaBonusCells(int layerIndex, int centerX, int centerY, BlockType primaryBlockType, Vector2Int direction)
        {
            if (primaryBlockType.Category != BlockCategory.Ore) return;

            var upgrades = UpgradeManager.Instance;
            if (upgrades == null || upgrades.Mining_AreaLevel <= 0) return;

            foreach (var cell in VeinMiningPattern.GetChainCells(mapGenerationService, layerIndex, centerX, centerY, upgrades.Mining_AreaLevel))
            {
                var bonusBlock = mapGenerationService.GetBlockTypeAt(layerIndex, cell.x, cell.y);
                if (bonusBlock == null) continue;
                if (bonusBlock.Category == BlockCategory.Ore && !playerInventory.CanFit(bonusBlock) && !CanOverflow) continue;

                if (!mapGenerationService.MineCell(layerIndex, cell.x, cell.y, minedByPlayer: true)) continue;

                float bonusHealth = bonusBlock.Health * mapGenerationService.GetBlockHealthMultiplier(layerIndex);
                digFeedback.Break(mapGenerationService.CellToWorldCenter(layerIndex, cell.x, cell.y), direction, bonusBlock, bonusHealth, primary: false);
                CollectMinedBlock(bonusBlock, layerIndex, cell.x, cell.y);
            }
        }
    }
}
