using Economy;
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
    public class PlayerMining : MonoBehaviour
    {
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;
        [SerializeField] private MiningCrackIndicator crackIndicator;

        private PlayerController playerController;
        private PlayerInventory playerInventory;
        private bool hasTarget;
        private int targetLayer, targetX, targetY;
        private float miningProgress;
        private UpgradeManager upgradeManager => UpgradeManager.Instance;

        private bool CanOverflow => UpgradeManager.Instance != null && UpgradeManager.Instance.OverflowUnlocked;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerInventory = GetComponent<PlayerInventory>();

            if (crackIndicator == null) Debug.LogError($"{nameof(PlayerMining)} on {name} is missing its crackIndicator reference.");
        }

        private void Update()
        {
            streamingManager.SetFocusDepth(-transform.position.y);
            if (!playerController.IsGrounded)
            {
                ResetTarget();
                return;
            }

            Vector2Int? direction = ResolveDirection();
            if (direction == null)
            {
                ResetTarget();
                return;
            }

            Vector3 targetWorldPos = transform.position + new Vector3(direction.Value.x, direction.Value.y, 0f) * mapGenerationService.CellSize;
            if (!mapGenerationService.WorldToCell(targetWorldPos, out int layerIndex, out int x, out int y))
            {
                ResetTarget();
                return;
            }

            bool isNewTarget = !hasTarget || layerIndex != targetLayer || x != targetX || y != targetY;
            if (isNewTarget)
            {
                targetLayer = layerIndex;
                targetX = x;
                targetY = y;
                hasTarget = true;
                miningProgress = 0f;
            }

            var blockType = mapGenerationService.GetBlockTypeAt(layerIndex, x, y);
            if (blockType == null || (blockType.Category == BlockCategory.Ore && playerInventory.IsFull && !CanOverflow))
            {
                ResetTarget();
                return;
            }

            

            float speedMultiplier = upgradeManager != null ? upgradeManager.MiningSpeedMultiplier : 1f;
            miningProgress += Time.deltaTime * speedMultiplier;
            float targetBlockHealth = blockType.Health * mapGenerationService.GetBlockHealthMultiplier(layerIndex);

            // GameDesignDoc "Insta-mine chance": rolled once per newly-acquired target.
            var canInstaMine = isNewTarget && upgradeManager != null && upgradeManager.InstaMineChance > 0f && Random.value < upgradeManager.InstaMineChance;
            // GameDesignDoc "the final upgrade makes dirt/stone an instant mine".
            var canInstaMineDirt = blockType.Category == BlockCategory.Dirt && upgradeManager != null && upgradeManager.InstantMineDirt;
            var finishedMining =  miningProgress >= targetBlockHealth;
            if (canInstaMine || canInstaMineDirt || finishedMining)
            {
                MineTarget(layerIndex, x, y, blockType);
                ResetTarget();
                return;
            }

            if (crackIndicator != null)
            {
                crackIndicator.Show(mapGenerationService.CellToWorldCenter(layerIndex, x, y), miningProgress / targetBlockHealth);
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
            bool hadArtifact = mapGenerationService.IsArtifactAt(layerIndex, x, y);
            if (!mapGenerationService.MineCell(layerIndex, x, y)) return;

            CollectMinedBlock(blockType);
            if (hadArtifact) playerInventory.AddArtifact();

            MineAreaBonusCells(layerIndex, x, y);
        }

        private void CollectMinedBlock(BlockType blockType)
        {
            if (blockType.Category != BlockCategory.Ore) return;

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

                bool hadArtifact = mapGenerationService.IsArtifactAt(layerIndex, x, y);
                if (!mapGenerationService.MineCell(layerIndex, x, y)) continue;

                CollectMinedBlock(bonusBlock);
                if (hadArtifact) playerInventory.AddArtifact();
            }
        }
    }
}
