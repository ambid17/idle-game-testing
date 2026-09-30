using Events;
using MapGeneration;
using System.Collections.Generic;
using Player;
using RunModifiers;
using UnityEngine;

namespace Economy
{
    // Orchestrates GameDesignDoc "# Prestige": a manual, irreversible hard reset of Dollars, Market
    // upgrade levels, Depot materials, and carried ore, followed by a fresh map generation - with
    // only PrestigeUpgradeManager's permanent perks (and their derived baselines/grid width)
    // surviving. Two-step by design (RequestPrestige -> UI confirmation -> ExecutePrestige) so a
    // single misclick can't trigger it; MuseumUI owns the actual confirmation sub-panel.
    //
    // Between confirmation and execution the player picks the next run's modifier from
    // PendingOffers (see RunModifiers). The next seed - and so the offers - are a pure function of
    // the current world, so reopening the prestige screen or reloading can't fish for new offers;
    // only RerollOffers (limited by the Museum perk) changes them.
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

        private MineWorld World => mapGenerationService.World;

        public int NextSeed => unchecked((int)MapRng.HashCell(World.Seed, prestigeCount, 0, 0, 0x9E57));

        public int RerollsRemaining => Mathf.Max(0, PrestigeUpgradeManager.Instance.Prestige_RunModifierRerolls - World.RunModifier.OfferRerollsUsed);

        // The run modifiers offered for the next run - one of these is passed to ExecutePrestige.
        public List<RunModifierState> PendingOffers()
        {
            string heirloomId = PrestigeUpgradeManager.Instance.Prestige_RunModifierHeirloomUnlocked ? World.RunModifier.ModifierId : null;
            return RunModifierOfferRoller.Roll(GameManager.RunModifierDatabase, GameManager.LayerConfigProvider, NextSeed,
                World.RunModifier.OfferRerollsUsed, PrestigeUpgradeManager.Instance.Prestige_RunModifierOfferCount, heirloomId);
        }

        public bool TryRerollOffers()
        {
            if (RerollsRemaining <= 0) return false;
            World.RunModifier.OfferRerollsUsed++;
            return true;
        }

        // Only ever called after the player has explicitly confirmed (MuseumUI's confirm sub-panel)
        // and picked one of PendingOffers.
        public void ExecutePrestige(RunModifierState chosenModifier)
        {
            // Read before prestigeCount++ below moves NextSeed on - must match the seed the offers were rolled from.
            int newSeed = NextSeed;

            // Must run first: every PrestigeUpgradeManager accessor below (GridWidthBonus etc.)
            // reads applied levels only, so anything the player queued in the Museum this run has to
            // be committed before the map regenerates against it.
            PrestigeUpgradeManager.Instance.CommitQueuedUpgrades();

            // Grant Funding reads the just-committed level, so a perk queued this run already pays
            // out on this prestige. Computed before the dollar reset below wipes the run's totals.
            double grantFunding = Wallet.Instance.DollarsEarnedThisRun * PrestigeUpgradeManager.Instance.Prestige_GrantFundingFraction;
            prestigeCount++;

            UpgradeManager.Instance.ResetAllLevels();
            LayerBonusTracker.Instance.ResetForPrestige();
            // SetDollars rather than Add so the grant doesn't count toward the new run's earnings
            // (which would let Grant Funding compound on itself across prestiges).
            Wallet.Instance.SetDollars(grantFunding);
            Wallet.Instance.SetDollarsEarnedThisRun(0);
            Depot.Instance.ClearAll();
            if (playerInventory != null) playerInventory.ClearOreOnly();

            // Apply the grid-width perk against the un-upgraded base, not the current (already
            // widened) World.GridWidth, so the bonus never compounds across prestiges.
            int newGridWidth = mapGenerationService.BaseGridWidth + PrestigeUpgradeManager.Instance.Mining_GridWidthBonus;
            mapGenerationService.ApplyGridWidthUpgrade(newGridWidth);
            mapGenerationService.PrestigeReset(newSeed, chosenModifier != null ? chosenModifier.Clone() : null);

            // Reuses the existing revive flow (full fuel/HP refill, teleport to spawn, IsDead=false)
            // rather than a new duplicate reset path - PlayerHealth/PlayerController already do
            // exactly what a fresh prestige run needs on PlayerRevivedEvent.
            GameManager.EventService.Dispatch<PlayerRevivedEvent>();

            GameManager.EventService.Dispatch(new PrestigeCompletedEvent(newSeed));
        }
    }
}
