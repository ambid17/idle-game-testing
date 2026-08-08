using Economy;
using Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One node in the Market skill tree (DepotUI/OreRowUI's row-prefab pattern, applied to
    // UpgradeDefinition instead of BlockType). MarketUI owns purchase logic; this just displays
    // state and raises a purchase request.
    public class UpgradeNodeUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private GameObject lockedOverlay;

        private UpgradeManager upgradeManager => UpgradeManager.Instance;

        public UpgradeDefinition Definition { get; private set; }

        public void Bind(UpgradeDefinition definition)
        {
            Definition = definition;
            if (nameLabel != null) nameLabel.text = definition.DisplayName;
            if (descriptionLabel != null) descriptionLabel.text = definition.Description;
            if (purchaseButton != null) purchaseButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new PurchaseRequestedEvent(Definition)));
            Refresh();
        }

        public void Refresh()
        {
            if (Definition == null || upgradeManager == null) return;

            int level = upgradeManager.GetLevel(Definition);
            bool maxed = upgradeManager.IsMaxed(Definition);
            bool unlocked = upgradeManager.IsUnlocked(Definition);

            if (levelLabel != null) levelLabel.text = $"{level}/{Definition.MaxLevel}";
            if (costLabel != null) costLabel.text = maxed ? "MAXED" : $"${upgradeManager.GetNextCost(Definition):0.##}";
            if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
            if (purchaseButton != null) purchaseButton.interactable = !maxed && upgradeManager.CanPurchase(Definition);
        }
    }
}
