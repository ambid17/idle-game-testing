using Automation;
using Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "fuel drone dashboard" tab: targeting choice only (fuel is free, so there's
    // no spending cap). Fuel drone upgrades are purchased from
    // MarketUI's Automation tab instead - no duplicate purchase UI in the Control Center.
    public class FuelDroneDashboardUI : MonoBehaviour
    {
        [SerializeField] private Button playerAlwaysButton;
        [SerializeField] private Button fullestInventoryButton;
        [SerializeField] private GameObject playerAlwaysSelectedIndicator;
        [SerializeField] private GameObject fullestInventorySelectedIndicator;

        private void Start()
        {
            if (playerAlwaysButton != null) playerAlwaysButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetFuelDroneTargetModeRequestedEvent(TargetMode.PlayerAlways)));
            if (fullestInventoryButton != null) fullestInventoryButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetFuelDroneTargetModeRequestedEvent(TargetMode.FullestInventory)));
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<SetFuelDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Add<AutomationSettingsChangedEvent>(RefreshSettingsDisplay);
            RefreshSettingsDisplay();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<SetFuelDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Remove<AutomationSettingsChangedEvent>(RefreshSettingsDisplay);
        }

        private void OnTargetModeRequested(SetFuelDroneTargetModeRequestedEvent evt) => AutomationSettings.Instance.SetFuelDroneTargetMode(evt.Mode);

        private void RefreshSettingsDisplay()
        {
            bool playerAlways = AutomationSettings.Instance.FuelDroneTargetMode == TargetMode.PlayerAlways;
            if (playerAlwaysSelectedIndicator != null) playerAlwaysSelectedIndicator.SetActive(playerAlways);
            if (fullestInventorySelectedIndicator != null) fullestInventorySelectedIndicator.SetActive(!playerAlways);
        }
    }
}
