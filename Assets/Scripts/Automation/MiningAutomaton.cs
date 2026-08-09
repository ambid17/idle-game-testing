using System.Collections.Generic;
using Economy;
using Events;
using MapGeneration;
using Player;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Automation > Mining Automatons": autonomous entity that wanders the mine,
    // digs like the player (down/left/right, through MapGenerationService.MineCell - the same
    // single mining codepath PlayerMining uses), fills its own OreInventory, and flies to the
    // Depot to deposit once full. No health/fuel per the doc; hazard interactions are handled
    // generically by Player.HazardDamageHandler listening for HazardTriggeredEvent regardless of
    // who mined the cell, so this script needs no hazard-specific code.
    [RequireComponent(typeof(OreInventory))]
    public class MiningAutomaton : MonoBehaviour, IOreCarrier
    {
        private enum State { PickingTarget, MovingAndDigging, Descending, FlyingToDepot }

        private static MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private static AutomationConfig config => GameManager.AutomationConfig;
        private static UpgradeManager upgrades => UpgradeManager.Instance;

        private OreInventory oreInventory;
        private readonly GridPathMover mover = new();
        private State state = State.PickingTarget;

        private int currentLayer;
        private Vector2Int currentCell;
        private List<Vector3> path;
        private int pathIndex;
        private Vector2Int digTargetCell;
        private float miningProgress;

        public int DisplayIndex { get; private set; } = 1;
        public Transform CarrierTransform => transform;
        public OreInventory Inventory => oreInventory;

        // Assigned by AutomationSpawner - used for notification text ("Automaton #2") and the
        // Control Center earnings graph's per-automaton series.
        public void Configure(int displayIndex) => DisplayIndex = displayIndex;

        private void Awake()
        {
            oreInventory = GetComponent<OreInventory>();
            if (oreInventory == null) Debug.LogError($"{nameof(MiningAutomaton)} on {name} is missing its required OreInventory component.");
        }

        private void Start()
        {
            oreInventory.Initialize(() => config.AutomatonBaseInventoryWeight * upgrades.AutomatonInventoryCapacityMultiplier);
            RefreshCurrentCell();
        }

        private void OnEnable() => OreCarrierRegistry.Instance.Register(this);
        private void OnDisable() => OreCarrierRegistry.Instance.Unregister(this);

        private void Update()
        {
            switch (state)
            {
                case State.PickingTarget:
                    UpdatePickingTarget();
                    break;
                case State.MovingAndDigging:
                    UpdateMovingAndDigging();
                    break;
                case State.Descending:
                    UpdateDescending();
                    break;
                case State.FlyingToDepot:
                    UpdateFlyingToDepot();
                    break;
            }
        }

        private void UpdatePickingTarget()
        {
            if (oreInventory.IsFull)
            {
                state = State.FlyingToDepot;
                return;
            }

            var accessible = AutomatonReachability.GetAccessibleTiles(mapGenerationService, currentLayer, currentCell.x, currentCell.y, config.AutomatonWanderRadius);
            miningProgress = 0f;

            if (accessible.Count == 0)
            {
                // "if there are no tiles in their radius, they will descend until they hit a block."
                state = State.Descending;
                return;
            }

            digTargetCell = accessible[Random.Range(0, accessible.Count)];
            path = AutomatonReachability.BuildWorldPath(mapGenerationService, currentLayer, currentCell, digTargetCell);
            pathIndex = 0;
            state = State.MovingAndDigging;
        }

        private void UpdateMovingAndDigging()
        {
            if (path == null || path.Count == 0)
            {
                state = State.PickingTarget;
                return;
            }

            float speed = config.AutomatonBaseMoveSpeed * upgrades.AutomatonMoveSpeedMultiplier;
            mover.StepAlongPath(transform, path, ref pathIndex, speed);

            // The final waypoint is the dig target cell itself (unmined) - mine it in place once
            // that's the active waypoint, mirroring PlayerMining accruing progress while the
            // player is simply facing the target rather than fully "arrived."
            if (pathIndex < path.Count - 1) return;

            var blockType = mapGenerationService.GetBlockTypeAt(currentLayer, digTargetCell.x, digTargetCell.y);
            if (blockType == null)
            {
                // Already mined out from under us (e.g. the player got there first) - move on.
                RefreshCurrentCell();
                state = State.PickingTarget;
                return;
            }

            float miningSpeed = config.AutomatonBaseMiningSpeed * upgrades.AutomatonMiningSpeedMultiplier;
            miningProgress += Time.deltaTime * miningSpeed;
            float targetHealth = blockType.Health * mapGenerationService.GetBlockHealthMultiplier(currentLayer);
            if (miningProgress < targetHealth) return;

            MineTargetAndBonusCells(currentLayer, digTargetCell, blockType);
            RefreshCurrentCell();
            state = State.PickingTarget;
        }

        // Straight-down fallback wander, resolved fresh every frame via WorldToCell (like
        // PlayerMining's own targeting) rather than a cached layer/cell pair, so it naturally
        // crosses into the next layer instead of getting stuck at a chunk's bottom edge.
        private void UpdateDescending()
        {
            float cellSize = mapGenerationService.CellSize;
            Vector3 targetWorldPos = new(transform.position.x, transform.position.y - cellSize, 0f);

            if (!mapGenerationService.WorldToCell(targetWorldPos, out int layer, out int x, out int y))
            {
                state = State.PickingTarget;
                return;
            }

            var blockType = mapGenerationService.GetBlockTypeAt(layer, x, y);
            if (blockType == null)
            {
                // Already-open ground directly below - step down into it and keep descending.
                float moveSpeed = config.AutomatonBaseMoveSpeed * upgrades.AutomatonMoveSpeedMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, mapGenerationService.CellToWorldCenter(layer, x, y), moveSpeed * Time.deltaTime);
                RefreshCurrentCell();
                return;
            }

            float miningSpeed = config.AutomatonBaseMiningSpeed * upgrades.AutomatonMiningSpeedMultiplier;
            miningProgress += Time.deltaTime * miningSpeed;
            float targetHealth = blockType.Health * mapGenerationService.GetBlockHealthMultiplier(layer);
            if (miningProgress < targetHealth) return;

            MineTargetAndBonusCells(layer, new Vector2Int(x, y), blockType);
            RefreshCurrentCell();
            state = State.PickingTarget;
        }

        // GameDesignDoc Control Center "increase mining radius by 1 (max 2)": reuses the same
        // offset pattern as the player's mining-size upgrade (Player.MiningAreaPattern) rather than
        // inventing a separate one - the design doc gives no distinct shape for the automaton
        // version.
        private void MineTargetAndBonusCells(int layer, Vector2Int primaryCell, BlockType primaryBlockType)
        {
            if (mapGenerationService.MineCell(layer, primaryCell.x, primaryCell.y))
            {
                CollectMinedBlock(primaryBlockType);
            }

            int radiusLevel = upgrades.AutomatonMiningRadiusBonus;
            if (radiusLevel <= 0) return;

            foreach (var offset in MiningAreaPattern.GetOffsets(radiusLevel))
            {
                var cell = primaryCell + offset;
                var bonusBlock = mapGenerationService.GetBlockTypeAt(layer, cell.x, cell.y);
                if (bonusBlock == null) continue;
                if (bonusBlock.Category == BlockCategory.Ore && oreInventory.IsFull) continue;

                if (!mapGenerationService.MineCell(layer, cell.x, cell.y)) continue;
                CollectMinedBlock(bonusBlock);
            }
        }

        private void CollectMinedBlock(BlockType blockType)
        {
            if (blockType == null) return;
            if (blockType.Category == BlockCategory.Artifact)
            {
                Wallet.Instance.AddArtifact();
                return;
            }
            if (blockType.Category != BlockCategory.Ore) return;
            oreInventory.AddOre(blockType);
        }

        private void RefreshCurrentCell()
        {
            if (mapGenerationService.WorldToCell(transform.position, out int layer, out int x, out int y))
            {
                currentLayer = layer;
                currentCell = new Vector2Int(x, y);
            }
        }

        private void UpdateFlyingToDepot()
        {
            float speed = config.AutomatonBaseMoveSpeed * upgrades.AutomatonMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, Depot.Instance.transform.position, speed);
            if (!arrived) return;

            Deposit();
            RefreshCurrentCell();
            state = State.PickingTarget;
        }

        private void Deposit()
        {
            var withdrawn = oreInventory.WithdrawAllOre();
            if (withdrawn.Count == 0) return;

            Depot.Instance.Deposit(withdrawn);

            foreach (var kvp in withdrawn)
            {
                if (kvp.Value > 0) IdleEarningsTracker.Instance.RecordOreMined(kvp.Key, kvp.Value);
            }

            GameManager.EventService.Dispatch(new OreDepositedByAutomationEvent($"Automaton #{DisplayIndex}", withdrawn, DisplayIndex));
        }
    }
}
