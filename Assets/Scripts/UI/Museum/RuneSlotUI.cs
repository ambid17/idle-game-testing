using Museum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One rune card in the Museum's Collection tab. Donated runes show their name and the curator's
    // translation; runes found but not yet turned in show their name and a nudge to donate; the rest
    // are dark silhouettes so the player can see how many are left without spoiling them.
    public class RuneSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Color silhouetteColor = new(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color foundColor = new(1f, 0.85f, 0.3f, 1f);

        private RuneDefinition rune;

        private void Awake()
        {
            if (iconImage == null) Debug.LogError("RuneSlotUI.iconImage is not assigned.");
            if (nameLabel == null) Debug.LogError("RuneSlotUI.nameLabel is not assigned.");
            if (descriptionLabel == null) Debug.LogError("RuneSlotUI.descriptionLabel is not assigned.");
            if (statusLabel == null) Debug.LogError("RuneSlotUI.statusLabel is not assigned.");
        }

        public void Bind(RuneDefinition definition)
        {
            rune = definition;
            iconImage.sprite = definition.Icon;
            iconImage.preserveAspect = true;
            Refresh();
        }

        public void Refresh()
        {
            var collection = RuneCollection.Instance;
            bool donated = collection.IsDonated(rune.Index);
            bool found = collection.IsFound(rune.Index);

            iconImage.color = donated || found ? Color.white : silhouetteColor;
            nameLabel.text = donated || found ? rune.DisplayName : "???";
            descriptionLabel.text = donated ? $"\"{rune.CuratorTranslation}\"" : string.Empty;
            statusLabel.text = found ? "NEW - Turn in!" : string.Empty;
            statusLabel.color = foundColor;
        }
    }
}
