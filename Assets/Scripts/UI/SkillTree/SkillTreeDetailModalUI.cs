using Economy;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // Detail popup for a clicked skill tree node: name/description/level/cost + a buy button that
    // routes into the same purchase pipeline the classic tabbed UI already uses (via
    // ISkillTreeSource.RequestPurchase -> PurchaseRequestedEvent/PrestigePurchaseRequestedEvent).
    // This panel never purchases anything itself. Inherits ModalBase so Escape can close just
    // this modal before it closes the Market/Museum panel underneath it.
    public class SkillTreeDetailModalUI : ModalBase
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button closeButton;

        private SkillTreeNodeViewModel current;
        private ISkillTreeSource source;

        // Lets SkillTreePanelUI find and rebind the same node's freshly-rebuilt view model after
        // a purchase, so the open modal reflects the new level/cost instead of a stale snapshot.
        public UpgradeDefinitionBase CurrentSource => current?.UpgradeDefinition;

        private void Awake()
        {
            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (root != null) root.SetActive(false);
        }

        public void Initialize(ISkillTreeSource source) => this.source = source;

        public void Show(SkillTreeNodeViewModel viewModel)
        {
            current = viewModel;
            if (root != null) root.SetActive(true);
            Opened();
            Refresh();
        }

        public void Refresh()
        {
            if (current == null || root == null || !root.activeSelf) return;

            if (nameLabel != null) nameLabel.text = current.DisplayName;
            if (descriptionLabel != null) descriptionLabel.text = current.Description;
            if (levelLabel != null) levelLabel.text = $"{current.Level}/{current.MaxLevel}";
            if (costLabel != null) costLabel.text = current.CostLabel;
            if (buyButton != null) buyButton.interactable = current.CanPurchase;
        }

        public override void Close()
        {
            current = null;
            if (root != null) root.SetActive(false);
            Closed();
        }

        private void OnBuyClicked()
        {
            if (current == null || source == null) return;
            source.RequestPurchase(current);
        }
    }
}
