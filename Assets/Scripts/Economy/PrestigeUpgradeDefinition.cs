using UnityEngine;

namespace Economy
{
    // Category grouping per GameDesignDoc "# Prestige" - the Museum's perk tree, parallel to but
    // distinct from the Market's UpgradeBranch (these are permanent, bought with Prestige points,
    // and survive every future hard reset).
    public enum PrestigeUpgradeBranch
    {
        Mining,
        Economy,
        Idle,
        Prestige,
        // Was Progression (its perks moved to Economy) - kept at the same position since the
        // branch is serialized by index on every asset.
        Hazard,
        Survival
    }

    // What purchasing a level of this prestige perk actually does. PrestigeUpgradeManager exposes
    // one computed property per effect - see Assets/Docs/GameDesignDoc.md "# Prestige". Values are
    // explicit and are what's serialized into the PrestigeUpgradeDefinition .asset files - reorder
    // or remove members freely, but never change an existing member's number or reuse a retired
    // one. New members take the next free number in their prefix's range. Members that moved
    // branches (renamed prefix) keep their original number, so a prefix's range only describes
    // where *new* members go.
    // PrestigeUpgradeDatabase.Validate() flags any asset whose name doesn't match its Effect.
    public enum PrestigeUpgradeEffect
    {
        // Mining 100-199
        Mining_GridWidthBonus = 100,
        Mining_DigWhileFlyingUnlocked = 101,
        // 102 - was used For camera zoom,
        Mining_LayerSizeReduction = 103,
        // GameDesignDoc "Prestige > Mining > true sight": reveals all fog of war.
        Mining_TrueSight = 104,
        // Lets the player mine upward (W) into the block directly above them.
        Mining_DigUpUnlock = 105,
        // Lets the player mine FallingRock blocks directly instead of only knocking them loose by
        // mining their support (PrestigeUpgradeManager.Mining_CanMineRocks).
        Mining_RockBreaker = 106,

        // Economy 200-299
        Economy_MineralValueMultiplier = 200,
        Economy_ProcessedGoodMultiplier = 201,
        Economy_PassiveLayerBonus = 202,
        // 203 - was used for keeping the passive layer bonus between prestiges
        // Moved from the retired Progression branch - original 500-range numbers kept.
        Economy_OreTierOddsBonus = 500,
        Economy_PowerUpEffectivenessBonus = 501,
        Economy_PowerUpSpawnRateBonus = 502,

        // Idle 300-399
        // GameDesignDoc "Prestige > idle > auto miner" lists 4 kept-tier perks (count, speed, dig
        // speed, move speed) but the Market only has 3 distinct automaton stats besides count
        // (AutomatonMiningSpeed, AutomatonMiningRadius, AutomatonMoveSpeed) - mapped 1:1 onto those
        // by name below rather than guessing at the doc's "speed" vs "dig speed" wording.
        Idle_KeepAutomatonCount = 300,
        Idle_KeepAutomatonMiningSpeed = 301,
        Idle_KeepAutomatonMiningRadius = 302,
        Idle_KeepAutomatonMoveSpeed = 303,

        // Prestige 400-499
        Prestige_ArtifactSpawnRateMultiplier = 400,
        // Multiplies how many artifacts a single artifact-ore grants on mining (formerly a
        // Prestige Points per artifact multiplier, before Prestige Points were removed).
        Prestige_ArtifactValueMultiplier = 401,
        // Passive artifact (currency) trickle - formerly a Prestige Points trickle.
        Prestige_PassiveArtifactRate = 402,
        // 403 retired (was Prestige_AutoPrestigeCapstone, never implemented) - do not reuse.
        // Each held (unspent) artifact adds a % bonus to all mineral/processed-good sale value.
        Prestige_MuseumDividends = 404,
        // Each new run starts with a % of the dollars earned during the previous run.
        Prestige_GrantFunding = 405,
        // Each prestige ever completed adds a permanent, stacking % bonus to all sale value.
        Prestige_Legacy = 406,
        // Run modifiers (pick 1 of 3 at prestige, see RunModifiers): each level allows one reroll
        // of the offered modifiers per prestige.
        Prestige_RunModifierReroll = 407,
        // Run modifiers: one extra modifier offered at prestige (4 instead of 3).
        Prestige_RunModifierWiderSelection = 408,
        // Run modifiers: the current run's modifier is always among the next prestige's offers.
        Prestige_RunModifierHeirloom = 409,

        // 500-599 - retired Progression range (its members now live in Economy above) - do not reuse.

        // Survival 600-699
        Survival_ShieldChargeCount = 600,
        Survival_MoveSpeedBonus = 601,
        Survival_FallDamageReduction = 602,
        // 603-606 moved to Hazard below.
        // Active ability: teleports the player to the Depot (PlayerDepotRecall, Q by default).
        // First level unlocks it; each further level halves the cooldown.
        Survival_DepotRecall = 607,

        // Hazard 700-799
        // Active ability: scans the block in front of the player (Player.PlayerAnalyzer) and
        // explains hazards/power-ups, jokes about ores (with their value), or shares artifact lore.
        Hazard_Analyzer = 700,
        // Moved from Survival - original 600-range numbers kept.
        Hazard_GasResistance = 603,
        Hazard_BlastResistance = 604,
        Hazard_FallingRockResistance = 605,
        Hazard_LavaResistance = 606,
    }

    [CreateAssetMenu(fileName = "PrestigeUpgradeDefinition", menuName = "Economy/Prestige Upgrade Definition")]
    public class PrestigeUpgradeDefinition : UpgradeDefinitionBase
    {
        public PrestigeUpgradeBranch Branch;
        public PrestigeUpgradeEffect Effect;

        // Prestige perks default to a cheaper base cost / steeper growth curve than Market
        // upgrades (UpgradeDefinitionBase's defaults) - only applies to newly created assets.
        private void Reset()
        {
            BaseCost = 10;
            CostGrowth = 1.5f;
        }
    }
}
