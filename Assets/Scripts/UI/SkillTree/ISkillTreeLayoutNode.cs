using Economy;

namespace UI.SkillTree
{
    // Minimal shape SkillTreeLayout needs from a node, independent of whether it backs an
    // UpgradeDefinition or a PrestigeUpgradeDefinition.
    public interface ISkillTreeLayoutNode
    {
        // Only used to name the node in layout warnings.
        string Name { get; }
        SkillTreeDirection Direction { get; }
        ISkillTreeLayoutNode Prerequisite { get; }
    }
}
