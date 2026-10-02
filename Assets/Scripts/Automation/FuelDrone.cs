using System.Linq;
using Economy;
using Events;
using Player;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Automation > Fuel Drones": flies to whichever IFuelConsumer needs fuel (the
    // player or a Mining Automaton - see FuelConsumerRegistry) and tops it off, reloading its own
    // payload for free at the Control Center (fuel costs nothing). Per the resolved design
    // decision, a drone delivers up to its full (upgradeable) capacity per visit rather than a
    // separate flat amount - "10 units" in the doc is just the level-0 base capacity.
    //
    // Targeting mirrors StorageDrone/OreCarrierRegistry: PlayerAlways only ever considers the
    // player, FullestInventory (reused here as "whoever is neediest") picks the consumer missing
    // the most fuel, falling back to nearest-with-need if the neediest one is already claimed by
    // another drone. FuelConsumerRegistry's claim system stops two drones converging on the same
    // target.
    public class FuelDrone : MonoBehaviour, ISleepableDrone
    {
        private enum State { IdleAtControlCenter, FlyingToTarget, Depositing, FlyingToControlCenter }

        private const float IdleRepollInterval = 2f;

        private static AutomationConfig config => GameManager.AutomationConfig;
        private static UpgradeManager upgrades => UpgradeManager.Instance;
        private static AutomationSettings settings => AutomationSettings.Instance;

        private readonly GridPathMover mover = new();
        private State state = State.IdleAtControlCenter;

        private Vector3 controlCenterPosition;
        private IFuelConsumer currentTarget;
        private float payload;
        private float idleRepollTimer;
        private bool isParked;

        public bool IsIdle => state == State.IdleAtControlCenter && isParked;

        public float Capacity => config.FuelDroneBaseFuelCapacity * upgrades.Automation_FuelDroneInventoryCapacityMultiplier;
        private float Speed => config.FuelDroneBaseMoveSpeed * upgrades.Automation_FuelDroneMoveSpeedMultiplier;

        // Assigned by AutomationSpawner.
        public void Configure(Vector3 controlCenterPos)
        {
            controlCenterPosition = controlCenterPos;
        }

        private void Update()
        {
            switch (state)
            {
                case State.IdleAtControlCenter: UpdateIdle(); break;
                case State.FlyingToTarget: UpdateFlyingToTarget(); break;
                case State.Depositing: UpdateDepositing(); break;
                case State.FlyingToControlCenter: UpdateFlyingToControlCenter(); break;
            }
        }

        // "they will repeat this step as long as any entity is missing at least 10% of their fuel."
        private bool NeedsFuel(IFuelConsumer consumer) => consumer.FuelMissing >= consumer.FuelMax * config.FuelNeedThresholdFraction;

        private IFuelConsumer FindTarget()
        {
            if (settings.FuelDroneTargetMode == TargetMode.PlayerAlways)
            {
                foreach (var consumer in FuelConsumerRegistry.Instance.Consumers)
                {
                    if (consumer is PlayerController && NeedsFuel(consumer)) return consumer;
                }
                return null;
            }

            return FindNeediestUnclaimedConsumer() ?? FindNearestUnclaimedConsumerNeedingFuel();
        }

        private IFuelConsumer FindNeediestUnclaimedConsumer()
        {
            IFuelConsumer best = null;
            float bestMissing = 0f;

            foreach (var consumer in FuelConsumerRegistry.Instance.Consumers)
            {
                if (FuelConsumerRegistry.Instance.IsClaimed(consumer)) continue;
                if (!NeedsFuel(consumer)) continue;
                if (consumer.FuelMissing <= bestMissing) continue;

                bestMissing = consumer.FuelMissing;
                best = consumer;
            }

            if (best != null) FuelConsumerRegistry.Instance.TryClaim(this, best);
            return best;
        }

        // Fallback used when the neediest consumer is already claimed by another drone.
        private IFuelConsumer FindNearestUnclaimedConsumerNeedingFuel()
        {
            IFuelConsumer nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var consumer in FuelConsumerRegistry.Instance.Consumers)
            {
                if (FuelConsumerRegistry.Instance.IsClaimed(consumer) || !NeedsFuel(consumer)) continue;

                float distSq = (consumer.FuelTransform.position - transform.position).sqrMagnitude;
                if (distSq >= nearestDistSq) continue;

                nearestDistSq = distSq;
                nearest = consumer;
            }

            if (nearest != null) FuelConsumerRegistry.Instance.TryClaim(this, nearest);
            return nearest;
        }

        // Guards against a target that was destroyed/unregistered mid-flight (Unregister removes
        // it from the registry's list, which survives Unity's fake-null quirk on interface refs) -
        // mirrors StorageDrone.IsValidTarget.
        private bool IsValidTarget(IFuelConsumer consumer) => consumer != null && FuelConsumerRegistry.Instance.Consumers.Contains(consumer);

        // Keeps drifting home while idle - ReleaseAndReturnToIdle can drop a drone into this state
        // mid-flight, and the spawn point isn't the Control Center anchor either.
        private void UpdateIdle()
        {
            isParked = mover.StepDirect(transform, controlCenterPosition, Speed);

            idleRepollTimer += Time.deltaTime;
            if (idleRepollTimer < IdleRepollInterval) return;
            idleRepollTimer = 0f;

            currentTarget = FindTarget();
            if (currentTarget == null) return;

            payload = Capacity;
            state = State.FlyingToTarget;
        }

        private void UpdateFlyingToTarget()
        {
            if (!IsValidTarget(currentTarget))
            {
                ReleaseAndReturnToIdle();
                return;
            }

            bool arrived = mover.StepChase(transform, currentTarget.FuelTransform, Speed, config.DroneChaseMaxSpeedMatch);
            if (arrived) state = State.Depositing;
        }

        private void UpdateDepositing()
        {
            if (IsValidTarget(currentTarget) && payload > 0f)
            {
                float amountToGive = Mathf.Min(payload, currentTarget.FuelMissing);
                if (amountToGive > 0f)
                {
                    currentTarget.AddFuel(amountToGive);
                    payload -= amountToGive;

                    // Only the player cares to be told about this - a refueled MiningAutomaton has
                    // no player-facing report (mirrors how it has no low-fuel warning either).
                    if (currentTarget is PlayerController)
                    {
                        GameManager.EventService.Dispatch(new NotificationEvent($"Fuel drone refueled you (+{amountToGive:0} fuel)", NotificationUrgency.Queued));
                    }
                }
            }

            FuelConsumerRegistry.Instance.ReleaseClaim(this);
            currentTarget = null;
            state = State.FlyingToControlCenter;
        }

        private void ReleaseAndReturnToIdle()
        {
            FuelConsumerRegistry.Instance.ReleaseClaim(this);
            currentTarget = null;
            state = State.IdleAtControlCenter;
            idleRepollTimer = IdleRepollInterval; // retry immediately
        }

        private void UpdateFlyingToControlCenter()
        {
            bool arrived = mover.StepDirect(transform, controlCenterPosition, Speed);
            if (!arrived) return;

            state = State.IdleAtControlCenter;
            idleRepollTimer = IdleRepollInterval; // re-check immediately on arrival
        }
    }
}
