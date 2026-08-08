using Economy;
using Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One node in the Museum's perk tree - mirrors UpgradeNodeUI exactly, bound to
    // PrestigeUpgradeDefinition/PrestigeUpgradeManager instead of the Market's equivalents.
    // MuseumUI owns purchase logic; this just displays state and raises a purchase request.
    public class PrestigeUpgradeNodeUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private GameObject lockedOverlay;

        public PrestigeUpgradeDefinition Definition { get; private set; }

        public void Bind(PrestigeUpgradeDefinition definition)
        {
            Definition = definition;
            if (nameLabel != null) nameLabel.text = definition.DisplayName;
            if (descriptionLabel != null) descriptionLabel.text = definition.Description;
            if (purchaseButton != null) purchaseButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new PrestigePurchaseRequestedEvent(Definition)));
        }

        public void Refresh(PrestigeUpgradeManager manager)
        {
            if (Definition == null || manager == null) return;

            int level = manager.GetLevel(Definition);
            bool maxed = manager.IsMaxed(Definition);
            bool unlocked = manager.IsUnlocked(Definition);

            if (levelLabel != null) levelLabel.text = $"{level}/{Definition.MaxLevel}";
            if (costLabel != null) costLabel.text = maxed ? "MAXED" : $"{manager.GetNextCost(Definition):0.##} pts";
            if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
            if (purchaseButton != null) purchaseButton.interactable = !maxed && manager.CanPurchase(Definition);
        }
    }
}
