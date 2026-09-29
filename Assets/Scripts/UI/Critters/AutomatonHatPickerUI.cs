using System.Collections.Generic;
using Critters;
using Economy;
using Events;
using TMPro;
using UnityEngine;

namespace UI
{
    // Control Center section: one AutomatonHatRowUI per owned Mining Automaton, each cycling
    // through "no hat" plus every hat unlocked at the Critter Shop. Rows follow the automaton
    // count upgrade; choices persist per automaton via Critters.CritterCollection.
    public class AutomatonHatPickerUI : MonoBehaviour
    {
        [SerializeField] private Transform rowContainer;
        [SerializeField] private AutomatonHatRowUI rowPrefab;
        [Tooltip("Shown instead of the rows while there's nothing to pick yet (no hats unlocked, or no automatons to wear them).")]
        [SerializeField] private TMP_Text emptyMessage;
        [SerializeField, TextArea] private string noHatsText = "No hats yet! Turn in critters at Grizzle's Critter Emporium, somewhere down in the mine, to unlock hats for your automatons.";
        [SerializeField, TextArea] private string noAutomatonsText = "You've got hats but nobody to wear them - buy a Mining Automaton at the Market.";

        private readonly List<AutomatonHatRowUI> rows = new();

        private void OnEnable()
        {
            if (rowContainer == null) Debug.LogError("AutomatonHatPickerUI.rowContainer is not assigned.");
            if (rowPrefab == null) Debug.LogError("AutomatonHatPickerUI.rowPrefab is not assigned.");
            if (emptyMessage == null) Debug.LogError("AutomatonHatPickerUI.emptyMessage is not assigned.");

            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<CritterCollectionChangedEvent>(Rebuild);
            GameManager.EventService.Add<AutomatonHatsChangedEvent>(RefreshRows);
            Rebuild();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<CritterCollectionChangedEvent>(Rebuild);
            GameManager.EventService.Remove<AutomatonHatsChangedEvent>(RefreshRows);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => Rebuild();

        private void Rebuild()
        {
            var unlockedHats = new List<HatDefinition>();
            foreach (var hat in GameManager.CritterDatabase.Hats)
            {
                if (CritterCollection.Instance.IsHatUnlocked(hat)) unlockedHats.Add(hat);
            }

            int automatonCount = unlockedHats.Count > 0 ? UpgradeManager.Instance.Automation_AutomatonCount : 0;
            emptyMessage.gameObject.SetActive(automatonCount == 0);
            emptyMessage.text = unlockedHats.Count == 0 ? noHatsText : noAutomatonsText;

            while (rows.Count < automatonCount)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.gameObject.name = $"HatRow_{rows.Count + 1}";
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < automatonCount;
                rows[i].gameObject.SetActive(active);
                // Automaton DisplayIndex is 1-based (see AutomationSpawner).
                if (active) rows[i].Bind(i + 1, unlockedHats);
            }
        }

        private void RefreshRows()
        {
            foreach (var row in rows)
            {
                if (row.gameObject.activeSelf) row.Refresh();
            }
        }
    }
}
