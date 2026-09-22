using System.Collections.Generic;
using Economy;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Dev Panel tab: skip Market/Museum progression. Maxing Automation_AutomatonCount/
    // StorageDroneCount/FuelDroneCount here also spawns the corresponding units for free, since
    // AutomationSpawner already reconciles entity counts off UpgradeLoadedEvent - no separate
    // "spawn" cheat is needed.
    public class DevPanelProgressionTab : MonoBehaviour
    {
        [SerializeField] private Transform upgradeRowContainer;
        [SerializeField] private DevProgressionUpgradeRowUI upgradeRowPrefab;
        [SerializeField] private Transform prestigeUpgradeRowContainer;
        [SerializeField] private DevProgressionUpgradeRowUI prestigeUpgradeRowPrefab;
        [SerializeField] private Button maxAllUpgradesButton;
        [SerializeField] private Button removeAllUpgradesButton;
        [SerializeField] private Button maxAllPrestigeUpgradesButton;
        [SerializeField] private Button removeAllPrestigeUpgradesButton;
        [SerializeField] private Button forcePrestigeButton;

        private readonly List<DevProgressionUpgradeRowUI> upgradeRows = new();
        private readonly List<DevProgressionUpgradeRowUI> prestigeUpgradeRows = new();

        private void Start()
        {
            if (upgradeRowContainer == null) Debug.LogError("DevPanelProgressionTab.upgradeRowContainer is not assigned.");
            if (upgradeRowPrefab == null) Debug.LogError("DevPanelProgressionTab.upgradeRowPrefab is not assigned.");
            if (prestigeUpgradeRowContainer == null) Debug.LogError("DevPanelProgressionTab.prestigeUpgradeRowContainer is not assigned.");
            if (prestigeUpgradeRowPrefab == null) Debug.LogError("DevPanelProgressionTab.prestigeUpgradeRowPrefab is not assigned.");
            if (maxAllUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.maxAllUpgradesButton is not assigned.");
            if (removeAllUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.removeAllUpgradesButton is not assigned.");
            if (maxAllPrestigeUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.maxAllPrestigeUpgradesButton is not assigned.");
            if (removeAllPrestigeUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.removeAllPrestigeUpgradesButton is not assigned.");
            if (forcePrestigeButton == null) Debug.LogError("DevPanelProgressionTab.forcePrestigeButton is not assigned.");

            BuildUpgradeRows();
            BuildPrestigeUpgradeRows();

            if (maxAllUpgradesButton != null) maxAllUpgradesButton.onClick.AddListener(OnMaxAllUpgradesClicked);
            if (removeAllUpgradesButton != null) removeAllUpgradesButton.onClick.AddListener(OnRemoveAllUpgradesClicked);
            if (maxAllPrestigeUpgradesButton != null) maxAllPrestigeUpgradesButton.onClick.AddListener(OnMaxAllPrestigeUpgradesClicked);
            if (removeAllPrestigeUpgradesButton != null) removeAllPrestigeUpgradesButton.onClick.AddListener(OnRemoveAllPrestigeUpgradesClicked);
            if (forcePrestigeButton != null) forcePrestigeButton.onClick.AddListener(() => PrestigeManager.Instance.ExecutePrestige());
        }

        private void BuildUpgradeRows()
        {
            if (upgradeRowContainer == null || upgradeRowPrefab == null) return;

            foreach (var def in GameManager.UpgradeDatabase.Upgrades)
            {
                if (def == null) continue;
                var row = Instantiate(upgradeRowPrefab, upgradeRowContainer);
                row.Bind(def.DisplayName,
                    () => UpgradeManager.Instance.GetPurchasedLevel(def),
                    () => UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, def.MaxLevel),
                    () => OnIncrementUpgradeClicked(def),
                    () => OnDecrementUpgradeClicked(def));
                upgradeRows.Add(row);
            }
        }

        private void BuildPrestigeUpgradeRows()
        {
            if (prestigeUpgradeRowContainer == null || prestigeUpgradeRowPrefab == null) return;

            foreach (var def in GameManager.PrestigeUpgradeDatabase.Upgrades)
            {
                if (def == null) continue;
                var row = Instantiate(prestigeUpgradeRowPrefab, prestigeUpgradeRowContainer);
                row.Bind(def.DisplayName,
                    () => PrestigeUpgradeManager.Instance.GetPurchasedLevel(def),
                    () => PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, def.MaxLevel),
                    () => OnIncrementPrestigeUpgradeClicked(def),
                    () => OnDecrementPrestigeUpgradeClicked(def));
                prestigeUpgradeRows.Add(row);
            }
        }

        private void OnIncrementUpgradeClicked(UpgradeDefinition def)
        {
            int newLevel = Mathf.Min(def.MaxLevel, UpgradeManager.Instance.GetPurchasedLevel(def) + 1);
            UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, newLevel);
        }

        private void OnDecrementUpgradeClicked(UpgradeDefinition def)
        {
            int newLevel = Mathf.Max(0, UpgradeManager.Instance.GetPurchasedLevel(def) - 1);
            UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, newLevel);
        }

        private void OnIncrementPrestigeUpgradeClicked(PrestigeUpgradeDefinition def)
        {
            int newLevel = Mathf.Min(def.MaxLevel, PrestigeUpgradeManager.Instance.GetPurchasedLevel(def) + 1);
            PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, newLevel);
        }

        private void OnDecrementPrestigeUpgradeClicked(PrestigeUpgradeDefinition def)
        {
            int newLevel = Mathf.Max(0, PrestigeUpgradeManager.Instance.GetPurchasedLevel(def) - 1);
            PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, newLevel);
        }

        private void OnMaxAllUpgradesClicked()
        {
            foreach (var def in GameManager.UpgradeDatabase.Upgrades)
            {
                if (def != null) UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, def.MaxLevel);
            }
            RefreshRows(upgradeRows);
        }

        private void OnRemoveAllUpgradesClicked()
        {
            UpgradeManager.Instance.ResetAllLevels();
            RefreshRows(upgradeRows);
        }

        private void OnMaxAllPrestigeUpgradesClicked()
        {
            foreach (var def in GameManager.PrestigeUpgradeDatabase.Upgrades)
            {
                if (def != null) PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, def.MaxLevel);
            }
            RefreshRows(prestigeUpgradeRows);
        }

        private void OnRemoveAllPrestigeUpgradesClicked()
        {
            PrestigeUpgradeManager.Instance.ResetAllLevels();
            RefreshRows(prestigeUpgradeRows);
        }

        private static void RefreshRows(List<DevProgressionUpgradeRowUI> rows)
        {
            foreach (var row in rows)
            {
                if (row != null) row.Refresh();
            }
        }
    }
}
