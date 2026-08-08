using Economy;
using Player;
using UnityEngine;

namespace Automation
{
    // GameDesignDoc "Automation > Fuel Drones": flies to a target (today only the player has a
    // fuel meter - automatons/drones explicitly don't per the doc, so "fullest inventory"
    // targeting is a no-op until a future fuel-consuming entity exists) and tops it off, buying
    // more fuel for itself at the Control Center within the player-set spending cap. Per the
    // resolved design decision, a drone delivers up to its full (upgradeable) capacity per visit
    // rather than a separate flat amount - "10 units" in the doc is just the level-0 base capacity.
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
        private PlayerController targetPlayer;
        private float payload;
        private float idleRepollTimer;

        public float Capacity => config.FuelDroneBaseFuelCapacity * upgrades.FuelDroneInventoryCapacityMultiplier;

        // Assigned by AutomationSpawner.
        public void Configure(Vector3 controlCenterPos, PlayerController player)
        {
            controlCenterPosition = controlCenterPos;
            targetPlayer = player;
            if (targetPlayer == null) Debug.LogError($"{nameof(FuelDrone)} on {name} was configured without a PlayerController target.");
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
        private bool AnyoneNeedsFuel() => targetPlayer != null && targetPlayer.FuelMissing >= targetPlayer.FuelMax * config.FuelNeedThresholdFraction;

        private void UpdateIdle()
        {
            idleRepollTimer += Time.deltaTime;
            if (idleRepollTimer < IdleRepollInterval) return;
            idleRepollTimer = 0f;

            if (!AnyoneNeedsFuel()) return;

            RefuelSelfWithinSpendingCap();
            if (payload <= 0f) return; // couldn't afford any fuel within the cap - stay idle and retry later

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
            if (targetPlayer == null)
            {
                state = State.FlyingToControlCenter;
                return;
            }

            float speed = config.FuelDroneBaseMoveSpeed * upgrades.FuelDroneMoveSpeedMultiplier;
            bool arrived = mover.StepDirect(transform, targetPlayer.transform.position, speed);
            if (arrived) state = State.Depositing;
        }

        private void UpdateDepositing()
        {
            if (targetPlayer != null && payload > 0f)
            {
                float amountToGive = Mathf.Min(payload, targetPlayer.FuelMissing);
                if (amountToGive > 0f)
                {
                    targetPlayer.AddFuel(amountToGive);
                    payload -= amountToGive;
                }
            }

            // Only the player consumes fuel today, so there's never another needy target to chain
            // remaining payload to - head back regardless of what's left in the tank.
            state = State.FlyingToControlCenter;
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
