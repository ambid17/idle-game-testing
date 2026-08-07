using MapGeneration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    // Directional mining per GameDesignDoc "Mechanics": holding A/S/D mines in that direction,
    // but only while grounded (PlayerController.IsGrounded). Resolves the targeted grid cell
    // through MapGenerationService's world<->cell helpers and mines it once BlockType.MiningTime
    // (scaled by the layer's BlockHealth) has elapsed. Per "Inventory": once the carried weight is
    // full, Ore-category blocks can no longer be mined, but Dirt/Hazard/PowerUp blocks still can.
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerMining : MonoBehaviour
    {
        [SerializeField] private MapGenerationService mapGenerationService;

        private PlayerController playerController;
        private PlayerInventory playerInventory;
        private bool hasTarget;
        private int targetLayer, targetX, targetY;
        private float miningProgress;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerInventory = GetComponent<PlayerInventory>();
        }

        private void Update()
        {
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

            if (!hasTarget || layerIndex != targetLayer || x != targetX || y != targetY)
            {
                targetLayer = layerIndex;
                targetX = x;
                targetY = y;
                hasTarget = true;
                miningProgress = 0f;
            }

            var blockType = mapGenerationService.GetBlockTypeAt(layerIndex, x, y);
            if (blockType == null || (blockType.Category == BlockCategory.Ore && playerInventory.IsFull))
            {
                ResetTarget();
                return;
            }

            miningProgress += Time.deltaTime;
            float miningTime = blockType.MiningTime * mapGenerationService.GetBlockHealthMultiplier(layerIndex);
            if (miningProgress >= miningTime)
            {
                bool hadArtifact = mapGenerationService.IsArtifactAt(layerIndex, x, y);
                if (mapGenerationService.MineCell(layerIndex, x, y))
                {
                    if (blockType.Category == BlockCategory.Ore) playerInventory.AddOre(blockType);
                    if (hadArtifact) playerInventory.AddArtifact();
                }
                ResetTarget();
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
        }
    }
}
