using Economy;
using Events;
using Interaction;
using Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // GameDesignDoc "Automation > Control Center": tabbed dashboard building. Opens/closes on
    // BuildingInteractedEvent exactly like MarketUI/DepotUI; per the resolved modal-blocking
    // decision, also blocks player input while open (InputBlocker) - unlike those other panels,
    // so Close() guards against redundant calls that would otherwise double-decrement the shared
    // block counter.
    public class ControlCenterUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button droneTabButton;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable() {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<UICloseEvent>(Close);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<UpgradeLoadedEvent>(OnUpgradeLoaded);
            RefreshDroneTabGate();
        }
        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<UICloseEvent>(Close);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<UpgradeLoadedEvent>(OnUpgradeLoaded);
        }

        // Distinctly-named typed handlers (rather than a parameterless one, or overloads sharing
        // this name) because EventService's registry keys both Add<T>(Action) and
        // Add<IEventType>(Action<IEventType>) off the same Type entry, and a method group with
        // both a parameterless and a typed overload makes the Add<T> call itself ambiguous.
        // AutomationSpawner already registers a typed handler for these events, so a parameterless
        // registration here would also collide and throw InvalidCastException at runtime.
        private void OnUpgradePurchased(UpgradePurchasedEvent _) => RefreshDroneTabGate();
        private void OnUpgradeLoaded(UpgradeLoadedEvent _) => RefreshDroneTabGate();

        private void RefreshDroneTabGate()
        {
            if (droneTabButton == null) { Debug.LogError("ControlCenterUI: droneTabButton not assigned."); return; }
            droneTabButton.gameObject.SetActive(UpgradeManager.Instance.AutomatonCount > 0);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            if (evt.Type == InteractableType.Building_ControlCenter) Open();
            else Close();
        }

        private void Open()
        {
            if (rendererRoot == null || rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
        }

        private void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
        }
    }
}
