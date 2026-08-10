namespace UI.SkillTree
{
    // Minimal shape SkillTreeLayout needs from a node, independent of whether it backs an
    // UpgradeDefinition or a PrestigeUpgradeDefinition.
    public interface ISkillTreeLayoutNode
    {
        int BranchIndex { get; }
        ISkillTreeLayoutNode Prerequisite { get; }
    }
}
