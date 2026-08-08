using Events;
using UnityEngine;

namespace Economy
{
    // Prestige points per GameDesignDoc "Currency": earned by turning artifacts in at the Museum,
    // spent at the Museum on permanent PrestigeUpgradeDefinition perks. Deliberately its own
    // singleton (not a Wallet mode) since, unlike Dollars, it must never be touched by
    // PrestigeManager.ExecutePrestige's hard reset.
    public class PrestigePoints : Singleton<PrestigePoints>
    {
        [SerializeField] private double points;

        public double Points => points;

        public void Add(double amount)
        {
            if (amount <= 0) return;
            points += amount;
            GameManager.EventService.Dispatch<PrestigePointsChangedEvent>();
        }

        public bool TrySpend(double amount)
        {
            if (amount <= 0 || amount > points) return false;
            points -= amount;
            GameManager.EventService.Dispatch<PrestigePointsChangedEvent>();
            return true;
        }

        // Direct set for Persistence.SaveService restoring a save file - distinct semantics from
        // Add/TrySpend (no add/subtract, no positive-amount requirement).
        public void SetPoints(double amount)
        {
            points = amount;
            GameManager.EventService.Dispatch<PrestigePointsChangedEvent>();
        }
    }
}
