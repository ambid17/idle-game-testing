using System.Collections.Generic;
using Economy;

namespace UI.SkillTree
{
    // Adapts one upgrade family (Market's UpgradeDefinition/UpgradeManager or Museum's
    // PrestigeUpgradeDefinition/PrestigeUpgradeManager) into the shape SkillTreePanelUI needs,
    // so the panel/layout/prefabs stay generic over which family they're displaying.
    public enum SkillTreeType
    {
        Upgrades,
        PrestigeUpgrades
    }
    public interface ISkillTreeSource
    {
        int BranchCount { get; }
        SkillTreeType SkillTreeType { get; }
        IReadOnlyList<SkillTreeNodeViewModel> BuildViewModels();
        void RequestPurchase(UpgradeDefinitionBase definition);

        // Live snapshot for SkillTreeDetailModalUI, computed fresh from the owning UpgradeManager
        // on every call instead of being cached on a SkillTreeNodeViewModel - the modal never
        // holds onto stale data between opens.
        SkillTreeNodeDetails GetDetails(UpgradeDefinitionBase definition);
    }
}
