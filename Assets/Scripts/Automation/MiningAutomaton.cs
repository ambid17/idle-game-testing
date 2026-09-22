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
    // Depot to deposit once full. No health per the doc; hazard interactions are handled
    // generically by Player.HazardDamageHandler listening for HazardTriggeredEvent regardless of
    // who mined the cell, so this script needs no hazard-specific code.
    //
    // Burns fuel (Economy.FuelSystem, shared with Player.PlayerController) - idle drain always,
    // flying/mining drain stack on top while those states are active. Capacity/drain numbers live
    // on this prefab's own FuelSystem component, not AutomationConfig. An empty tank stalls the
    // automaton in place (Update's early-out below) until a Fuel Drone tops it back off; it never
    // "dies" the way the player can. FuelSystem self-heals rather than [RequireComponent] as a
    // defensive fallback (see PlayerInventory's OreInventory for the same trick), but is also
    // explicitly present on the prefab with tuned values. Also implements IFuelConsumer +
    // registers with FuelConsumerRegistry so Fuel Drones can find it.
    [RequireComponent(typeof(OreInventory))]
    public class MiningAutomaton : MonoBehaviour, IOreCarrier, IFuelConsumer
    {
        private enum State { PickingTarget, MovingAndDigging, Descending, FlyingToDepot, ReturningToRefuel }

        private static MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private static AutomationConfig config => GameManager.AutomationConfig;
        private static UpgradeManager upgrades => UpgradeManager.Instance;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;

        private OreInventory oreInventory;
        private FuelSystem fuelSystem;
        private readonly GridPathMover mover = new();
        [SerializeField] private State state = State.PickingTarget;
        [SerializeField] private MiningCrackIndicator crackIndicator;

        // Direction of the last chosen dig target relative to where it was picked from - biases
        // the next pick toward continuing the same "vein" instead of reversing course. Zero until
        // the first target is ever picked, or after crossing into a new layer via the unbounded
        // fallback (see UpdatePickingTarget), where continuing the old direction is meaningless.
        private Vector2 lastDigDirection;

        private int currentLayer;
        [SerializeField] private Vector2Int currentCell;
        [SerializeField] private List<Vector3> path;
        [SerializeField] private int pathIndex;
        [SerializeField] private int digTargetLayer;
        [SerializeField] private Vector2Int digTargetCell;
        [SerializeField] private float miningProgress;
        [SerializeField] private Vector3 _depotLocation;

        public int DisplayIndex { get; private set; } = 1;
        public Transform CarrierTransform => transform;
        public OreInventory Inventory => oreInventory;

        // IFuelConsumer - lets Fuel Drones find and refuel this automaton.
        public Transform FuelTransform => transform;
        public float FuelMissing => fuelSystem.FuelMissing;
        public float FuelMax => fuelSystem.MaxFuel;
        public void AddFuel(float amount) => fuelSystem.AddFuel(amount);

        // Assigned by AutomationSpawner - used for notification text ("Automaton #2") and the
        // Control Center earnings graph's per-automaton series.
        public void Configure(int displayIndex, Vector3 depotPosition)
        {
            DisplayIndex = displayIndex;
            _depotLocation = depotPosition;
        }

        private void Awake()
        {
            oreInventory = GetComponent<OreInventory>();
            if (oreInventory == null) Debug.LogError($"{nameof(MiningAutomaton)} on {name} is missing its required OreInventory component.");

            fuelSystem = GetComponent<FuelSystem>();
            if (fuelSystem == null) fuelSystem = gameObject.AddComponent<FuelSystem>();

            if (crackIndicator == null) Debug.LogError($"{nameof(MiningAutomaton)} on {name} is missing its crackIndicator reference.");
        }

        private void Start()
        {
            oreInventory.Initialize(() => config.AutomatonBaseInventoryWeight * upgrades.AutomatonInventoryCapacityMultiplier);
            // No upgrade-driven bonus/efficiency for automatons (unlike the player) - just the
            // capacity/drain values baked into this prefab's own FuelSystem component.
            fuelSystem.Initialize();
            RefreshCurrentCell();
        }

        private void OnEnable()
        {
            RefreshCurrentCell();
            OreCarrierRegistry.Instance.Register(this);
            FuelConsumerRegistry.Instance.Register(this);
        }
        // HasInstance guard: teardown order across objects isn't guaranteed when Stopping the
        // Player, so OreCarrierRegistry's singleton may already be destroyed by the time this runs.
        // (Instance would resurrect it as a stray GameObject mid-unload - HasInstance doesn't.)
        private void OnDisable()
        {
            if (OreCarrierRegistry.HasInstance) OreCarrierRegistry.Instance.Unregister(this);
            if (FuelConsumerRegistry.HasInstance) FuelConsumerRegistry.Instance.Unregister(this);
        }

        private void Update()
        {
            streamingManager.SetFocusDepth(gameObject.name, transform.position.y);

            fuelSystem.ConsumeIdle(Time.deltaTime);

            // Empty tank: abandon whatever it was doing and head for the Control Center to buy more,
            // rather than stalling in place waiting for a Fuel Drone to happen by. Consume() no-ops at
            // 0 fuel, so the trip home costs nothing further - it's running on fumes.
            if (fuelSystem.IsEmpty && state != State.ReturningToRefuel)
            {
                crackIndicator.Hide();
                state = State.ReturningToRefuel;
            }

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
                case State.ReturningToRefuel:
                    UpdateReturningToRefuel();
                    break;
            }

            RefreshCurrentCell();
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

            if (accessible.Count > 0)
            {
                digTargetLayer = currentLayer;
                digTargetCell = PickWeightedTarget(accessible, config.AutomatonWanderRadius);
                lastDigDirection = ((Vector2)(digTargetCell - currentCell)).normalized;
            }
            else
            {
                // Nothing within the normal wander radius - before giving up and drilling blind
                // straight down, try the whole reachable region instead. Local exhaustion often
                // means the only unmined ground left is past a building-support run wider than the
                // wander radius, or the current layer is fully mined out and the only way onward is
                // through the next layer down - either way this can cross into a deeper chunk.
                var unbounded = AutomatonReachability.GetAccessibleTilesUnbounded(mapGenerationService, currentLayer, currentCell.x, currentCell.y);
                if (unbounded.Count == 0)
                {
                    // "if there are no tiles in their radius, they will descend until they hit a block."
                    state = State.Descending;
                    return;
                }

                (digTargetLayer, digTargetCell) = unbounded[Random.Range(0, unbounded.Count)];
                // Crossed layers (or picked from an unrelated chunk) - the old direction no longer
                // means anything in the new grid, so the next bounded pick starts unbiased.
                lastDigDirection = Vector2.zero;
            }

            path = AutomatonReachability.BuildWorldPath(mapGenerationService, currentLayer, currentCell, digTargetLayer, digTargetCell);
            pathIndex = 0;
            state = State.MovingAndDigging;
        }

        // Weighted random pick over the wander-radius candidates: prefers tiles closer to
        // currentCell and, once a digging direction has been established, tiles that continue
        // that same direction (a "vein") over ones that reverse course - reads more like a person
        // following a lead than picking a totally new spot after every single block. Never fully
        // excludes any candidate (weights are clamped above zero) so a dead-ending vein can't get
        // the automaton stuck. AutomatonRandomBranchChance occasionally ignores both weights so it
        // still branches off for variety instead of tunneling in a perfectly straight line forever.
        private Vector2Int PickWeightedTarget(List<(Vector2Int Cell, int Depth)> candidates, int radius)
        {
            if (Random.value < config.AutomatonRandomBranchChance)
                return candidates[Random.Range(0, candidates.Count)].Cell;

            bool hasDirection = lastDigDirection != Vector2.zero;
            float bias = config.AutomatonDirectionBiasStrength;

            float totalWeight = 0f;
            var weights = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                var (cell, depth) = candidates[i];
                float distanceWeight = radius - depth + 1;

                float directionWeight = 1f;
                if (hasDirection)
                {
                    Vector2 offset = ((Vector2)(cell - currentCell)).normalized;
                    directionWeight = Mathf.Max(0.05f, 1f + bias * Vector2.Dot(offset, lastDigDirection));
                }

                weights[i] = distanceWeight * directionWeight;
                totalWeight += weights[i];
            }

            float roll = Random.value * totalWeight;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return candidates[i].Cell;
            }

            return candidates[^1].Cell;
        }

        private void UpdateMovingAndDigging()
        {
            if (path == null || path.Count == 0)
            {
                state = State.PickingTarget;
                return;
            }

            float speed = config.AutomatonBaseMoveSpeed * upgrades.AutomatonMoveSpeedMultiplier;
            mover.StepAlongPath(transform, path, ref pathIndex, speed, cornerRadius: config.AutomatonCornerRadius);

            // The final waypoint is the dig target cell itself (unmined) - mine it in place once
            // that's the active waypoint, mirroring PlayerMining accruing progress while the
            // player is simply facing the target rather than fully "arrived."
            if (pathIndex < path.Count - 1)
            {
                return;
            }

            var blockType = mapGenerationService.GetBlockTypeAt(digTargetLayer, digTargetCell.x, digTargetCell.y);
            if (blockType == null)
            {
                // Already mined out from under us (e.g. the player got there first) - move on.
                crackIndicator.Hide();
                state = State.PickingTarget;
                return;
            }

            float miningSpeed = config.AutomatonBaseMiningSpeed * upgrades.AutomatonMiningSpeedMultiplier;
            miningProgress += Time.deltaTime * miningSpeed;
            fuelSystem.ConsumeMining(Time.deltaTime);
            float targetHealth = blockType.Health * mapGenerationService.GetBlockHealthMultiplier(digTargetLayer);
            if (miningProgress < targetHealth)
            {
                crackIndicator.Show(mapGenerationService.CellToWorldCenter(digTargetLayer, digTargetCell.x, digTargetCell.y), miningProgress / targetHealth);
                return;
            }

            crackIndicator.Hide();
            MineTargetAndBonusCells(digTargetLayer, digTargetCell, blockType);
            state = State.PickingTarget;
        }

        // Straight-down fallback wander, resolved fresh every frame via WorldToCell (like
        // PlayerMining's own targeting) rather than a cached layer/cell pair, so it naturally
        // crosses into the next layer instead of getting stuck at a chunk's bottom edge.
        private void UpdateDescending()
        {
            float cellSize = mapGenerationService.CellSize;
            Vector3 targetWorldPos = new(transform.position.x, transform.position.y - cellSize, 0f);

            if (!mapGenerationService.TryWorldToCellInBounds(targetWorldPos, out int layer, out int x, out int y))
            {
                state = State.PickingTarget;
                return;
            }

            var blockType = mapGenerationService.GetBlockTypeAt(layer, x, y);
            if (blockType == null)
            {
                // Already-open ground directly below - step down into it and keep descending.
                crackIndicator.Hide();
                float moveSpeed = config.AutomatonBaseMoveSpeed * upgrades.AutomatonMoveSpeedMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, mapGenerationService.CellToWorldCenter(layer, x, y), moveSpeed * Time.deltaTime);
                return;
            }

            float miningSpeed = config.AutomatonBaseMiningSpeed * upgrades.AutomatonMiningSpeedMultiplier;
            miningProgress += Time.deltaTime * miningSpeed;
            fuelSystem.ConsumeMining(Time.deltaTime);
            float targetHealth = blockType.Health * mapGenerationService.GetBlockHealthMultiplier(layer);
            if (miningProgress < targetHealth)
            {
                crackIndicator.Show(mapGenerationService.CellToWorldCenter(layer, x, y), miningProgress / targetHealth);
                return;
            }

            crackIndicator.Hide();
            MineTargetAndBonusCells(layer, new Vector2Int(x, y), blockType);
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
            if (mapGenerationService.TryWorldToCellInBounds(transform.position, out int layer, out int x, out int y))
            {
                currentLayer = layer;
                currentCell = new Vector2Int(x, y);
            }
        }

        private void UpdateFlyingToDepot()
        {
            fuelSystem.ConsumeFlying(Time.deltaTime);

            float speed = config.AutomatonBaseMoveSpeed * upgrades.AutomatonMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, _depotLocation, speed);
            if (!arrived) return;

            Deposit();

            if(fuelSystem.FuelFraction < 0.5f)
            {
                state = State.ReturningToRefuel;
                return;
            }
            state = State.PickingTarget;
        }

        private void Deposit()
        {
            var withdrawn = oreInventory.WithdrawAllOre();
            AutomationDepositService.Deposit($"Automaton #{DisplayIndex}", withdrawn);
        }

        // Reuses _depotLocation (the Control Center's deposit point, same spot Fuel Drones idle at)
        // rather than a separate refuel destination - there's only the one Control Center.
        private void UpdateReturningToRefuel()
        {
            float speed = config.AutomatonBaseMoveSpeed * upgrades.AutomatonMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, _depotLocation, speed);
            if (!arrived) return;

            PurchaseFuel();
            // If funds ran out, stay parked here rather than bouncing back to PickingTarget only to
            // immediately re-trigger this same state - wait for more money or a passing Fuel Drone.
            if (!fuelSystem.IsEmpty)
            {
                if(oreInventory.CurrentWeight > 0f)
                {
                    state = State.FlyingToDepot;
                }
                else
                {
                    state = State.PickingTarget;
                }
            }
        }

        // Mirrors UI.ResourceRefillUI.TryFillFuel's player-facing purchase - buys as much of the
        // missing fuel as the wallet can afford, same per-unit price.
        private void PurchaseFuel()
        {
            float unitsNeeded = fuelSystem.FuelMissing;
            if (unitsNeeded <= 0f) return;

            float unitsAffordable = Mathf.FloorToInt((float)(Wallet.Instance.Dollars / config.FuelCostPerUnit));
            float unitsToBuy = Mathf.Min(unitsNeeded, unitsAffordable);
            if (unitsToBuy <= 0f) return;

            if (!Wallet.Instance.TrySpend(unitsToBuy * config.FuelCostPerUnit)) return;
            fuelSystem.AddFuel(unitsToBuy);
        }

#if UNITY_EDITOR
        // Editor-only inspection aid (see EditorTools.Automation.MiningAutomatonEditor for the
        // Inspector-side counterpart): draws the current path, dig target, and depot leg in the
        // Scene view when this automaton is selected, so its behavior can be observed without
        // temporary Debug.Log calls.
        private void OnDrawGizmosSelected()
        {
            if (path != null && path.Count > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < path.Count - 1; i++)
                    Gizmos.DrawLine(path[i], path[i + 1]);

                for (int i = 0; i < path.Count; i++)
                {
                    Gizmos.color = i == pathIndex ? Color.yellow : Color.cyan;
                    Gizmos.DrawSphere(path[i], i == pathIndex ? 0.18f : 0.1f);
                }
            }

            if (state == State.MovingAndDigging || state == State.Descending)
            {
                Gizmos.color = Color.red;
                Vector3 targetWorld = mapGenerationService.CellToWorldCenter(digTargetLayer, digTargetCell.x, digTargetCell.y);
                Gizmos.DrawWireCube(targetWorld, Vector3.one * mapGenerationService.CellSize);
            }

            if (state == State.FlyingToDepot)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, _depotLocation);
                Gizmos.DrawWireSphere(_depotLocation, 0.3f);
            }

            if (state == State.ReturningToRefuel)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, _depotLocation);
                Gizmos.DrawWireSphere(_depotLocation, 0.3f);
            }

            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, state.ToString());
        }
#endif
    }
}
