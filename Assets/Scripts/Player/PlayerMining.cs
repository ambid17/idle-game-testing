using Economy;
using Events;
using MapGeneration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    // Directional mining per GameDesignDoc "Mechanics": holding A/S/D mines in that direction,
    // but only while grounded (PlayerController.IsGrounded). Resolves the targeted grid cell
    // through MapGenerationService's world<->cell helpers and mines it once BlockType.MiningTime
    // (scaled by the layer's BlockHealth and the Mining Speed upgrade) has elapsed. Per
    // "Inventory": once the carried weight is full, Ore-category blocks can no longer be mined
    // unless the Overflow upgrade is unlocked, in which case they're auto-sold instead.
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerMining : MonoBehaviour
    {
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;
        [SerializeField] private MiningCrackIndicator crackIndicator;
        [SerializeField] private bool debug;

        private PlayerController playerController;
        private PlayerInventory playerInventory;
        private CapsuleCollider2D capsuleCollider;
        private bool hasTarget;
        private int targetLayer, targetX, targetY;
        private float miningProgress;
        private bool wasBlockedByFullInventory;
        private UpgradeManager upgradeManager => UpgradeManager.Instance;

        private bool CanOverflow => UpgradeManager.Instance != null && UpgradeManager.Instance.OverflowUnlocked;
        

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerInventory = GetComponent<PlayerInventory>();
            capsuleCollider = GetComponent<CapsuleCollider2D>();

            if (crackIndicator == null) Debug.LogError($"{nameof(PlayerMining)} on {name} is missing its crackIndicator reference.");
        }

        private void Update()
        {
            streamingManager.SetFocusDepth(gameObject.name, transform.position.y);
            Vector2Int? direction = ResolveDirection();
            // GameDesignDoc "Prestige > Mining": the KeepDigWhileFlying perk lifts the normal
            // grounded-only mining restriction. Mining also burns fuel per tick (same tank as
            // flying/idle drain - see PlayerController.ConsumeMiningFuel), so an empty tank blocks
            // it too.
            bool canMine = (playerController.IsGrounded || (PrestigeUpgradeManager.Instance != null && PrestigeUpgradeManager.Instance.KeepDigWhileFlyingUnlocked)) && playerController.HasFuel;
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

            var bottomOfCollider = transform.position.y - (capsuleCollider.size.y / 2);
            var digDownDepth = direction.Value.y < 0 ? cellSize / 2 : 0;
            float targetYPos = bottomOfCollider - digDownDepth;

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
                GameManager.EventService.Dispatch(new HudNotificationEvent("Inventory is full!"));
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

            miningProgress += Time.deltaTime * upgradeManager.MiningSpeedMultiplier;
            playerController.ConsumeMiningFuel(Time.deltaTime);
            float targetBlockHealth = blockType.Health * mapGenerationService.GetBlockHealthMultiplier(layerIndex);

            // GameDesignDoc "Insta-mine chance": rolled once per newly-acquired target.
            var canInstaMine = isNewTarget && upgradeManager != null && upgradeManager.InstaMineChance > 0f && Random.value < upgradeManager.InstaMineChance;
            // GameDesignDoc "the final upgrade makes dirt/stone an instant mine".
            var canInstaMineDirt = blockType.Category == BlockCategory.Dirt && upgradeManager != null && upgradeManager.InstantMineDirt;
            // Mining_WoodInstaMine's capstone - see UpgradeManager.InstantMineScrapAlloy for the
            // "Wood" naming gap.
            var canInstaMineScrapAlloy = blockType.Id == BlockTypeId.ScrapAlloy && upgradeManager != null && upgradeManager.InstantMineScrapAlloy;
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
            if (!mapGenerationService.MineCell(layerIndex, x, y)) return;

            CollectMinedBlock(blockType, layerIndex);
            MineAreaBonusCells(layerIndex, x, y);
        }

        private void CollectMinedBlock(BlockType blockType, int layerIndex)
        {
            if (blockType.Category == BlockCategory.Artifact)
            {
                Wallet.Instance.AddArtifact();
                return;
            }
            if (blockType.Category != BlockCategory.Ore) return;

            GameManager.EventService.Dispatch(new OreMinedEvent(blockType.Id, 1));

            ApplyLayerBonus(blockType, layerIndex);

            if (playerInventory.IsFull && CanOverflow)
            {
                var upgrades = UpgradeManager.Instance;
                double value = blockType.Value * upgrades.OverflowSellFraction * upgrades.SellValueMultiplier;
                if (value > 0 && Wallet.Instance != null) Wallet.Instance.Add(value);
            }
            else
            {
                playerInventory.AddOre(blockType);
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

        // GameDesignDoc "Market Upgrades > Mining > Increase mining size": each unlocked offset
        // mines alongside the primary target cell for free (no extra time cost - the upgrade IS
        // the free hit).
        private void MineAreaBonusCells(int layerIndex, int centerX, int centerY)
        {
            var upgrades = UpgradeManager.Instance;
            if (upgrades == null || upgrades.MiningAreaLevel <= 0) return;

            foreach (var offset in MiningAreaPattern.GetOffsets(upgrades.MiningAreaLevel))
            {
                int x = centerX + offset.x;
                int y = centerY + offset.y;

                var bonusBlock = mapGenerationService.GetBlockTypeAt(layerIndex, x, y);
                if (bonusBlock == null) continue;
                if (bonusBlock.Category == BlockCategory.Ore && playerInventory.IsFull && !CanOverflow) continue;

                if (!mapGenerationService.MineCell(layerIndex, x, y)) continue;

                CollectMinedBlock(bonusBlock, layerIndex);
            }
        }
    }
}
