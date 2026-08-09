using System.Collections.Generic;
using Automation;
using Economy;
using Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "fuel drone dashboard" tab: upgrade nodes (see MinerDashboardUI's header
    // comment on why no PurchaseRequestedEvent listener is needed here), targeting choice, and the
    // spending-cap slider (AutomationSettings.FuelSpendingCapPercent).
    public class FuelDroneDashboardUI : MonoBehaviour
    {
        private static readonly UpgradeEffect[] Effects =
        {
            UpgradeEffect.FuelDroneCount,
            UpgradeEffect.FuelDroneMoveSpeed,
            UpgradeEffect.FuelDroneInventoryCapacity
        };

        [SerializeField] private Transform nodeContainer;
        [SerializeField] private UpgradeNodeUI nodePrefab;
        [SerializeField] private Button playerAlwaysButton;
        [SerializeField] private Button fullestInventoryButton;
        [SerializeField] private GameObject playerAlwaysSelectedIndicator;
        [SerializeField] private GameObject fullestInventorySelectedIndicator;
        [SerializeField] private Slider spendingCapSlider; // 0-1, normalized
        [SerializeField] private TMP_Text spendingCapLabel;

        private readonly List<UpgradeNodeUI> nodes = new();
        private UpgradeDatabase upgradeDatabase => GameManager.UpgradeDatabase;
        private bool suppressSliderEvent;

        private void Start()
        {
            BuildNodes();
            if (playerAlwaysButton != null) playerAlwaysButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetFuelDroneTargetModeRequestedEvent(TargetMode.PlayerAlways)));
            if (fullestInventoryButton != null) fullestInventoryButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetFuelDroneTargetModeRequestedEvent(TargetMode.FullestInventory)));
            if (spendingCapSlider != null) spendingCapSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<DollarsChangedEvent>(RefreshNodes);
            GameManager.EventService.Add<SetFuelDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Add<SetFuelSpendingCapRequestedEvent>(OnSpendingCapRequested);
            GameManager.EventService.Add<AutomationSettingsChangedEvent>(RefreshSettingsDisplay);
            RefreshNodes();
            RefreshSettingsDisplay();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<DollarsChangedEvent>(RefreshNodes);
            GameManager.EventService.Remove<SetFuelDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Remove<SetFuelSpendingCapRequestedEvent>(OnSpendingCapRequested);
            GameManager.EventService.Remove<AutomationSettingsChangedEvent>(RefreshSettingsDisplay);
        }

        private void BuildNodes()
        {
            if (nodePrefab == null || nodeContainer == null)
            {
                Debug.LogError("FuelDroneDashboardUI: Missing nodePrefab or nodeContainer.");
                return;
            }

            foreach (var effect in Effects)
            {
                var def = upgradeDatabase.Find(effect);
                if (def == null) continue;

                var node = Instantiate(nodePrefab, nodeContainer);
                node.Bind(def);
                nodes.Add(node);
            }
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => RefreshNodes();
        private void RefreshNodes()
        {
            foreach (var node in nodes) node.Refresh();
        }

        private void OnTargetModeRequested(SetFuelDroneTargetModeRequestedEvent evt) => AutomationSettings.Instance.SetFuelDroneTargetMode(evt.Mode);
        private void OnSpendingCapRequested(SetFuelSpendingCapRequestedEvent evt) => AutomationSettings.Instance.SetFuelSpendingCapPercent(evt.Percent);

        private void OnSliderChanged(float value)
        {
            if (suppressSliderEvent) return;
            GameManager.EventService.Dispatch(new SetFuelSpendingCapRequestedEvent(value));
        }

        private void RefreshSettingsDisplay()
        {
            bool playerAlways = AutomationSettings.Instance.FuelDroneTargetMode == TargetMode.PlayerAlways;
            if (playerAlwaysSelectedIndicator != null) playerAlwaysSelectedIndicator.SetActive(playerAlways);
            if (fullestInventorySelectedIndicator != null) fullestInventorySelectedIndicator.SetActive(!playerAlways);

            float percent = AutomationSettings.Instance.FuelSpendingCapPercent;
            if (spendingCapSlider != null)
            {
                suppressSliderEvent = true;
                spendingCapSlider.value = percent;
                suppressSliderEvent = false;
            }
            if (spendingCapLabel != null) spendingCapLabel.text = $"{percent * 100f:0}%";
        }
    }
}
