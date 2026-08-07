using MapGeneration;
using UnityEngine;

namespace Player
{
    // Directional mining per GameDesignDoc "Mechanics": holding A/S/D mines in that direction,
    // but only while grounded (PlayerController.IsGrounded). Resolves the targeted grid cell
    // through MapGenerationService's world<->cell helpers and mines it once BlockType.MiningTime
    // (scaled by the layer's BlockHealth) has elapsed.
    [RequireComponent(typeof(PlayerController))]
    public class PlayerMining : MonoBehaviour
    {
        [SerializeField] private MapGenerationService mapGenerationService;

        private PlayerController playerController;
        private bool hasTarget;
        private int targetLayer, targetX, targetY;
        private float miningProgress;

        private void Awake() => playerController = GetComponent<PlayerController>();

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
            if (blockType == null)
            {
                ResetTarget();
                return;
            }

            miningProgress += Time.deltaTime;
            float miningTime = blockType.MiningTime * mapGenerationService.GetBlockHealthMultiplier(layerIndex);
            if (miningProgress >= miningTime)
            {
                mapGenerationService.MineCell(layerIndex, x, y);
                ResetTarget();
            }
        }

        private static Vector2Int? ResolveDirection()
        {
            if (Input.GetKey(KeyCode.A)) return Vector2Int.left;
            if (Input.GetKey(KeyCode.D)) return Vector2Int.right;
            if (Input.GetKey(KeyCode.S)) return Vector2Int.down;
            return null;
        }

        private void ResetTarget()
        {
            hasTarget = false;
            miningProgress = 0f;
        }
    }
}
