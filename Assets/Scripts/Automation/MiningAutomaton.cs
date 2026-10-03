using System.Collections;
using System.Collections.Generic;
using Buildings;
using Critters;
using Economy;
using Events;
using MapGeneration;
using Museum;
using Player;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Automation > Mining Automatons": autonomous entity that wanders the mine,
    // digs like the player (down/left/right, through MapGenerationService.MineCell - the same
    // single mining codepath PlayerMining uses), fills its own OreInventory, and travels back to
    // the Depot to deposit once full - always through open cells (AutomatonReachability.IsWalkable),
    // never through solid ground. No health per the doc; hazard interactions are handled
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
        private enum State { PickingTarget, MovingAndDigging, Descending, FlyingToDepot, ReturningToRefuel, LeavingDepot }

        private static MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private static AutomationConfig config => GameManager.AutomationConfig;
        private static UpgradeManager upgrades => UpgradeManager.Instance;
        private ChunkStreamingManager streamingManager => GameManager.ChunkStreamingManager;

        private OreInventory oreInventory;
        private FuelSystem fuelSystem;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer hatRenderer;
        private HatDefinition currentHat;
        private readonly GridPathMover mover = new();
        [SerializeField] private State state = State.PickingTarget;
        [SerializeField] private MiningCrackIndicator crackIndicator;
        [Tooltip("X of the top of the drone's dome relative to the sprite pivot, for the art facing right (world units). Mirrored when flipped.")]
        [SerializeField] private float headCenterX = -0.105f;

        // Direction of the last chosen dig target relative to where it was picked from - biases
        // the next pick toward continuing the same "vein" instead of reversing course. Zero until
        // the first target is ever picked, or after crossing into a new layer via the unbounded
        // fallback (see UpdatePickingTarget), where continuing the old direction is meaningless.
        private Vector2 lastDigDirection;

        private const float InsideDoorwayScale = 0.5f;
        private static readonly Color InsideDoorwayTint = new(0.25f, 0.3f, 0.35f, 1f);
        private bool heldForDeployment;

        private int currentLayer;
        [SerializeField] private Vector2Int currentCell;
        [SerializeField] private List<Vector3> path;
        [SerializeField] private int pathIndex;
        [SerializeField] private int digTargetLayer;
        [SerializeField] private Vector2Int digTargetCell;
        [SerializeField] private float miningProgress;
        [SerializeField] private Vector3 _depotLocation;
        private DepotDoor depotDoor;

        public int DisplayIndex { get; private set; } = 1;
        public Transform CarrierTransform => transform;
        public OreInventory Inventory => oreInventory;

        // Read by AutomatonAnimation. IsMining is only true on frames that actually accrue dig
        // progress (reset at the top of every Update), MiningTargetPosition is that block's center.
        public bool IsMining { get; private set; }
        public Vector3 MiningTargetPosition { get; private set; }
        public bool IsFlying => state is State.FlyingToDepot or State.ReturningToRefuel or State.LeavingDepot;

        // IFuelConsumer - lets Fuel Drones find and refuel this automaton.
        public Transform FuelTransform => transform;
        public float FuelMissing => fuelSystem.FuelMissing;
        public float FuelMax => fuelSystem.MaxFuel;
        public void AddFuel(float amount) => fuelSystem.AddFuel(amount);

        // Assigned by AutomationSpawner - used for notification text ("Automaton #2") and the
        // Control Center earnings graph's per-automaton series.
        public void Configure(int displayIndex, Vector3 depotPosition, DepotDoor depotDoor)
        {
            DisplayIndex = displayIndex;
            _depotLocation = depotPosition;
            this.depotDoor = depotDoor;
            RefreshHat();
        }

        // Control Center reveal cinematic (ControlCenterRevealController): the first automaton is
        // bought before its building has appeared, so it waits hidden and inert until the doors
        // open, then WalkOut plays and Release hands control back to the state machine.
        public void HoldInside()
        {
            heldForDeployment = true;
            SetVisible(false);
        }

        public void Release()
        {
            heldForDeployment = false;
            SetVisible(true);
        }

        // Emerges from the doorway toward the camera (grows and brightens out of the dark
        // interior onto the doorstep), then walks clear of the building.
        public IEnumerator WalkOut(Vector3 doorway, Vector3 doorstep, Vector3 exit, float emergeSeconds, float walkSpeed)
        {
            Vector3 fullScale = transform.localScale;
            Color fullColor = bodyRenderer.color;
            transform.position = doorway;
            SetVisible(true);

            float elapsed = 0f;
            while (elapsed < emergeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / emergeSeconds));
                transform.position = Vector3.Lerp(doorway, doorstep, t);
                transform.localScale = fullScale * Mathf.Lerp(InsideDoorwayScale, 1f, t);
                SetTint(Color.Lerp(InsideDoorwayTint * fullColor, fullColor, t));
                yield return null;
            }
            transform.localScale = fullScale;
            SetTint(fullColor);

            while (!mover.StepDirect(transform, exit, walkSpeed, arriveThreshold: 0.02f))
            {
                yield return null;
            }
        }

        private void SetVisible(bool visible)
        {
            bodyRenderer.enabled = visible;
            if (visible) RefreshHat();
            else hatRenderer.enabled = false;
        }

        private void SetTint(Color color)
        {
            bodyRenderer.color = color;
            hatRenderer.color = color;
        }

        private void Awake()
        {
            oreInventory = GetComponent<OreInventory>();
            if (oreInventory == null) Debug.LogError($"{nameof(MiningAutomaton)} on {name} is missing its required OreInventory component.");

            fuelSystem = GetComponent<FuelSystem>();
            if (fuelSystem == null) fuelSystem = gameObject.AddComponent<FuelSystem>();

            if (crackIndicator == null) Debug.LogError($"{nameof(MiningAutomaton)} on {name} is missing its crackIndicator reference.");

            bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null) Debug.LogError($"{nameof(MiningAutomaton)} on {name} is missing its body SpriteRenderer.");
            hatRenderer = CreateHatRenderer();
        }

        private void Start()
        {
            oreInventory.Initialize(() => config.AutomatonBaseInventoryWeight * upgrades.Automation_AutomatonInventoryCapacityMultiplier);
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
            GameManager.EventService.Add<AutomatonHatsChangedEvent>(RefreshHat);
            RefreshHat();
        }
        // HasInstance guard: teardown order across objects isn't guaranteed when Stopping the
        // Player, so OreCarrierRegistry's singleton may already be destroyed by the time this runs.
        // (Instance would resurrect it as a stray GameObject mid-unload - HasInstance doesn't.)
        private void OnDisable()
        {
            if (OreCarrierRegistry.HasInstance) OreCarrierRegistry.Instance.Unregister(this);
            if (FuelConsumerRegistry.HasInstance) FuelConsumerRegistry.Instance.Unregister(this);
            GameManager.EventService.Remove<AutomatonHatsChangedEvent>(RefreshHat);
        }

        // Critter Shop hat (CritterCollection.GetAutomatonHat, keyed by DisplayIndex) - a child
        // sprite sat on top of the body, one sorting order above it, following its flip.
        private SpriteRenderer CreateHatRenderer()
        {
            var hatObject = new GameObject("Hat");
            hatObject.transform.SetParent(transform, false);
            var renderer = hatObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + 1 : 3;
            renderer.enabled = false;
            return renderer;
        }

        private void RefreshHat()
        {
            if (hatRenderer == null || bodyRenderer == null) return;

            var hat = GameManager.CritterDatabase.GetHat(CritterCollection.Instance.GetAutomatonHat(DisplayIndex));
            hatRenderer.enabled = bodyRenderer.enabled && hat != null && hat.Sprite != null;
            currentHat = hatRenderer.enabled ? hat : null;
            if (!hatRenderer.enabled) return;

            hatRenderer.sprite = hat.Sprite;
            // Scale to the hat's authored world width, undoing this automaton's own scale.
            float lossyX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float scale = hat.Width / Mathf.Max(0.0001f, hat.Sprite.bounds.size.x) / lossyX;
            hatRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            UpdateHatPose();
        }

        // Every frame rather than once in RefreshHat: the body's animation frames bob and shake
        // inside a fixed 128px rect, so the hat re-reads the current frame's top each frame to ride
        // along. AutomatonAnimation runs its LateUpdate first (DefaultExecutionOrder) so this sees
        // the sprite for this frame, not last frame's.
        private void LateUpdate()
        {
            if (currentHat != null) UpdateHatPose();
        }

        private void UpdateHatPose()
        {
            bool flipped = bodyRenderer.flipX;
            hatRenderer.flipX = flipped;

            float lossyX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float facing = flipped ? -1f : 1f;
            float x = (headCenterX + currentHat.Offset.x / lossyX) * facing;
            float y = SpriteOpaqueBounds.Top(bodyRenderer.sprite) + currentHat.Offset.y / lossyX;
            hatRenderer.transform.localPosition = new Vector3(x, y, 0f);
        }

        private void Update()
        {
            streamingManager.SetFocusDepth(gameObject.name, transform.position.y);
            IsMining = false;
            if (heldForDeployment) return;

            // Empty tank: abandon whatever it was doing and head for the Control Center to buy more,
            // rather than stalling in place waiting for a Fuel Drone to happen by. Consume() no-ops at
            // 0 fuel, so the trip home costs nothing further - it's running on fumes.
            if (fuelSystem.IsEmpty && state != State.ReturningToRefuel)
            {
                BeginTripToDepot(State.ReturningToRefuel);
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
                case State.LeavingDepot:
                    UpdateLeavingDepot();
                    break;
            }

            RefreshCurrentCell();
        }

        private void UpdatePickingTarget()
        {
            if (oreInventory.IsFull)
            {
                BeginTripToDepot(State.FlyingToDepot);
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
                // means the only unmined ground left is along the sky lane past a building's
                // unmineable footing, or the current layer is fully mined out and the only way onward is
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
            // BuildWorldPath ends on the dig target itself - drop it so the walk stops in the open
            // cell next to the block instead of carrying on into it while mining.
            if (path.Count > 1) path.RemoveAt(path.Count - 1);
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

            float speed = config.AutomatonBaseMoveSpeed * upgrades.Automation_AutomatonMoveSpeedMultiplier;
            // The final waypoint is the open cell beside the dig target - only start mining once
            // standing there, so the automaton drills the block from next to it.
            if (!mover.StepAlongPath(transform, path, ref pathIndex, speed, cornerRadius: config.AutomatonCornerRadius))
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

            float miningSpeed = config.AutomatonBaseMiningSpeed * upgrades.Automation_AutomatonMiningSpeedMultiplier;
            miningProgress += Time.deltaTime * miningSpeed;
            fuelSystem.ConsumeMining(Time.deltaTime);
            IsMining = true;
            MiningTargetPosition = mapGenerationService.CellToWorldCenter(digTargetLayer, digTargetCell.x, digTargetCell.y);
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
                float moveSpeed = config.AutomatonBaseMoveSpeed * upgrades.Automation_AutomatonMoveSpeedMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, mapGenerationService.CellToWorldCenter(layer, x, y), moveSpeed * Time.deltaTime);
                return;
            }

            float miningSpeed = config.AutomatonBaseMiningSpeed * upgrades.Automation_AutomatonMiningSpeedMultiplier;
            miningProgress += Time.deltaTime * miningSpeed;
            fuelSystem.ConsumeMining(Time.deltaTime);
            IsMining = true;
            MiningTargetPosition = mapGenerationService.CellToWorldCenter(layer, x, y);
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

        // GameDesignDoc Control Center "increase mining radius by 1 (max 2)": vein mining, same as
        // the player's Mining_AreaSize upgrade (PlayerMining.MineAreaBonusCells) - only triggers
        // off mining an Ore block, then chains into connected Ore blocks for free, one more per
        // level. Only ore of the same type as the mined block chains, unless the Chain Vein Mining
        // prestige perk is owned.
        private void MineTargetAndBonusCells(int layer, Vector2Int primaryCell, BlockType primaryBlockType)
        {
            if (mapGenerationService.MineCell(layer, primaryCell.x, primaryCell.y))
            {
                CollectMinedBlock(primaryBlockType, layer, primaryCell);
            }

            if (primaryBlockType.Category != BlockCategory.Ore) return;

            int chainLevel = upgrades.Automation_AutomatonMiningRadiusBonus;
            if (chainLevel <= 0) return;

            bool anyOre = PrestigeUpgradeManager.Instance.Mining_ChainVeinMiningUnlocked;
            foreach (var cell in VeinMiningPattern.GetChainCells(mapGenerationService, layer, primaryCell.x, primaryCell.y, chainLevel, primaryBlockType, anyOre))
            {
                var bonusBlock = mapGenerationService.GetBlockTypeAt(layer, cell.x, cell.y);
                if (bonusBlock == null) continue;
                if (oreInventory.IsFull) continue;

                if (!mapGenerationService.MineCell(layer, cell.x, cell.y)) continue;
                CollectMinedBlock(bonusBlock, layer, cell);
            }
        }

        private void CollectMinedBlock(BlockType blockType, int layer, Vector2Int cell)
        {
            if (blockType == null) return;
            if (blockType.Category == BlockCategory.Artifact)
            {
                Wallet.Instance.AddArtifact();
                RuneCollection.Instance.RecordFound(GameManager.MuseumCollectionDatabase.GetRuneAt(layer, cell.x, cell.y), byPlayer: false);
                return;
            }
            if (blockType.Category != BlockCategory.Ore) return;
            oreInventory.AddOre(blockType, GameManager.RunModifierService.OreYieldMultiplier(layer));
        }

        private void RefreshCurrentCell()
        {
            if (mapGenerationService.TryWorldToCellInBounds(transform.position, out int layer, out int x, out int y))
            {
                currentLayer = layer;
                currentCell = new Vector2Int(x, y);
            }
        }

        // The open sky cell just above the surface over the deposit point - where every trip home
        // leaves the grid and every departure rejoins it. _depotLocation itself sits a touch into
        // the building's unmineable footing (it's the doorway), so it can't be a path node.
        private Vector2Int DepotLaneCell()
        {
            mapGenerationService.TryWorldToCellInBounds(_depotLocation, out _, out int x, out _);
            return new Vector2Int(x, -1);
        }

        // Depot and refuel trips share a destination, so switching between them mid-trip (tank
        // runs dry on the way to deposit) keeps the route already being followed. The route goes
        // up through dug-out ground to the sky lane and along it to the Depot - never through
        // solid cells - then drops the last bit straight into the doorway.
        private void BeginTripToDepot(State tripState)
        {
            crackIndicator.Hide();
            bool alreadyHeadingHome = state is State.FlyingToDepot or State.ReturningToRefuel;
            state = tripState;
            if (alreadyHeadingHome) return;

            path = AutomatonReachability.BuildWorldPath(mapGenerationService, currentLayer, currentCell, 0, DepotLaneCell());
            if (path.Count == 0)
            {
                // Open cells never close back up, so the way it came in should always lead out.
                Debug.LogError($"{name} found no open route from layer {currentLayer} cell {currentCell} to the Depot - flying straight there instead.");
            }
            path.Add(_depotLocation);
            pathIndex = 0;
        }

        private bool StepTripToDepot()
        {
            float speed = config.AutomatonBaseMoveSpeed * upgrades.Automation_AutomatonMoveSpeedMultiplier;
            bool arrived = mover.StepAlongPath(transform, path, ref pathIndex, speed, cornerRadius: config.AutomatonCornerRadius);
            depotDoor.NotifyApproach(transform.position);
            return arrived;
        }

        private void UpdateFlyingToDepot()
        {
            fuelSystem.ConsumeFlying(Time.deltaTime);
            if (!StepTripToDepot()) return;

            Deposit();
            // Refueling happens at this same spot, so top up now rather than making a second trip.
            if (fuelSystem.FuelFraction < 0.5f) fuelSystem.FillFull();
            state = State.LeavingDepot;
        }

        // Back up out of the doorway onto the sky lane before picking the next dig target, so the
        // search starts from an open cell instead of the building's footing.
        private void UpdateLeavingDepot()
        {
            fuelSystem.ConsumeFlying(Time.deltaTime);

            var lane = DepotLaneCell();
            float speed = config.AutomatonBaseMoveSpeed * upgrades.Automation_AutomatonMoveSpeedMultiplier;
            if (!mover.StepDirect(transform, mapGenerationService.CellToWorldCenter(0, lane.x, lane.y), speed, arriveThreshold: 0.05f)) return;
            state = State.PickingTarget;
        }

        private void Deposit()
        {
            var withdrawn = oreInventory.WithdrawAllOre();
            AutomationDepositService.Deposit($"Automaton #{DisplayIndex}", withdrawn, transform);
        }

        // Reuses _depotLocation (the Control Center's deposit point, same spot Fuel Drones idle at)
        // rather than a separate refuel destination - there's only the one Control Center.
        private void UpdateReturningToRefuel()
        {
            if (!StepTripToDepot()) return;

            // Fuel is free, so this always fills the tank. Depositing happens at this same spot,
            // so drop off whatever it was carrying while it's here.
            fuelSystem.FillFull();
            if (oreInventory.CurrentWeight > 0f) Deposit();
            state = State.LeavingDepot;
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
