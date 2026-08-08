using Events;
using UnityEngine;

namespace Economy
{
    // Dollars per GameDesignDoc "Currency": earned by selling minerals/processed goods at the
    // Depot, spent at the Market for upgrades. Singleton so Depot/UI can reach it without scene wiring.
    public class Wallet : Singleton<Wallet>
    {
        [SerializeField] private double dollars;

        public double Dollars => dollars;

        public void Add(double amount)
        {
            if (amount <= 0) return;
            dollars += amount;
            GameManager.EventService.Dispatch<DollarsChangedEvent>();
        }

        public bool TrySpend(double amount)
        {
            if (amount <= 0 || amount > dollars) return false;
            dollars -= amount;
            GameManager.EventService.Dispatch<DollarsChangedEvent>();
            return true;
        }

        // Direct set for Persistence.SaveService restoring a save file - distinct semantics from
        // Add/TrySpend (no add/subtract, no positive-amount requirement).
        public void SetDollars(double amount)
        {
            dollars = amount;
            GameManager.EventService.Dispatch<DollarsChangedEvent>();
        }
    }
}
