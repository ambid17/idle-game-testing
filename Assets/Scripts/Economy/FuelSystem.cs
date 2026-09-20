using System;
using UnityEngine;

namespace Economy
{
    // Generic fuel tank extracted so Player.PlayerController and Automation.MiningAutomaton can
    // both burn fuel without duplicating the bookkeeping - mirrors how Economy.OreInventory backs
    // both PlayerInventory and MiningAutomaton's carrying capacity. Unlike OreInventory though, all
    // the tuning numbers (capacity, drain rates) live directly on this component as serialized
    // fields rather than being injected - each owner (Player GameObject, MiningAutomaton.prefab)
    // gets its own FuelSystem instance with its own tuned values in the Inspector, one place to
    // look for every fuel number instead of them being scattered across PlayerController/
    // PlayerMining/AutomationConfig.
    public class FuelSystem : MonoBehaviour
    {
        [SerializeField] private float baseMaxFuel = 100f;
        // Drains constantly regardless of activity - there's no passive regen, fuel only ever goes
        // back up via AddFuel (Fuel Drones / ResourceRefillUI purchases).
        [SerializeField] private float idleDrainPerSecond = 1f;
        [SerializeField] private float flyingDrainPerSecond = 5f;
        [SerializeField] private float miningDrainPerSecond = 3f;

        // Owner-injected formulas for things that genuinely vary per entity/live state rather than
        // being static config - e.g. Player's Movement_FuelInventory/Movement_FuelEfficiency
        // upgrades. Null for entities with no such bonuses (MiningAutomaton).
        private Func<float> maxFuelBonusProvider;
        private Func<float> drainEfficiencyMultiplierProvider;

        public float Fuel { get; private set; }
        public float MaxFuel => baseMaxFuel + (maxFuelBonusProvider != null ? maxFuelBonusProvider() : 0f);
        public float FuelFraction => MaxFuel > 0f ? Fuel / MaxFuel : 0f;
        public float FuelMissing => Mathf.Max(0f, MaxFuel - Fuel);
        public bool IsEmpty => Fuel <= 0f;

        // Starts full. Bonus/efficiency providers are optional - omit them for entities with no
        // upgrade-driven modifiers (see MiningAutomaton.Start).
        public void Initialize(Func<float> maxFuelBonusProvider = null, Func<float> drainEfficiencyMultiplierProvider = null)
        {
            this.maxFuelBonusProvider = maxFuelBonusProvider;
            this.drainEfficiencyMultiplierProvider = drainEfficiencyMultiplierProvider;
            Fuel = MaxFuel;
        }

        // Call exactly once per frame regardless of activity.
        public void ConsumeIdle(float dt) => Consume(idleDrainPerSecond * dt);

        // Flying/mining drain stack on top of idle drain rather than replacing it - a player can
        // mine while flying with the Prestige "keep dig while flying" perk, so both can be active
        // in the same frame.
        public void ConsumeFlying(float dt) => Consume(flyingDrainPerSecond * dt);
        public void ConsumeMining(float dt) => Consume(miningDrainPerSecond * dt);

        private void Consume(float amount)
        {
            if (amount <= 0f || Fuel <= 0f) return;
            float efficiency = drainEfficiencyMultiplierProvider != null ? drainEfficiencyMultiplierProvider() : 1f;
            Fuel = Mathf.Max(0f, Fuel - amount * efficiency);
        }

        public void AddFuel(float amount)
        {
            if (amount <= 0f) return;
            Fuel = Mathf.Min(MaxFuel, Fuel + amount);
        }

        // Restore for SaveService/PlayerRevivedEvent.
        public void RestoreFuel(float fuel)
        {
            Fuel = Mathf.Clamp(fuel, 0f, MaxFuel);
        }

        public void FillFull()
        {
            Fuel = MaxFuel;
        }
    }
}
