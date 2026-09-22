using System.Linq;
using Economy;
using Events;
using Player;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Automation > Fuel Drones": flies to whichever IFuelConsumer needs fuel (the
    // player or a Mining Automaton - see FuelConsumerRegistry) and tops it off, buying more fuel
    // for itself at the Control Center within the player-set spending cap. Per the resolved design
    // decision, a drone delivers up to its full (upgradeable) capacity per visit rather than a
    // separate flat amount - "10 units" in the doc is just the level-0 base capacity.
    //
    // Targeting mirrors StorageDrone/OreCarrierRegistry: PlayerAlways only ever considers the
    // player, FullestInventory (reused here as "whoever is neediest") picks the consumer missing
    // the most fuel, falling back to nearest-with-need if the neediest one is already claimed by
    // another drone. FuelConsumerRegistry's claim system stops two drones converging on the same
    // target.
    public class FuelDrone : MonoBehaviour
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

        public float Capacity => config.FuelDroneBaseFuelCapacity * upgrades.FuelDroneInventoryCapacityMultiplier;

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

        private void UpdateIdle()
        {
            idleRepollTimer += Time.deltaTime;
            if (idleRepollTimer < IdleRepollInterval) return;
            idleRepollTimer = 0f;

            currentTarget = FindTarget();
            if (currentTarget == null) return;

            RefuelSelfWithinSpendingCap();
            if (payload <= 0f)
            {
                // Couldn't afford any fuel within the cap - release the claim and stay idle.
                FuelConsumerRegistry.Instance.ReleaseClaim(this);
                currentTarget = null;
                return;
            }

            state = State.FlyingToTarget;
        }

        private void RefuelSelfWithinSpendingCap()
        {
            float unitsNeeded = Capacity - payload;
            if (unitsNeeded <= 0f) return;

            double maxSpend = Wallet.Instance.Dollars * settings.FuelSpendingCapPercent;
            float unitsAffordable = (float)(maxSpend / config.FuelCostPerUnit);
            float unitsToBuy = Mathf.Min(unitsNeeded, unitsAffordable);
            if (unitsToBuy <= 0f) return;

            if (!Wallet.Instance.TrySpend(unitsToBuy * config.FuelCostPerUnit)) return;
            payload += unitsToBuy;
        }

        private void UpdateFlyingToTarget()
        {
            if (!IsValidTarget(currentTarget))
            {
                ReleaseAndReturnToIdle();
                return;
            }

            float speed = config.FuelDroneBaseMoveSpeed * upgrades.FuelDroneMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, currentTarget.FuelTransform.position, speed);
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
            float speed = config.FuelDroneBaseMoveSpeed * upgrades.FuelDroneMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, controlCenterPosition, speed);
            if (!arrived) return;

            state = State.IdleAtControlCenter;
            idleRepollTimer = IdleRepollInterval; // re-check immediately on arrival
        }
    }
}
