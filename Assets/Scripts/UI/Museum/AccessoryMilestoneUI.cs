using Museum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One accessory milestone in the Museum's Collection tab: the accessory, its rune threshold and
    // slot, and - once unlocked - a toggle to wear or remove it (one accessory per slot).
    public class AccessoryMilestoneUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text requirementLabel;
        [Tooltip("Shown over the icon while the accessory is still locked (e.g. a padlock).")]
        [SerializeField] private GameObject lockedOverlay;
        [Tooltip("Wears/removes the accessory. Not interactable while locked.")]
        [SerializeField] private Button wearButton;
        [SerializeField] private TMP_Text wearButtonLabel;
        [SerializeField] private Color lockedTint = new(0.25f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color wornButtonColor = new(0.55f, 0.3f, 0.75f, 1f);
        [SerializeField] private Color unwornButtonColor = Color.white;

        private AccessoryDefinition accessory;

        private void Awake()
        {
            if (iconImage == null) Debug.LogError("AccessoryMilestoneUI.iconImage is not assigned.");
            if (nameLabel == null) Debug.LogError("AccessoryMilestoneUI.nameLabel is not assigned.");
            if (requirementLabel == null) Debug.LogError("AccessoryMilestoneUI.requirementLabel is not assigned.");
            if (lockedOverlay == null) Debug.LogError("AccessoryMilestoneUI.lockedOverlay is not assigned.");
            if (wearButton == null) Debug.LogError("AccessoryMilestoneUI.wearButton is not assigned.");
            if (wearButtonLabel == null) Debug.LogError("AccessoryMilestoneUI.wearButtonLabel is not assigned.");

            wearButton.onClick.AddListener(() => RuneCollection.Instance.ToggleEquipped(accessory));
        }

        public void Bind(AccessoryDefinition definition)
        {
            accessory = definition;
            iconImage.sprite = definition.Sprite;
            iconImage.preserveAspect = true;
            Refresh();
        }

        public void Refresh()
        {
            var collection = RuneCollection.Instance;
            bool unlocked = collection.IsAccessoryUnlocked(accessory);
            bool worn = collection.IsEquipped(accessory);

            iconImage.color = unlocked ? Color.white : lockedTint;
            lockedOverlay.SetActive(!unlocked);
            nameLabel.text = unlocked ? accessory.DisplayName : "Locked";
            requirementLabel.text = $"{accessory.UnlockAtRuneCount} runes - {accessory.Slot}";

            wearButton.interactable = unlocked;
            wearButtonLabel.text = !unlocked ? "-" : worn ? "Remove" : "Wear";
            wearButton.targetGraphic.color = worn ? wornButtonColor : unwornButtonColor;
        }
    }
}
