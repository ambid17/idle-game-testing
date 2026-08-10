using Automation;
using Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "storage drone dashboard" tab: targeting choice only
    // (AutomationSettings.StorageDroneTargetMode), owned here per DepotUI's SellRequestedEvent
    // precedent - the settings object only stores state and dispatches the changed-event, the UI
    // owning the feature applies the request. Storage drone upgrades are purchased from MarketUI's
    // Automation tab instead - no duplicate purchase UI in the Control Center.
    public class StorageDroneDashboardUI : MonoBehaviour
    {
        [SerializeField] private Button playerAlwaysButton;
        [SerializeField] private Button fullestInventoryButton;
        [SerializeField] private GameObject playerAlwaysSelectedIndicator;
        [SerializeField] private GameObject fullestInventorySelectedIndicator;

        private void Start()
        {
            if (playerAlwaysButton != null) playerAlwaysButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneTargetModeRequestedEvent(TargetMode.PlayerAlways)));
            if (fullestInventoryButton != null) fullestInventoryButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneTargetModeRequestedEvent(TargetMode.FullestInventory)));
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<SetStorageDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Add<AutomationSettingsChangedEvent>(RefreshTargetingIndicator);
            RefreshTargetingIndicator();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<SetStorageDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Remove<AutomationSettingsChangedEvent>(RefreshTargetingIndicator);
        }

        private void OnTargetModeRequested(SetStorageDroneTargetModeRequestedEvent evt) => AutomationSettings.Instance.SetStorageDroneTargetMode(evt.Mode);

        private void RefreshTargetingIndicator()
        {
            bool playerAlways = AutomationSettings.Instance.StorageDroneTargetMode == TargetMode.PlayerAlways;
            if (playerAlwaysSelectedIndicator != null) playerAlwaysSelectedIndicator.SetActive(playerAlways);
            if (fullestInventorySelectedIndicator != null) fullestInventorySelectedIndicator.SetActive(!playerAlways);
        }
    }
}
