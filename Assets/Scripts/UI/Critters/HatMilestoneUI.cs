using Critters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One automaton-hat milestone in CritterShopUI's hat track: the hat, its species threshold,
    // and whether it's unlocked yet.
    public class HatMilestoneUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text requirementLabel;
        [Tooltip("Shown over the icon while the hat is still locked (e.g. a padlock).")]
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private Color lockedTint = new(0.25f, 0.25f, 0.25f, 1f);

        private HatDefinition hat;

        private void Awake()
        {
            if (iconImage == null) Debug.LogError("HatMilestoneUI.iconImage is not assigned.");
            if (nameLabel == null) Debug.LogError("HatMilestoneUI.nameLabel is not assigned.");
            if (requirementLabel == null) Debug.LogError("HatMilestoneUI.requirementLabel is not assigned.");
            if (lockedOverlay == null) Debug.LogError("HatMilestoneUI.lockedOverlay is not assigned.");
        }

        public void Bind(HatDefinition definition)
        {
            hat = definition;
            iconImage.sprite = definition.Sprite;
            iconImage.preserveAspect = true;
            Refresh();
        }

        public void Refresh()
        {
            bool unlocked = CritterCollection.Instance.IsHatUnlocked(hat);
            iconImage.color = unlocked ? Color.white : lockedTint;
            lockedOverlay.SetActive(!unlocked);
            nameLabel.text = unlocked ? hat.DisplayName : "Locked";
            requirementLabel.text = hat.UnlockAtSpeciesCount == 1 ? "1 species" : $"{hat.UnlockAtSpeciesCount} species";
        }
    }
}
