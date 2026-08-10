using Automation;
using Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "fuel drone dashboard" tab: targeting choice and the spending-cap slider
    // (AutomationSettings.FuelSpendingCapPercent) only. Fuel drone upgrades are purchased from
    // MarketUI's Automation tab instead - no duplicate purchase UI in the Control Center.
    public class FuelDroneDashboardUI : MonoBehaviour
    {
        [SerializeField] private Button playerAlwaysButton;
        [SerializeField] private Button fullestInventoryButton;
        [SerializeField] private GameObject playerAlwaysSelectedIndicator;
        [SerializeField] private GameObject fullestInventorySelectedIndicator;
        [SerializeField] private Slider spendingCapSlider; // 0-1, normalized
        [SerializeField] private TMP_Text spendingCapLabel;

        private bool suppressSliderEvent;

        private void Start()
        {
            if (playerAlwaysButton != null) playerAlwaysButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetFuelDroneTargetModeRequestedEvent(TargetMode.PlayerAlways)));
            if (fullestInventoryButton != null) fullestInventoryButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetFuelDroneTargetModeRequestedEvent(TargetMode.FullestInventory)));
            if (spendingCapSlider != null) spendingCapSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<SetFuelDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Add<SetFuelSpendingCapRequestedEvent>(OnSpendingCapRequested);
            GameManager.EventService.Add<AutomationSettingsChangedEvent>(RefreshSettingsDisplay);
            RefreshSettingsDisplay();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<SetFuelDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Remove<SetFuelSpendingCapRequestedEvent>(OnSpendingCapRequested);
            GameManager.EventService.Remove<AutomationSettingsChangedEvent>(RefreshSettingsDisplay);
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
