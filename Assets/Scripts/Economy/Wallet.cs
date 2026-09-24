using Events;
using UnityEngine;

namespace Economy
{
    // Dollars per GameDesignDoc "Currency": earned by selling minerals/processed goods at the
    // Depot, spent at the Market for upgrades. Singleton so Depot/UI can reach it without scene wiring.
    //
    // Also banks artifacts (GameDesignDoc "# Prestige"): mining an artifact credits the Wallet
    // directly rather than being carried in PlayerInventory, so - like Dollars - it isn't lost on
    // player death. Artifacts ARE the Museum's currency (no separate Prestige Points conversion
    // step) - spent directly on PrestigeUpgradeManager purchases.
    public class Wallet : Singleton<Wallet>
    {
        [SerializeField] private double dollars;
        [SerializeField] private int artifactCount;

        // Fractional carry for PrestigeUpgradeEffect.Prestige_ArtifactValueMultiplier - each pickup
        // credits a whole number of artifacts, but the multiplier itself is often fractional
        // (e.g. 1.15x), so the remainder accumulates here instead of always rounding the same way.
        private double artifactCreditFraction;

        // Total dollars earned (via Add) since the current run started - the basis for
        // PrestigeUpgradeEffect.Prestige_GrantFunding. Reset by PrestigeManager.ExecutePrestige.
        [SerializeField] private double dollarsEarnedThisRun;

        public double Dollars => dollars;
        public int ArtifactCount => artifactCount;
        public double DollarsEarnedThisRun => dollarsEarnedThisRun;

        public void Add(double amount)
        {
            if (amount <= 0) return;
            dollars += amount;
            dollarsEarnedThisRun += amount;
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

        // Direct set for Persistence.SaveService restore and PrestigeManager's per-run reset.
        public void SetDollarsEarnedThisRun(double amount)
        {
            dollarsEarnedThisRun = amount;
        }

        // Called when mining a single artifact-ore block. Credits PrestigeUpgradeManager's applied
        // (post-prestige) Prestige_ArtifactValueMultiplier level rather than a flat 1, via
        // artifactCreditFraction so a fractional multiplier averages out correctly across pickups.
        public void AddArtifact()
        {
            var prestige = PrestigeUpgradeManager.Instance;
            float multiplier = prestige != null ? prestige.Prestige_ArtifactValueMultiplier : 1f;

            artifactCreditFraction += multiplier;
            int whole = (int)artifactCreditFraction;
            if (whole <= 0) return;

            artifactCreditFraction -= whole;
            artifactCount += whole;
            GameManager.EventService.Dispatch<ArtifactCountChangedEvent>();
        }

        // Bulk credit for PassivePrestigeIncomeTicker's passive trickle and dev-panel cheats -
        // bypasses the per-pickup value multiplier above, which only applies to mining an artifact
        // block directly.
        public void AddArtifacts(int amount)
        {
            if (amount <= 0) return;
            artifactCount += amount;
            GameManager.EventService.Dispatch<ArtifactCountChangedEvent>();
        }

        // Museum currency spend - artifacts are the currency directly, no conversion step.
        public bool TrySpendArtifacts(int amount)
        {
            if (amount <= 0 || amount > artifactCount) return false;
            artifactCount -= amount;
            GameManager.EventService.Dispatch<ArtifactCountChangedEvent>();
            return true;
        }

        // Direct set for Persistence.SaveService restoring a save file, mirrors SetDollars.
        public void SetArtifactCount(int amount)
        {
            artifactCount = Mathf.Max(0, amount);
            GameManager.EventService.Dispatch<ArtifactCountChangedEvent>();
        }
    }
}
