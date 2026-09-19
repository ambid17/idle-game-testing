using UnityEngine;

namespace Economy
{
    // Shared shape of the Market's UpgradeDefinition and the Museum's PrestigeUpgradeDefinition -
    // two otherwise-unrelated upgrade families that both need to be handed around generically by
    // the SkillTree UI (see SkillTreeNodeViewModel.Source) without boxing to object. Branch/Effect
    // enums and Prerequisite stay on the concrete subclasses since they're family-specific types.
    public abstract class UpgradeDefinitionBase : ScriptableObject
    {
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;

        [Tooltip("Value added per purchased level. Meaning depends on Effect - see the owning manager's accessor for this Effect.")]
        public float EffectValuePerLevel = 1f;

        [Tooltip("Number of purchasable levels. Use 1 for a one-time unlock (e.g. a capstone).")]
        public int MaxLevel = 1;

        public double BaseCost = 100;
        [Tooltip("Cost multiplier applied per level already purchased.")]
        public float CostGrowth = 1.15f;

        [Tooltip("If set, Prerequisite must be fully maxed rather than just purchased once. Used for capstones.")]
        public bool RequirePrerequisiteMaxed;

        public double GetCost(int currentLevel) => BaseCost * System.Math.Pow(CostGrowth, currentLevel);
    }
}
