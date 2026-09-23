using Events;
using MapGeneration;
using Player;
using UnityEngine;

namespace Economy
{
    // Orchestrates GameDesignDoc "# Prestige": a manual, irreversible hard reset of Dollars, Market
    // upgrade levels, Depot materials, and carried ore, followed by a fresh map generation - with
    // only PrestigeUpgradeManager's permanent perks (and their derived baselines/grid width)
    // surviving. Two-step by design (RequestPrestige -> UI confirmation -> ExecutePrestige) so a
    // single misclick can't trigger it; MuseumUI owns the actual confirmation sub-panel.
    public class PrestigeManager : Singleton<PrestigeManager>
    {
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private PlayerInventory playerInventory;

        // Total prestiges ever completed - the basis for PrestigeUpgradeEffect.Prestige_Legacy.
        [SerializeField] private int prestigeCount;
        public int PrestigeCount => prestigeCount;

        // Direct set for Persistence.SaveService restoring a save file.
        public void SetPrestigeCount(int count)
        {
            prestigeCount = Mathf.Max(0, count);
        }

        protected override void Initialize()
        {
            base.Initialize();
            playerInventory = FindAnyObjectByType<PlayerInventory>();
            if (playerInventory == null) Debug.LogError("PrestigeManager: no PlayerInventory found in scene.");
        }

        // Only ever called after the player has explicitly confirmed (MuseumUI's confirm sub-panel).
        public void ExecutePrestige()
        {
            // Must run first: every PrestigeUpgradeManager accessor below (GridWidthBonus etc.)
            // reads applied levels only, so anything the player queued in the Museum this run has to
            // be committed before the map regenerates against it.
            PrestigeUpgradeManager.Instance.CommitQueuedUpgrades();

            // Grant Funding reads the just-committed level, so a perk queued this run already pays
            // out on this prestige. Computed before the dollar reset below wipes the run's totals.
            double grantFunding = Wallet.Instance.DollarsEarnedThisRun * PrestigeUpgradeManager.Instance.GrantFundingFraction;
            prestigeCount++;

            UpgradeManager.Instance.ResetAllLevels();
            LayerBonusTracker.Instance.ClearUnlessKept();
            // SetDollars rather than Add so the grant doesn't count toward the new run's earnings
            // (which would let Grant Funding compound on itself across prestiges).
            Wallet.Instance.SetDollars(grantFunding);
            Wallet.Instance.SetDollarsEarnedThisRun(0);
            Depot.Instance.ClearAll();
            if (playerInventory != null) playerInventory.ClearOreOnly();

            int newSeed = Random.Range(int.MinValue, int.MaxValue);

            // Apply the grid-width perk against the un-upgraded base, not the current (already
            // widened) World.GridWidth, so the bonus never compounds across prestiges.
            int newGridWidth = mapGenerationService.BaseGridWidth + PrestigeUpgradeManager.Instance.GridWidthBonus;
            mapGenerationService.ApplyGridWidthUpgrade(newGridWidth);
            mapGenerationService.PrestigeReset(newSeed);

            // Reuses the existing revive flow (full fuel/HP refill, teleport to spawn, IsDead=false)
            // rather than a new duplicate reset path - PlayerHealth/PlayerController already do
            // exactly what a fresh prestige run needs on PlayerRevivedEvent.
            GameManager.EventService.Dispatch<PlayerRevivedEvent>();

            GameManager.EventService.Dispatch(new PrestigeCompletedEvent(newSeed));
        }
    }
}
