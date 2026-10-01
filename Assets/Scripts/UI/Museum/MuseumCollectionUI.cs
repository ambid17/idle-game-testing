using System.Collections.Generic;
using Events;
using Museum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // The Museum panel's Collection tab: the curator's greeting, how many runes are waiting to be
    // shown to them, the 20-rune grid, and the accessory milestones (with Wear/Remove). Turn In /
    // Talk hand over to Museum.MuseumCuratorController, which runs the curator's dialog over the panel.
    // This is the tab's content root, so TabGroupUI toggles this GameObject directly.
    public class MuseumCollectionUI : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text greetingLabel;
        [SerializeField] private TMP_Text pendingLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Button turnInButton;
        [SerializeField] private Button talkButton;
        [SerializeField] private Transform runeContainer;
        [SerializeField] private RuneSlotUI runeSlotPrefab;
        [SerializeField] private Transform accessoryContainer;
        [SerializeField] private AccessoryMilestoneUI accessoryMilestonePrefab;

        private readonly List<RuneSlotUI> runeSlots = new();
        private readonly List<AccessoryMilestoneUI> accessoryMilestones = new();
        private bool built;

        private void Awake()
        {
            if (portraitImage == null) Debug.LogError("MuseumCollectionUI.portraitImage is not assigned.");
            if (greetingLabel == null) Debug.LogError("MuseumCollectionUI.greetingLabel is not assigned.");
            if (pendingLabel == null) Debug.LogError("MuseumCollectionUI.pendingLabel is not assigned.");
            if (progressLabel == null) Debug.LogError("MuseumCollectionUI.progressLabel is not assigned.");
            if (turnInButton == null) Debug.LogError("MuseumCollectionUI.turnInButton is not assigned.");
            if (talkButton == null) Debug.LogError("MuseumCollectionUI.talkButton is not assigned.");
            if (runeContainer == null) Debug.LogError("MuseumCollectionUI.runeContainer is not assigned.");
            if (runeSlotPrefab == null) Debug.LogError("MuseumCollectionUI.runeSlotPrefab is not assigned.");
            if (accessoryContainer == null) Debug.LogError("MuseumCollectionUI.accessoryContainer is not assigned.");
            if (accessoryMilestonePrefab == null) Debug.LogError("MuseumCollectionUI.accessoryMilestonePrefab is not assigned.");

            turnInButton.onClick.AddListener(() => MuseumCuratorController.Instance.TurnIn());
            talkButton.onClick.AddListener(() => MuseumCuratorController.Instance.Chat());
        }

        // Also subscribed while the tab is hidden, so it's already current when shown.
        private void OnEnable()
        {
            GameManager.EventService.Add<RuneCollectionChangedEvent>(Refresh);
            GameManager.EventService.Add<PlayerAccessoriesChangedEvent>(Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<RuneCollectionChangedEvent>(Refresh);
            GameManager.EventService.Remove<PlayerAccessoriesChangedEvent>(Refresh);
        }

        // MuseumUI, each time the panel opens.
        public void Greet()
        {
            var dialog = MuseumCuratorController.Instance.Dialog;
            portraitImage.sprite = dialog.Portrait;
            portraitImage.enabled = dialog.Portrait != null;
            greetingLabel.text = CuratorDialog.PickRandom(dialog.Greetings);
        }

        private void BuildIfNeeded()
        {
            if (built) return;
            built = true;

            var database = GameManager.MuseumCollectionDatabase;
            foreach (var rune in database.Runes)
            {
                var slot = Instantiate(runeSlotPrefab, runeContainer);
                slot.gameObject.name = $"Rune_{rune.Index:00}_{rune.DisplayName}";
                slot.Bind(rune);
                runeSlots.Add(slot);
            }

            foreach (var accessory in database.Accessories)
            {
                var milestone = Instantiate(accessoryMilestonePrefab, accessoryContainer);
                milestone.gameObject.name = $"Accessory_{accessory.Id}";
                milestone.Bind(accessory);
                accessoryMilestones.Add(milestone);
            }
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled) return;
            BuildIfNeeded();

            var collection = RuneCollection.Instance;
            int pending = collection.FoundCount;
            pendingLabel.text = pending == 0
                ? "No new runes to show the curator."
                : $"{pending} new rune{(pending == 1 ? "" : "s")} to show the curator!";
            progressLabel.text = $"{collection.DonatedCount} / {GameManager.MuseumCollectionDatabase.RuneCount} runes donated";

            foreach (var slot in runeSlots) slot.Refresh();
            foreach (var milestone in accessoryMilestones) milestone.Refresh();
        }
    }
}
