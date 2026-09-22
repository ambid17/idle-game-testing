using Automation;
using Economy;
using Events;
using UI.Reuseable;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "storage drone dashboard" tab: targeting choice
    // (AutomationSettings.StorageDroneTargetMode) and deposit-vs-auto-sell choice
    // (AutomationSettings.StorageDroneDepositMode), owned here per DepotUI's SellRequestedEvent
    // precedent - the settings object only stores state and dispatches the changed-event, the UI
    // owning the feature applies the request. Storage drone upgrades are purchased from MarketUI's
    // Automation tab instead - no duplicate purchase UI in the Control Center. The Auto Sell option
    // stays gated behind UpgradeManager.StorageDroneAutoSellUnlocked (the "Market Sense" capstone)
    // even though the toggle itself lives here. While locked, hovering the button shows an
    // explanatory tooltip via HoverTooltipTrigger.
    public class StorageDroneDashboardUI : MonoBehaviour
    {
        [SerializeField] private Button playerAlwaysButton;
        [SerializeField] private Button fullestInventoryButton;
        [SerializeField] private GameObject playerAlwaysSelectedIndicator;
        [SerializeField] private GameObject fullestInventorySelectedIndicator;

        [SerializeField] private Button depositButton;
        [SerializeField] private Button autoSellButton;
        [SerializeField] private GameObject depositSelectedIndicator;
        [SerializeField] private GameObject autoSellSelectedIndicator;
        [SerializeField] private GameObject autoSellLockedIndicator;
        [SerializeField] private HoverTooltipTrigger autoSellLockedTooltipTrigger;

        private void Start()
        {
            if (playerAlwaysButton != null) playerAlwaysButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneTargetModeRequestedEvent(TargetMode.PlayerAlways)));
            if (fullestInventoryButton != null) fullestInventoryButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneTargetModeRequestedEvent(TargetMode.FullestInventory)));
            if (depositButton != null) depositButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneDepositModeRequestedEvent(StorageDroneDepositMode.Deposit)));
            if (autoSellButton != null) autoSellButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneDepositModeRequestedEvent(StorageDroneDepositMode.AutoSell)));
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<SetStorageDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Add<SetStorageDroneDepositModeRequestedEvent>(OnDepositModeRequested);
            GameManager.EventService.Add<AutomationSettingsChangedEvent>(RefreshTargetingIndicator);
            GameManager.EventService.Add<AutomationSettingsChangedEvent>(RefreshDepositModeIndicator);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<UpgradeLoadedEvent>(OnUpgradeLoaded);
            RefreshTargetingIndicator();
            RefreshDepositModeIndicator();
            RefreshAutoSellGate();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<SetStorageDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Remove<SetStorageDroneDepositModeRequestedEvent>(OnDepositModeRequested);
            GameManager.EventService.Remove<AutomationSettingsChangedEvent>(RefreshTargetingIndicator);
            GameManager.EventService.Remove<AutomationSettingsChangedEvent>(RefreshDepositModeIndicator);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<UpgradeLoadedEvent>(OnUpgradeLoaded);
        }

        private void OnTargetModeRequested(SetStorageDroneTargetModeRequestedEvent evt) => AutomationSettings.Instance.SetStorageDroneTargetMode(evt.Mode);

        // Guards against AutoSell being requested while still locked - belt-and-suspenders on top
        // of autoSellButton.interactable being false, since that's what actually stops the click.
        private void OnDepositModeRequested(SetStorageDroneDepositModeRequestedEvent evt)
        {
            if (evt.Mode == StorageDroneDepositMode.AutoSell && !UpgradeManager.Instance.StorageDroneAutoSellUnlocked) return;
            AutomationSettings.Instance.SetStorageDroneDepositMode(evt.Mode);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent _) => RefreshAutoSellGate();
        private void OnUpgradeLoaded(UpgradeLoadedEvent _) => RefreshAutoSellGate();

        private void RefreshTargetingIndicator()
        {
            bool playerAlways = AutomationSettings.Instance.StorageDroneTargetMode == TargetMode.PlayerAlways;
            if (playerAlwaysSelectedIndicator != null) playerAlwaysSelectedIndicator.SetActive(playerAlways);
            if (fullestInventorySelectedIndicator != null) fullestInventorySelectedIndicator.SetActive(!playerAlways);
        }

        private void RefreshDepositModeIndicator()
        {
            bool autoSell = AutomationSettings.Instance.StorageDroneDepositMode == StorageDroneDepositMode.AutoSell;
            if (depositSelectedIndicator != null) depositSelectedIndicator.SetActive(!autoSell);
            if (autoSellSelectedIndicator != null) autoSellSelectedIndicator.SetActive(autoSell);
        }

        private void RefreshAutoSellGate()
        {
            bool unlocked = UpgradeManager.Instance.StorageDroneAutoSellUnlocked;
            if (autoSellButton != null) autoSellButton.interactable = unlocked;
            if (autoSellLockedIndicator != null) autoSellLockedIndicator.SetActive(!unlocked);
            if (autoSellLockedTooltipTrigger != null) autoSellLockedTooltipTrigger.Active = !unlocked;
        }
    }
}
