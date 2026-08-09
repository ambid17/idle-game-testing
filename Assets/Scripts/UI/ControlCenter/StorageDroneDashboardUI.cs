using System.Collections.Generic;
using Automation;
using Economy;
using Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "storage drone dashboard" tab: upgrade nodes (see MinerDashboardUI's header
    // comment on why no PurchaseRequestedEvent listener is needed here) plus the targeting choice
    // (AutomationSettings.StorageDroneTargetMode), owned here per DepotUI's SellRequestedEvent
    // precedent - the settings object only stores state and dispatches the changed-event, the UI
    // owning the feature applies the request.
    public class StorageDroneDashboardUI : MonoBehaviour
    {
        private static readonly UpgradeEffect[] Effects =
        {
            UpgradeEffect.StorageDroneCount,
            UpgradeEffect.StorageDroneMoveSpeed,
            UpgradeEffect.StorageDroneInventoryCapacity
        };

        [SerializeField] private Transform nodeContainer;
        [SerializeField] private UpgradeNodeUI nodePrefab;
        [SerializeField] private Button playerAlwaysButton;
        [SerializeField] private Button fullestInventoryButton;
        [SerializeField] private GameObject playerAlwaysSelectedIndicator;
        [SerializeField] private GameObject fullestInventorySelectedIndicator;

        private readonly List<UpgradeNodeUI> nodes = new();
        private UpgradeDatabase upgradeDatabase => GameManager.UpgradeDatabase;

        private void Start()
        {
            BuildNodes();
            if (playerAlwaysButton != null) playerAlwaysButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneTargetModeRequestedEvent(TargetMode.PlayerAlways)));
            if (fullestInventoryButton != null) fullestInventoryButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SetStorageDroneTargetModeRequestedEvent(TargetMode.FullestInventory)));
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<DollarsChangedEvent>(RefreshNodes);
            GameManager.EventService.Add<SetStorageDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Add<AutomationSettingsChangedEvent>(RefreshTargetingIndicator);
            RefreshNodes();
            RefreshTargetingIndicator();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<DollarsChangedEvent>(RefreshNodes);
            GameManager.EventService.Remove<SetStorageDroneTargetModeRequestedEvent>(OnTargetModeRequested);
            GameManager.EventService.Remove<AutomationSettingsChangedEvent>(RefreshTargetingIndicator);
        }

        private void BuildNodes()
        {
            if (nodePrefab == null || nodeContainer == null)
            {
                Debug.LogError("StorageDroneDashboardUI: Missing nodePrefab or nodeContainer.");
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

        private void OnTargetModeRequested(SetStorageDroneTargetModeRequestedEvent evt) => AutomationSettings.Instance.SetStorageDroneTargetMode(evt.Mode);

        private void RefreshTargetingIndicator()
        {
            bool playerAlways = AutomationSettings.Instance.StorageDroneTargetMode == TargetMode.PlayerAlways;
            if (playerAlwaysSelectedIndicator != null) playerAlwaysSelectedIndicator.SetActive(playerAlways);
            if (fullestInventorySelectedIndicator != null) fullestInventorySelectedIndicator.SetActive(!playerAlways);
        }
    }
}
