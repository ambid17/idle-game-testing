using Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // Hover tooltip for a skill tree node: name/description/level/cost + why a purchase is
    // blocked, if it is. Read-only - purchasing happens by clicking the node itself
    // (SkillTreeNodeUI/SkillTreePanelUI.OnNodePurchaseClicked), which routes into the same
    // pipeline the classic tabbed UI already uses (via ISkillTreeSource.RequestPurchase ->
    // PurchaseRequestedEvent/PrestigePurchaseRequestedEvent). Not a ModalBase: it never blocks
    // input or needs an Escape close, it just follows the hovered node and hides on pointer exit.
    public class SkillTreeTooltipUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text purchaseBlockReasonLabel;
        // Just the definition identity - never a cached snapshot of its level/cost/affordability.
        // Refresh() re-queries source.GetDetails(current) live every time, so this can't go stale.
        private UpgradeDefinitionBase upgradeDefinition;
        private ISkillTreeSource skillTreeSource;
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            root.SetActive(false);
        }

        private void Start()
        {
            CheckNullRefs();
        }

        private void CheckNullRefs()
        {
            if (root == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(root)} is not assigned in the inspector.");
            if (card == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(card)} is not assigned in the inspector.");
            if (nameLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(nameLabel)} is not assigned in the inspector.");
            if (descriptionLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(descriptionLabel)} is not assigned in the inspector.");
            if (levelLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(levelLabel)} is not assigned in the inspector.");
            if (costLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(costLabel)} is not assigned in the inspector.");
            if (purchaseBlockReasonLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(purchaseBlockReasonLabel)} is not assigned in the inspector.");
            if (canvas == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}: no parent Canvas found, tooltip positioning will be inaccurate.");
        }

        public void Initialize(ISkillTreeSource source) => this.skillTreeSource = source;

        public void Show(UpgradeDefinitionBase definition, RectTransform anchor)
        {
            upgradeDefinition = definition;
            root.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            upgradeDefinition = null;
            root.SetActive(false);
        }

        public void Refresh()
        {
            if (upgradeDefinition == null || skillTreeSource == null || !root.activeSelf) return;

            var details = skillTreeSource.GetDetails(upgradeDefinition);
            nameLabel.text = details.DisplayName;
            descriptionLabel.text = details.Description;
            levelLabel.text = details.QueuedLevel > 0
                ? $"{details.Level}+{details.QueuedLevel}/{details.MaxLevel}"
                : $"{details.Level}/{details.MaxLevel}";
            costLabel.text = details.CostLabel;
            purchaseBlockReasonLabel.text = details.CanPurchase ? "" : details.PurchaseBlockedReason;
        }
    }
}
