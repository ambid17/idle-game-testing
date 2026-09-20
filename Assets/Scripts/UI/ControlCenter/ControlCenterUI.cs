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
        [SerializeField] private ResourceRefillUI resourceRefillUI;

        private void Start()
        {
            CheckNullRefs();
            closeButton.onClick.AddListener(Close);
            rendererRoot.SetActive(false);
        }

        private void CheckNullRefs()
        {
            if (rendererRoot == null) Debug.LogError("ControlCenterUI: rendererRoot not assigned in the Inspector.");
            if (closeButton == null) Debug.LogError("ControlCenterUI: closeButton not assigned in the Inspector.");
            if (droneTabButton == null) Debug.LogError("ControlCenterUI: droneTabButton not assigned in the Inspector.");
            if (resourceRefillUI == null) Debug.LogError("ControlCenterUI: resourceRefillUI not assigned in the Inspector.");
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
            droneTabButton.gameObject.SetActive(UpgradeManager.Instance.AutomatonCount > 0);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            if (evt.InteractableType != InteractableType.Building_ControlCenter) {
                Close();
                return;
            }

            switch(evt.InteractionType )
            {
                case InteractionType.Primary:
                    Open();
                    break;
                case InteractionType.Secondary:
                    resourceRefillUI.TryFillFuel();
                    // TODO: show toast with money spent, and animation of the HUD bar refill
                    break;
                case InteractionType.Tertiary:
                    resourceRefillUI.TryFillHp();
                    // TODO: show toast with money spent, and animation of the HUD bar refill
                    break;
                default:
                    Close();
                    break;
            }
            
        }

        private void Open()
        {
            if (rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
        }

        private void Close()
        {
            if (!rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
        }
    }
}
