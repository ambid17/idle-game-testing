using System.Linq;
using Economy;
using Events;
using Player;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Automation > Storage Drones": flies (always ignoring collision) to whichever
    // IOreCarrier the player has targeted via Control Center settings, drains it into its own
    // OreInventory, and repeats until full before flying to the Depot. OreCarrierRegistry's claim
    // system stops two drones converging on the same target.
    [RequireComponent(typeof(OreInventory))]
    public class StorageDrone : MonoBehaviour
    {
        private enum State { SelectingTarget, FlyingToTarget, Draining, FlyingToDepot, IdleAtControlCenter }

        private const float IdleRepollInterval = 2f;

        private static AutomationConfig config => GameManager.AutomationConfig;
        private static UpgradeManager upgrades => UpgradeManager.Instance;
        private static AutomationSettings settings => AutomationSettings.Instance;

        private OreInventory oreInventory;
        private readonly GridPathMover mover = new();
        private State state = State.SelectingTarget;

        private IOreCarrier currentTarget;
        private Vector3 controlCenterPosition;
        private float idleRepollTimer;

        public int DisplayIndex { get; private set; } = 1;

        // Assigned by AutomationSpawner - control center position is the idle/refuel anchor,
        // displayIndex feeds notification text ("Storage Drone #2").
        public void Configure(Vector3 controlCenterPos, int displayIndex)
        {
            controlCenterPosition = controlCenterPos;
            DisplayIndex = displayIndex;
        }

        private void Awake()
        {
            oreInventory = GetComponent<OreInventory>();
            if (oreInventory == null) Debug.LogError($"{nameof(StorageDrone)} on {name} is missing its required OreInventory component.");
        }

        private void Start()
        {
            oreInventory.Initialize(() => config.StorageDroneBaseInventoryWeight * upgrades.StorageDroneInventoryCapacityMultiplier);
        }

        private void OnDisable() => OreCarrierRegistry.Instance.ReleaseClaim(this);

        private void Update()
        {
            switch (state)
            {
                case State.SelectingTarget: UpdateSelectingTarget(); break;
                case State.FlyingToTarget: UpdateFlyingToTarget(); break;
                case State.Draining: UpdateDraining(); break;
                case State.FlyingToDepot: UpdateFlyingToDepot(); break;
                case State.IdleAtControlCenter: UpdateIdle(); break;
            }
        }

        private void UpdateSelectingTarget()
        {
            currentTarget = FindTarget();
            if (currentTarget == null)
            {
                state = State.IdleAtControlCenter;
                idleRepollTimer = 0f;
                return;
            }

            state = State.FlyingToTarget;
        }

        private IOreCarrier FindTarget()
        {
            if (settings.StorageDroneTargetMode == TargetMode.PlayerAlways)
            {
                foreach (var carrier in OreCarrierRegistry.Instance.Carriers)
                {
                    if (carrier is PlayerInventory && carrier.Inventory.CurrentWeight > 0f) return carrier;
                }
                return null;
            }

            return FindFullestUnclaimedCarrier() ?? FindNearestUnclaimedCarrierWithOre();
        }

        private IOreCarrier FindFullestUnclaimedCarrier()
        {
            IOreCarrier best = null;
            float bestWeight = 0f;

            foreach (var carrier in OreCarrierRegistry.Instance.Carriers)
            {
                if (OreCarrierRegistry.Instance.IsClaimed(carrier)) continue;
                float weight = carrier.Inventory.CurrentWeight;
                if (weight <= bestWeight) continue;

                bestWeight = weight;
                best = carrier;
            }

            if (best != null) OreCarrierRegistry.Instance.TryClaim(this, best);
            return best;
        }

        // Fallback used when the previously claimed target didn't have enough to fill this drone.
        private IOreCarrier FindNearestUnclaimedCarrierWithOre()
        {
            IOreCarrier nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var carrier in OreCarrierRegistry.Instance.Carriers)
            {
                if (OreCarrierRegistry.Instance.IsClaimed(carrier) || carrier.Inventory.CurrentWeight <= 0f) continue;

                float distSq = (carrier.CarrierTransform.position - transform.position).sqrMagnitude;
                if (distSq >= nearestDistSq) continue;

                nearestDistSq = distSq;
                nearest = carrier;
            }

            if (nearest != null) OreCarrierRegistry.Instance.TryClaim(this, nearest);
            return nearest;
        }

        // Guards against a target that was destroyed/unregistered mid-flight (Unregister removes
        // it from the registry's list, which survives Unity's fake-null quirk on interface refs).
        private bool IsValidTarget(IOreCarrier carrier) => carrier != null && OreCarrierRegistry.Instance.Carriers.Contains(carrier);

        private void UpdateFlyingToTarget()
        {
            if (!IsValidTarget(currentTarget))
            {
                ReleaseAndReselect();
                return;
            }

            float speed = config.StorageDroneBaseMoveSpeed * upgrades.StorageDroneMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, currentTarget.CarrierTransform.position, speed);
            if (arrived) state = State.Draining;
        }

        private void UpdateDraining()
        {
            if (!IsValidTarget(currentTarget))
            {
                ReleaseAndReselect();
                return;
            }

            float capacityRemaining = oreInventory.MaxWeight - oreInventory.CurrentWeight;
            var drained = currentTarget.Inventory.WithdrawUpToWeight(capacityRemaining);
            foreach (var kvp in drained)
            {
                var blockType = GameManager.BlockTypeDatabase.Get((byte)kvp.Key);
                if (blockType != null) oreInventory.AddOre(blockType, kvp.Value);
            }

            OreCarrierRegistry.Instance.ReleaseClaim(this);

            // Doc: "if the entity they fly to doesn't have enough to fill their inventory, they
            // will fly to the next closest entity with items in their inventory."
            state = oreInventory.IsFull ? State.FlyingToDepot : State.SelectingTarget;
        }

        private void ReleaseAndReselect()
        {
            OreCarrierRegistry.Instance.ReleaseClaim(this);
            currentTarget = null;
            state = State.SelectingTarget;
        }

        private void UpdateFlyingToDepot()
        {
            float speed = config.StorageDroneBaseMoveSpeed * upgrades.StorageDroneMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, Depot.Instance.transform.position, speed);
            if (!arrived) return;

            Deposit();
            state = State.SelectingTarget;
        }

        private void Deposit()
        {
            var withdrawn = oreInventory.WithdrawAllOre();
            Depot.Instance.Deposit(withdrawn);
            // AutomatonIndex left at its default (-1): storage-drone deposits don't feed the
            // Control Center's per-automaton earnings graph or IdleEarningsTracker, only Mining
            // Automatons do (per the design decision on idle-earnings scope).
            GameManager.EventService.Dispatch(new OreDepositedByAutomationEvent($"Storage Drone #{DisplayIndex}", withdrawn));
        }

        // "storage drones will repeat this process as long as there is an entity with minerals in
        // their inventory" - poll periodically rather than reacting to every InventoryChangedEvent,
        // simpler and avoids event-storm coupling across every carrier.
        private void UpdateIdle()
        {
            float speed = config.StorageDroneBaseMoveSpeed * upgrades.StorageDroneMoveSpeedMultiplier;
            mover.StepDirect(transform, controlCenterPosition, speed);

            idleRepollTimer += Time.deltaTime;
            if (idleRepollTimer >= IdleRepollInterval)
            {
                state = State.SelectingTarget;
            }
        }
    }
}
