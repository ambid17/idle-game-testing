using System.Collections.Generic;
using Critters;
using Events;
using Interaction;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // The Critter Shop panel: the shopkeeper's greeting, the player's jar (count + payout), the
    // 16-species collection grid (undiscovered species as dark silhouettes), and the automaton hat
    // milestones. Opened by Critters.CritterShopController (after the intro conversation on a first
    // visit), not directly by the interaction event. Turn In / Talk hand back to the controller,
    // which runs the shopkeeper's dialog over this panel.
    public class CritterShopUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text greetingLabel;
        [SerializeField] private TMP_Text jarLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Button turnInButton;
        [SerializeField] private Button talkButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform collectionContainer;
        [SerializeField] private CritterCollectionSlotUI slotPrefab;
        [SerializeField] private Transform hatContainer;
        [SerializeField] private HatMilestoneUI hatMilestonePrefab;

        private readonly List<CritterCollectionSlotUI> slots = new();
        private readonly List<HatMilestoneUI> hatMilestones = new();

        public bool IsOpen => panelRoot.activeSelf;

        private void Start()
        {
            CheckNullRefs();

            turnInButton.onClick.AddListener(() => CritterShopController.Instance.TurnIn());
            talkButton.onClick.AddListener(() => CritterShopController.Instance.Chat());
            closeButton.onClick.AddListener(Close);

            foreach (var critter in GameManager.CritterDatabase.Critters)
            {
                var slot = Instantiate(slotPrefab, collectionContainer);
                slot.gameObject.name = $"Slot_{critter.Id}";
                slot.Bind(critter);
                slots.Add(slot);
            }

            foreach (var hat in GameManager.CritterDatabase.Hats)
            {
                var milestone = Instantiate(hatMilestonePrefab, hatContainer);
                milestone.gameObject.name = $"Hat_{hat.Id}";
                milestone.Bind(hat);
                hatMilestones.Add(milestone);
            }

            panelRoot.SetActive(false);
        }

        private void CheckNullRefs()
        {
            if (panelRoot == null) Debug.LogError("CritterShopUI.panelRoot is not assigned.");
            if (portraitImage == null) Debug.LogError("CritterShopUI.portraitImage is not assigned.");
            if (greetingLabel == null) Debug.LogError("CritterShopUI.greetingLabel is not assigned.");
            if (jarLabel == null) Debug.LogError("CritterShopUI.jarLabel is not assigned.");
            if (progressLabel == null) Debug.LogError("CritterShopUI.progressLabel is not assigned.");
            if (turnInButton == null) Debug.LogError("CritterShopUI.turnInButton is not assigned.");
            if (talkButton == null) Debug.LogError("CritterShopUI.talkButton is not assigned.");
            if (closeButton == null) Debug.LogError("CritterShopUI.closeButton is not assigned.");
            if (collectionContainer == null) Debug.LogError("CritterShopUI.collectionContainer is not assigned.");
            if (slotPrefab == null) Debug.LogError("CritterShopUI.slotPrefab is not assigned.");
            if (hatContainer == null) Debug.LogError("CritterShopUI.hatContainer is not assigned.");
            if (hatMilestonePrefab == null) Debug.LogError("CritterShopUI.hatMilestonePrefab is not assigned.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<CritterCollectionChangedEvent>(Refresh);
            GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<CritterCollectionChangedEvent>(Refresh);
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        // Same rule as the other building panels: interacting with anything else closes this one.
        private void OnPlayerInteracted(PlayerInteractedEvent evt)
        {
            if (evt.InteractableType != InteractableType.Building_CritterShop) Close();
        }

        public void Open()
        {
            if (panelRoot.activeSelf) return;

            var dialog = CritterShopController.Instance.Dialog;
            portraitImage.sprite = dialog.Portrait;
            portraitImage.enabled = dialog.Portrait != null;
            greetingLabel.text = ShopkeeperDialog.PickRandom(dialog.Greetings);

            InputBlocker.SetBlocked(true);
            panelRoot.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            if (!panelRoot.activeSelf) return;
            InputBlocker.SetBlocked(false);
            panelRoot.SetActive(false);
        }

        private void Refresh()
        {
            if (!panelRoot.activeSelf) return;

            var collection = CritterCollection.Instance;
            int jarCount = collection.Jar.Count;
            jarLabel.text = jarCount == 0
                ? "Your jar is empty."
                : $"Jar: {jarCount} critter{(jarCount == 1 ? "" : "s")} - worth ${collection.JarValue:0}";
            progressLabel.text = $"{collection.SpeciesCollected} / {GameManager.CritterDatabase.SpeciesCount} species";

            foreach (var slot in slots) slot.Refresh();
            foreach (var milestone in hatMilestones) milestone.Refresh();
        }
    }
}
