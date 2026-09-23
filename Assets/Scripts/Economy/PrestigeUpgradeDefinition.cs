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
        Progression,
        Survival
    }

    // What purchasing a level of this prestige perk actually does. PrestigeUpgradeManager exposes
    // one computed property per effect - see Assets/Docs/GameDesignDoc.md "# Prestige". Values are
    // explicit and are what's serialized into the PrestigeUpgradeDefinition .asset files - reorder
    // or remove members freely, but never change an existing member's number or reuse a retired
    // one. New members take the next free number in their prefix's range.
    // PrestigeUpgradeDatabase.Validate() flags any asset whose name doesn't match its Effect.
    public enum PrestigeUpgradeEffect
    {
        // Mining 100-199
        Mining_GridWidthBonus = 100,
        Mining_KeepDigWhileFlying = 101,
        // 102 - was used For camera zoom,
        Mining_LayerSizeReduction = 103,
        // GameDesignDoc "Prestige > Mining > true sight": reveals all fog of war.
        Mining_TrueSight = 104,

        // Economy 200-299
        Economy_MineralValueMultiplier = 200,
        Economy_ProcessedGoodMultiplier = 201,
        Economy_PassiveLayerBonus = 202,
        // 203 - was used for keeping the passive layer bonus between prestiges

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

        // Progression 500-599
        Progression_OreTierOddsBonus = 500,
        Progression_PowerUpEffectivenessBonus = 501,
        Progression_PowerUpSpawnRateBonus = 502,

        // Survival 600-699
        Survival_ShieldChargeCount = 600,
        Survival_MoveSpeedBonus = 601,
        Survival_FallDamageReduction = 602,
        Survival_GasResistance = 603,
        Survival_BlastResistance = 604,
        Survival_FallingRockResistance = 605,
        Survival_LavaResistance = 606,
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
