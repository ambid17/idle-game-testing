namespace UI.SkillTree
{
    // Live data for one upgrade, returned by ISkillTreeSource.GetDetails. Always computed fresh
    // from the owning UpgradeManager at the moment it's requested - never cached - so
    // SkillTreeDetailModalUI can't show a stale Buy button.
    public struct SkillTreeNodeDetails
    {
        public string DisplayName;
        public string Description;
        public int Level;
        public int MaxLevel;
        public string CostLabel;
        public bool CanPurchase;
        public string PurchaseBlockedReason;
    }
}
