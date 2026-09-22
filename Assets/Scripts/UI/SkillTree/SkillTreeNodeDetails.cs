namespace UI.SkillTree
{
    // Live data for one upgrade, returned by ISkillTreeSource.GetDetails. Always computed fresh
    // from the owning UpgradeManager at the moment it's requested - never cached - so
    // SkillTreeTooltipUI can't show stale info.
    public struct SkillTreeNodeDetails
    {
        public string DisplayName;
        public string Description;
        public int Level;
        // Levels paid for but not yet applied (PrestigeUpgradeManager only). Always 0 for the
        // Market tree.
        public int QueuedLevel;
        public int MaxLevel;
        public string CostLabel;
        public bool CanPurchase;
        public string PurchaseBlockedReason;
    }
}
