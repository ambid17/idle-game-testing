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
    // one computed property per effect. Entries marked "stub" below have no live gameplay hook yet
    // because the system they'd plug into (Processing Center, digging-while-flying, passive layer
    // bonus, etc.) doesn't exist in the codebase yet - see Assets/Docs/GameDesignDoc.md "# Prestige".
    public enum PrestigeUpgradeEffect
    {
        GridWidthBonus,
        KeepDigWhileFlying, // stub
        CameraZoomBonus, // stub
        LayerSizeReduction, // stub
        MineralValueMultiplier,
        ProcessedGoodMultiplier, // stub
        // GameDesignDoc "Prestige > idle > auto miner" lists 4 kept-tier perks (count, speed, dig
        // speed, move speed) but the Market only has 3 distinct automaton stats besides count
        // (AutomatonMiningSpeed, AutomatonMiningRadius, AutomatonMoveSpeed) - mapped 1:1 onto those
        // by name below rather than guessing at the doc's "speed" vs "dig speed" wording.
        KeepAutomatonCount,
        KeepAutomatonMiningSpeed,
        KeepAutomatonMiningRadius,
        KeepAutomatonMoveSpeed,
        ArtifactSpawnRateMultiplier,
        PrestigePointsPerArtifactMultiplier,
        PassivePrestigePointRate,
        AutoPrestigeCapstone, // stub
        OreTierOddsBonus, // stub
        PowerUpEffectivenessBonus, // stub
        PowerUpSpawnRateBonus, // stub
        ShieldChargeCount, // stub
        MoveSpeedBonus, // stub
        FallDamageReduction, // stub
        GasResistance, // stub
        DoublePassiveLayerBonus, // stub
        KeepPassiveLayerBonus // stub
    }

    [CreateAssetMenu(fileName = "PrestigeUpgradeDefinition", menuName = "Economy/Prestige Upgrade Definition")]
    public class PrestigeUpgradeDefinition : ScriptableObject
    {
        [Tooltip("Must be unique across the PrestigeUpgradeDatabase.")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public PrestigeUpgradeBranch Branch;
        public PrestigeUpgradeEffect Effect;

        [Tooltip("Value added per purchased level. Meaning depends on Effect - see PrestigeUpgradeManager's accessor for this Effect.")]
        public float EffectValuePerLevel = 1f;

        [Tooltip("Number of purchasable levels. Use 1 for a one-time unlock (e.g. a capstone).")]
        public int MaxLevel = 1;

        public double BaseCost = 10;
        [Tooltip("Cost multiplier applied per level already purchased.")]
        public float CostGrowth = 1.5f;

        [Tooltip("Must be unlocked (or maxed, if Require Prerequisite Maxed) before this can be purchased. Leave empty for a branch's first tier.")]
        public PrestigeUpgradeDefinition Prerequisite;
        [Tooltip("If set, Prerequisite must be fully maxed rather than just purchased once. Used for capstones.")]
        public bool RequirePrerequisiteMaxed;

        public double GetCost(int currentLevel) => BaseCost * System.Math.Pow(CostGrowth, currentLevel);
    }
}
