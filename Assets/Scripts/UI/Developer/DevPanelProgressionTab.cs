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
        [SerializeField] private DevUpgradeRowUI upgradeRowPrefab;
        [SerializeField] private Transform prestigeUpgradeRowContainer;
        [SerializeField] private DevUpgradeRowUI prestigeUpgradeRowPrefab;
        [SerializeField] private Button maxAllUpgradesButton;
        [SerializeField] private Button maxAllPrestigeUpgradesButton;
        [SerializeField] private Button forcePrestigeButton;

        private void Start()
        {
            if (upgradeRowContainer == null) Debug.LogError("DevPanelProgressionTab.upgradeRowContainer is not assigned.");
            if (upgradeRowPrefab == null) Debug.LogError("DevPanelProgressionTab.upgradeRowPrefab is not assigned.");
            if (prestigeUpgradeRowContainer == null) Debug.LogError("DevPanelProgressionTab.prestigeUpgradeRowContainer is not assigned.");
            if (prestigeUpgradeRowPrefab == null) Debug.LogError("DevPanelProgressionTab.prestigeUpgradeRowPrefab is not assigned.");
            if (maxAllUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.maxAllUpgradesButton is not assigned.");
            if (maxAllPrestigeUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.maxAllPrestigeUpgradesButton is not assigned.");
            if (forcePrestigeButton == null) Debug.LogError("DevPanelProgressionTab.forcePrestigeButton is not assigned.");

            BuildUpgradeRows();
            BuildPrestigeUpgradeRows();

            if (maxAllUpgradesButton != null) maxAllUpgradesButton.onClick.AddListener(OnMaxAllUpgradesClicked);
            if (maxAllPrestigeUpgradesButton != null) maxAllPrestigeUpgradesButton.onClick.AddListener(OnMaxAllPrestigeUpgradesClicked);
            if (forcePrestigeButton != null) forcePrestigeButton.onClick.AddListener(() => PrestigeManager.Instance.ExecutePrestige());
        }

        private void BuildUpgradeRows()
        {
            if (upgradeRowContainer == null || upgradeRowPrefab == null) return;

            foreach (var def in GameManager.UpgradeDatabase.Upgrades)
            {
                if (def == null) continue;
                var row = Instantiate(upgradeRowPrefab, upgradeRowContainer);
                row.Bind(def.DisplayName, () => UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, def.MaxLevel));
            }
        }

        private void BuildPrestigeUpgradeRows()
        {
            if (prestigeUpgradeRowContainer == null || prestigeUpgradeRowPrefab == null) return;

            foreach (var def in GameManager.PrestigeUpgradeDatabase.Upgrades)
            {
                if (def == null) continue;
                var row = Instantiate(prestigeUpgradeRowPrefab, prestigeUpgradeRowContainer);
                row.Bind(def.DisplayName, () => PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, def.MaxLevel));
            }
        }

        private void OnMaxAllUpgradesClicked()
        {
            foreach (var def in GameManager.UpgradeDatabase.Upgrades)
            {
                if (def != null) UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, def.MaxLevel);
            }
        }

        private void OnMaxAllPrestigeUpgradesClicked()
        {
            foreach (var def in GameManager.PrestigeUpgradeDatabase.Upgrades)
            {
                if (def != null) PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, def.MaxLevel);
            }
        }
    }
}
