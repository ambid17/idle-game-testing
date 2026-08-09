using Events;
using UnityEngine;

namespace Economy
{
    // Dollars per GameDesignDoc "Currency": earned by selling minerals/processed goods at the
    // Depot, spent at the Market for upgrades. Singleton so Depot/UI can reach it without scene wiring.
    //
    // Also banks artifacts (GameDesignDoc "# Prestige"): mining an artifact credits the Wallet
    // directly rather than being carried in PlayerInventory, so - like Dollars - it isn't lost on
    // player death and is turned in wholesale at the Museum via WithdrawAllArtifacts.
    public class Wallet : Singleton<Wallet>
    {
        [SerializeField] private double dollars;
        [SerializeField] private int artifactCount;

        public double Dollars => dollars;
        public int ArtifactCount => artifactCount;

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

        public void AddArtifact()
        {
            artifactCount++;
            GameManager.EventService.Dispatch<ArtifactCountChangedEvent>();
        }

        // Snapshots and clears the banked artifacts - called when turning them in at the Museum.
        public int WithdrawAllArtifacts()
        {
            int count = artifactCount;
            artifactCount = 0;
            GameManager.EventService.Dispatch<ArtifactCountChangedEvent>();
            return count;
        }

        // Direct set for Persistence.SaveService restoring a save file, mirrors SetDollars.
        public void SetArtifactCount(int amount)
        {
            artifactCount = Mathf.Max(0, amount);
            GameManager.EventService.Dispatch<ArtifactCountChangedEvent>();
        }
    }
}
