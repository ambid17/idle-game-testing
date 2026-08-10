using System.Collections.Generic;

namespace UI.SkillTree
{
    // Adapts one upgrade family (Market's UpgradeDefinition/UpgradeManager or Museum's
    // PrestigeUpgradeDefinition/PrestigeUpgradeManager) into the shape SkillTreePanelUI needs,
    // so the panel/layout/prefabs stay generic over which family they're displaying.
    public interface ISkillTreeSource
    {
        int BranchCount { get; }
        IReadOnlyList<SkillTreeNodeViewModel> BuildViewModels();
        void RequestPurchase(SkillTreeNodeViewModel node);
    }
}
