using System;
using System.Collections.Generic;
using System.Globalization;
using Automation;
using Events;
using MapGeneration;
using Player;
using TMPro;
using Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Dev Panel tab: simulate an offline-earnings screen without waiting, and replay any tutorial
    // popup that's already been shown this save.
    public class DevPanelTimeTutorialTab : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private TMP_InputField offlineMinutesInput;
        [SerializeField] private Button simulateOfflineTimeButton;
        [SerializeField] private Transform tutorialRowContainer;
        [SerializeField] private DevUpgradeRowUI tutorialRowPrefab;

        private void Start()
        {
            if (playerController == null) Debug.LogError("DevPanelTimeTutorialTab.playerController is not assigned.");
            if (offlineMinutesInput == null) Debug.LogError("DevPanelTimeTutorialTab.offlineMinutesInput is not assigned.");
            if (simulateOfflineTimeButton == null) Debug.LogError("DevPanelTimeTutorialTab.simulateOfflineTimeButton is not assigned.");
            if (tutorialRowContainer == null) Debug.LogError("DevPanelTimeTutorialTab.tutorialRowContainer is not assigned.");
            if (tutorialRowPrefab == null) Debug.LogError("DevPanelTimeTutorialTab.tutorialRowPrefab is not assigned.");

            if (simulateOfflineTimeButton != null) simulateOfflineTimeButton.onClick.AddListener(OnSimulateOfflineTimeClicked);
            BuildTutorialRows();
        }

        private void OnSimulateOfflineTimeClicked()
        {
            if (offlineMinutesInput == null) return;
            if (!float.TryParse(offlineMinutesInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutesAway) || minutesAway <= 0f) return;

            var oreGained = new Dictionary<BlockTypeId, int>();
            foreach (var kvp in IdleEarningsTracker.Instance.AveragePerMinute)
            {
                int amount = Mathf.RoundToInt(kvp.Value * minutesAway);
                if (amount > 0) oreGained[kvp.Key] = amount;
            }
            if (oreGained.Count == 0) return;

            GameManager.EventService.Dispatch(new OfflineEarningsReadyEvent(oreGained, minutesAway));
        }

        private void BuildTutorialRows()
        {
            if (tutorialRowContainer == null || tutorialRowPrefab == null) return;

            foreach (TutorialId id in Enum.GetValues(typeof(TutorialId)))
            {
                var row = Instantiate(tutorialRowPrefab, tutorialRowContainer);
                row.Bind(id.ToString(), () => OnReplayTutorialClicked(id));
            }
        }

        private void OnReplayTutorialClicked(TutorialId id)
        {
            TutorialManager.Instance.ResetShown(id);
            Vector3? worldPos = playerController != null ? playerController.transform.position : (Vector3?)null;
            TutorialManager.Instance.TryShow(id, worldPos);
        }
    }
}
