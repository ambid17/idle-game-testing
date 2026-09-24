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
        private Vector3 _depositLocation;
        private float idleRepollTimer;

        public int DisplayIndex { get; private set; } = 1;

        // Assigned by AutomationSpawner - control center position is the idle/refuel anchor,
        // displayIndex feeds notification text ("Storage Drone #2").
        public void Configure(Vector3 depotDepositLocation, int displayIndex)
        {
            _depositLocation = depotDepositLocation;
            DisplayIndex = displayIndex;
        }

        private void Awake()
        {
            oreInventory = GetComponent<OreInventory>();
            if (oreInventory == null) Debug.LogError($"{nameof(StorageDrone)} on {name} is missing its required OreInventory component.");
        }

        private void Start()
        {
            oreInventory.Initialize(() => config.StorageDroneBaseInventoryWeight * upgrades.Automation_StorageDroneInventoryCapacityMultiplier);
        }

        // HasInstance guard: teardown order across objects isn't guaranteed when Stopping the
        // Player, so OreCarrierRegistry's singleton may already be destroyed by the time this runs.
        // (Instance would resurrect it as a stray GameObject mid-unload - HasInstance doesn't.)
        private void OnDisable()
        {
            if (OreCarrierRegistry.HasInstance) OreCarrierRegistry.Instance.ReleaseClaim(this);
        }

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

            float speed = config.StorageDroneBaseMoveSpeed * upgrades.Automation_StorageDroneMoveSpeedMultiplier;
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

            if (currentTarget is PlayerInventory)
            {
                GameManager.EventService.Dispatch<InventoryChangedEvent>();
            }

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
            float speed = config.StorageDroneBaseMoveSpeed * upgrades.Automation_StorageDroneMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, _depositLocation, speed);
            if (!arrived) return;

            Deposit();
            state = State.SelectingTarget;
        }

        private void Deposit()
        {
            var withdrawn = oreInventory.WithdrawAllOre();
            AutomationDepositService.Deposit($"Storage Drone #{DisplayIndex}", withdrawn);

            // GameDesignDoc "Automation > Drone delivery > Market Sense" capstone. Sells exactly
            // what this delivery just added (fraction of the now-current total), not any ore
            // already banked, which might be reserved for a Processing Center recipe. Unlocking
            // the capstone only makes auto-sell available - the Control Center toggle
            // (AutomationSettings.StorageDroneDepositMode) decides whether it's actually used.
            if (upgrades.Automation_StorageDroneAutoSellUnlocked && settings.StorageDroneDepositMode == StorageDroneDepositMode.AutoSell)
            {
                foreach (var kvp in withdrawn)
                {
                    if (kvp.Value <= 0) continue;
                    if (!Depot.Instance.StoredOres.TryGetValue(kvp.Key, out var currentStored) || currentStored <= 0) continue;
                    Depot.Instance.Sell(kvp.Key, (float)kvp.Value / currentStored);
                }
            }
        }

        // "storage drones will repeat this process as long as there is an entity with minerals in
        // their inventory" - poll periodically rather than reacting to every InventoryChangedEvent,
        // simpler and avoids event-storm coupling across every carrier.
        private void UpdateIdle()
        {
            float speed = config.StorageDroneBaseMoveSpeed * upgrades.Automation_StorageDroneMoveSpeedMultiplier;
            mover.StepDirect(transform, _depositLocation, speed);

            idleRepollTimer += Time.deltaTime;
            if (idleRepollTimer >= IdleRepollInterval)
            {
                state = State.SelectingTarget;
            }
        }
    }
}
