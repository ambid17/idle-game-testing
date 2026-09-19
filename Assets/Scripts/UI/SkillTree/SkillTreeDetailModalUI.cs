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
        [SerializeField] private TMP_Text purchaseBlockReasonLabel;

        // Just the definition identity - never a cached snapshot of its level/cost/affordability.
        // Refresh() re-queries source.GetDetails(current) live every time, so this can't go stale.
        private UpgradeDefinitionBase upgradeDefinition;
        private ISkillTreeSource skillTreeSource;

        private void Awake()
        {
            buyButton.onClick.AddListener(OnBuyClicked);
            closeButton.onClick.AddListener(Close);
            root.SetActive(false);
        }

        private void Start()
        {
            CheckNullRefs();
        }

        private void CheckNullRefs()
        {
            if (root == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(root)} is not assigned in the inspector.");
            if (nameLabel == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(nameLabel)} is not assigned in the inspector.");
            if (descriptionLabel == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(descriptionLabel)} is not assigned in the inspector.");
            if (levelLabel == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(levelLabel)} is not assigned in the inspector.");
            if (costLabel == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(costLabel)} is not assigned in the inspector.");
            if (buyButton == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(buyButton)} is not assigned in the inspector.");
            if (closeButton == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(closeButton)} is not assigned in the inspector.");
            if (purchaseBlockReasonLabel  == null) Debug.LogError($"{nameof(SkillTreeDetailModalUI)}.{nameof(purchaseBlockReasonLabel)} is not assigned in the inspector.");

        }

        public void Initialize(ISkillTreeSource source) => this.skillTreeSource = source;

        public void Show(UpgradeDefinitionBase definition)
        {
            upgradeDefinition = definition;
            root.SetActive(true);
            SetOpened();
            Refresh();
        }

        public void Refresh()
        {
            if (upgradeDefinition == null || skillTreeSource == null || !root.activeSelf) return;

            var details = skillTreeSource.GetDetails(upgradeDefinition);
            nameLabel.text = details.DisplayName;
            descriptionLabel.text = details.Description;
            levelLabel.text = $"{details.Level}/{details.MaxLevel}";
            costLabel.text = details.CostLabel;
            buyButton.interactable = details.CanPurchase;
            purchaseBlockReasonLabel.text = details.CanPurchase ? "" : details.PurchaseBlockedReason;
        }

        public override void Close()
        {
            upgradeDefinition = null;
            root.SetActive(false);
            SetClosed();
        }

        private void OnBuyClicked()
        {
            if (upgradeDefinition == null || skillTreeSource == null) return;
            skillTreeSource.RequestPurchase(upgradeDefinition);
        }
    }
}
