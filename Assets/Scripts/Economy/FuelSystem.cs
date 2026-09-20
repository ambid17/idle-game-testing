using System;
using UnityEngine;

namespace Economy
{
    // Generic fuel tank extracted so Player.PlayerController and Automation.MiningAutomaton can
    // both burn fuel without duplicating the bookkeeping - mirrors how Economy.OreInventory backs
    // both PlayerInventory and MiningAutomaton's carrying capacity. Purely a data component (no
    // Update of its own, dispatches no events): the owner reports activity and calls Consume/AddFuel,
    // same "dumb component" convention OreInventory follows.
    public class FuelSystem : MonoBehaviour
    {
        private Func<float> maxFuelProvider;

        public float Fuel { get; private set; }
        public float MaxFuel => maxFuelProvider != null ? maxFuelProvider() : 0f;
        public float FuelFraction => MaxFuel > 0f ? Fuel / MaxFuel : 0f;
        public float FuelMissing => Mathf.Max(0f, MaxFuel - Fuel);
        public bool IsEmpty => Fuel <= 0f;

        // Owner injects its own capacity formula (base + upgrades) since that varies per entity
        // type - same delegate-injection pattern as OreInventory.Initialize. Starts full.
        public void Initialize(Func<float> maxFuelProvider)
        {
            this.maxFuelProvider = maxFuelProvider;
            Fuel = MaxFuel;
        }

        public void Consume(float amount)
        {
            if (amount <= 0f || Fuel <= 0f) return;
            Fuel = Mathf.Max(0f, Fuel - amount);
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
