using Critters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One species card in CritterShopUI's collection grid. Undiscovered species show a dark
    // silhouette and "???" so the player can see how many are left without spoiling them. A newly
    // discovered species shows a badge until clicked; clicking a discovered card has the shopkeeper
    // talk about it.
    public class CritterCollectionSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Button button;
        [Tooltip("Shown while the shopkeeper's quip about this newly discovered species is unheard.")]
        [SerializeField] private GameObject newBadge;
        [SerializeField] private Color silhouetteColor = new(0f, 0f, 0f, 0.85f);

        private CritterDefinition critter;

        private void Awake()
        {
            if (iconImage == null) Debug.LogError("CritterCollectionSlotUI.iconImage is not assigned.");
            if (nameLabel == null) Debug.LogError("CritterCollectionSlotUI.nameLabel is not assigned.");
            if (descriptionLabel == null) Debug.LogError("CritterCollectionSlotUI.descriptionLabel is not assigned.");
            if (countLabel == null) Debug.LogError("CritterCollectionSlotUI.countLabel is not assigned.");
            if (button == null) Debug.LogError("CritterCollectionSlotUI.button is not assigned.");
            if (newBadge == null) Debug.LogError("CritterCollectionSlotUI.newBadge is not assigned.");

            button.onClick.AddListener(() => CritterShopController.Instance.TalkAbout(critter));
        }

        public void Bind(CritterDefinition definition)
        {
            critter = definition;
            iconImage.sprite = definition.Sprite;
            iconImage.preserveAspect = true;
            Refresh();
        }

        public void Refresh()
        {
            int count = CritterCollection.Instance.GetTurnedInCount(critter.Id);
            bool discovered = count > 0;

            iconImage.color = discovered ? Color.white : silhouetteColor;
            nameLabel.text = discovered ? critter.DisplayName : "???";
            descriptionLabel.text = discovered ? critter.Description : string.Empty;
            countLabel.text = discovered ? $"x{count}" : string.Empty;
            button.interactable = discovered && !string.IsNullOrEmpty(critter.ShopkeeperQuip);
            newBadge.SetActive(CritterCollection.Instance.HasUnheardQuip(critter.Id));
        }
    }
}
