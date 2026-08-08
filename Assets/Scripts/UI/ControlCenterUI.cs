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

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<BuildingInteractedEvent>(OnBuildingInteracted);
        private void OnDisable() => GameManager.EventService.Remove<BuildingInteractedEvent>(OnBuildingInteracted);

        private void OnBuildingInteracted(BuildingInteractedEvent evt)
        {
            if (evt.Type == InteractableType.ControlCenter) Open();
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
