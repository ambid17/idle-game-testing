using Economy;
using UnityEngine;

namespace UI.SkillTree
{
    // Shared shape SkillTreePanelUI renders, filled in by an ISkillTreeSource adapter from either
    // an UpgradeDefinition (Market) or a PrestigeUpgradeDefinition (Museum) so the tree, layout
    // and prefabs only need to know about this one type.
    public class SkillTreeNodeViewModel : ISkillTreeLayoutNode
    {
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public Sprite CurrencyIcon;
        public int BranchIndex { get; set; }
        public int Level;
        // Levels paid for but not yet applied (PrestigeUpgradeManager only - see its class comment).
        // Always 0 for the Market tree.
        public int QueuedLevel;
        public int MaxLevel;
        public string CostLabel;
        public bool IsUnlocked;
        public bool IsMaxed;
        public bool CanPurchase;
        public ISkillTreeLayoutNode Prerequisite { get; set; }

        // The underlying UpgradeDefinition/PrestigeUpgradeDefinition, shared via their common base
        // so SkillTreePanelUI can hand it back to the owning ISkillTreeSource (e.g. RequestPurchase)
        // without needing to know which concrete type it is.
        public UpgradeDefinitionBase UpgradeDefinition;
    }
}
