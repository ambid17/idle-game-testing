using Economy;
using Events;
using MapGeneration;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public class PlayerMining : MonoBehaviour
    {
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;
        [SerializeField] private MiningCrackIndicator crackIndicator;
        [SerializeField] private bool debug;

        private PlayerController playerController;
        private PlayerInventory playerInventory;
        private PlayerPowerUps playerPowerUps;
        private CapsuleCollider2D capsuleCollider;
        private bool hasTarget;
        private int targetLayer, targetX, targetY;
        private float miningProgress;
        private bool wasBlockedByFullInventory;
        private UpgradeManager upgradeManager => UpgradeManager.Instance;

        private bool CanOverflow => UpgradeManager.Instance != null && UpgradeManager.Instance.Economy_OverflowUnlocked;
        

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerInventory = GetComponent<PlayerInventory>();
            capsuleCollider = GetComponent<CapsuleCollider2D>();
            playerPowerUps = GetComponent<PlayerPowerUps>();

            if (playerPowerUps == null) Debug.LogError($"{nameof(PlayerMining)} on {name} requires a PlayerPowerUps component.");
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
            if (!canMine || direction == null || InputBlocker.IsBlocked)
            {
                if(debug) Debug.Log($"PlayerMining: not mining because: IsGrounded={playerController.IsGrounded}, direction={direction}, InputBlocker.IsBlocked={InputBlocker.IsBlocked}");
                wasBlockedByFullInventory = false;
                ResetTarget();
                return;
            }

            // The player's pivot sits at chest height, above the row they're standing on top of,
            // so targeting always resolves against that standing row's vertical center rather
            // than the raw pivot Y - otherwise horizontal targets resolve one row too high and
            // never hit a block (only "down" ever happened to land in-bounds by coincidence).
            float cellSize = mapGenerationService.CellSize;

            // Digging up targets the cell just above the top of the collider instead.
            var bottomOfCollider = transform.position.y - (capsuleCollider.size.y / 2);
            var topOfCollider = transform.position.y + (capsuleCollider.size.y / 2);
            var digDownDepth = direction.Value.y < 0 ? cellSize / 2 : 0;
            float targetYPos = isDiggingUp ? topOfCollider + cellSize / 2 : bottomOfCollider - digDownDepth;

            Vector3 miningTargetWorldPos = new Vector3(transform.position.x + direction.Value.x * cellSize, targetYPos, 0f);

            if (!mapGenerationService.TryWorldToCellInBounds(miningTargetWorldPos, out int layerIndex, out int targetCellX, out int targetCellY))
            {
                if (debug) Debug.LogWarning($"PlayerMining: failed to resolve target cell at {miningTargetWorldPos} (playerPos: {transform.position.ToFormattedString()}, direction {direction.ToFormattedString()}). Resolved Cell: ({targetCellX}, {targetCellY})");
                wasBlockedByFullInventory = false;
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
            }

            var blockType = mapGenerationService.GetBlockTypeAt(layerIndex, targetCellX, targetCellY);
            bool blockedByFullInventory = blockType != null && blockType.Category == BlockCategory.Ore && playerInventory.IsFull && !CanOverflow;
            
            // Edge-triggered like PlayerController's low-fuel check: fires once when mining first
            // becomes blocked, not every frame it stays blocked, so it can't drown out other HUD
            // notifications sharing the same toast.
            if (blockedByFullInventory && !wasBlockedByFullInventory)
            {
                GameManager.EventService.Dispatch(new NotificationEvent("Inventory is full!", NotificationUrgency.TimeSensitive));
            }
            wasBlockedByFullInventory = blockedByFullInventory;

            if (blockType == null
                || (blockType.Id == (byte)BlockTypeId.GrassyDirt)
                || blockType.Id == BlockTypeId.FallingRock
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
                MineTarget(layerIndex, targetCellX, targetCellY, blockType);
                ResetTarget();
                return;
            }

            if (crackIndicator != null)
            {
                crackIndicator.Show(mapGenerationService.CellToWorldCenter(layerIndex, targetCellX, targetCellY), miningProgress / targetBlockHealth);
            }
        }

        private static Vector2Int? ResolveDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;

            if (keyboard.aKey.isPressed) return Vector2Int.left;
            if (keyboard.dKey.isPressed) return Vector2Int.right;
            if (keyboard.sKey.isPressed) return Vector2Int.down;
            // W also fires the jetpack (PlayerController), so digging up only happens while the
            // player is holding W against a block overhead.
            if (keyboard.wKey.isPressed && PrestigeUpgradeManager.Instance.Mining_DigUpUnlocked) return Vector2Int.up;
            return null;
        }

        private void ResetTarget()
        {
            hasTarget = false;
            miningProgress = 0f;
            if (crackIndicator != null) crackIndicator.Hide();
        }

        private void MineTarget(int layerIndex, int x, int y, BlockType blockType)
        {
            if (!mapGenerationService.MineCell(layerIndex, x, y, minedByPlayer: true)) return;

            CollectMinedBlock(blockType, layerIndex, x, y);
            MineAreaBonusCells(layerIndex, x, y, blockType);
        }

        private void CollectMinedBlock(BlockType blockType, int layerIndex, int x, int y)
        {
            if (blockType.Category == BlockCategory.PowerUp)
            {
                playerPowerUps.Apply(blockType, layerIndex, x, y);
                return;
            }
            if (blockType.Category == BlockCategory.Artifact)
            {
                GameManager.EventService.Dispatch(new NotificationEvent($"+1 <color=purple>Artifact</color>", NotificationUrgency.Queued, blockType.Icon));
                Wallet.Instance.AddArtifact();
                return;
            }
            if (blockType.Category != BlockCategory.Ore) return;

            // Lucky Strike power-up (see PlayerPowerUps): 2 while charges remain, else 1.
            int amount = playerPowerUps.ConsumeLuckyStrikeMultiplier();
            GameManager.EventService.Dispatch(new NotificationEvent($"+{amount} {blockType.DisplayName}", NotificationUrgency.Queued, blockType.Icon));

            for (int i = 0; i < amount; i++) ApplyLayerBonus(blockType, layerIndex);

            if (playerInventory.IsFull && CanOverflow)
            {
                var upgrades = UpgradeManager.Instance;
                double value = blockType.Value * amount * upgrades.Economy_OverflowSellFraction * upgrades.Economy_SellValueMultiplier;
                if (value > 0 && Wallet.Instance != null) Wallet.Instance.Add(value);
            }
            else
            {
                playerInventory.AddOre(blockType, amount);
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
        private void MineAreaBonusCells(int layerIndex, int centerX, int centerY, BlockType primaryBlockType)
        {
            if (primaryBlockType.Category != BlockCategory.Ore) return;

            var upgrades = UpgradeManager.Instance;
            if (upgrades == null || upgrades.Mining_AreaLevel <= 0) return;

            foreach (var cell in VeinMiningPattern.GetChainCells(mapGenerationService, layerIndex, centerX, centerY, upgrades.Mining_AreaLevel))
            {
                var bonusBlock = mapGenerationService.GetBlockTypeAt(layerIndex, cell.x, cell.y);
                if (bonusBlock == null) continue;
                if (bonusBlock.Category == BlockCategory.Ore && playerInventory.IsFull && !CanOverflow) continue;

                if (!mapGenerationService.MineCell(layerIndex, cell.x, cell.y, minedByPlayer: true)) continue;

                CollectMinedBlock(bonusBlock, layerIndex, cell.x, cell.y);
            }
        }
    }
}
