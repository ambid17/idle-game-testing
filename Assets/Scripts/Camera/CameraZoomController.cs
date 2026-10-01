using Economy;
using Events;
using Player;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CameraControl
{
    // GameDesignDoc "Lantern capstones > zoom, enhance" (Mining_CameraZoom): adds to the camera's
    // base distance to reveal more of the map. The mouse wheel zooms freely between the original
    // distance and the furthest one the upgrade has unlocked. Single scene use - editor-referenced
    // per the project's object-reference convention (see CLAUDE.md "Object references"), no
    // singleton needed. Namespaced as CameraControl rather than Camera to avoid shadowing
    // UnityEngine.Camera for any file in this folder.
    public class CameraZoomController : MonoBehaviour
    {
        [SerializeField] private CinemachinePositionComposer cinemachineCamera;
        // Camera distance changed per mouse wheel notch.
        [SerializeField] private float scrollStep = 0.275f;
        [SerializeField] private float zoomSmoothing = 12f;

        private CinemachineConfiner2D confiner;
        private float baseDistance;
        // Distance beyond baseDistance the camera is easing toward, 0..MaxZoomOffset.
        private float targetZoomOffset;

        private static float MaxZoomOffset => UpgradeManager.Instance != null ? UpgradeManager.Instance.Mining_CameraZoomBonus : 0f;

        private void Awake()
        {
            cinemachineCamera = GetComponent<CinemachinePositionComposer>();
            if (cinemachineCamera == null)
            {
                Debug.LogError($"{nameof(CameraZoomController)} on {name} is missing its cinemachineCamera reference.");
                return;
            }
            confiner = GetComponent<CinemachineConfiner2D>();
            if (confiner == null) Debug.LogError($"{nameof(CameraZoomController)} on {name} has no CinemachineConfiner2D.");
            baseDistance = cinemachineCamera.CameraDistance;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradeChanged);
            GameManager.EventService.Add<UpgradeLoadedEvent>(OnUpgradeLoaded);
            ZoomToMax();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradeChanged);
            GameManager.EventService.Remove<UpgradeLoadedEvent>(OnUpgradeLoaded);
        }

        private void Update()
        {
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            // Scrolling up zooms in. Only the sign is used - the wheel's raw delta per notch
            // differs between platforms.
            if (scroll != 0f && CanScrollZoom()) targetZoomOffset -= Mathf.Sign(scroll) * scrollStep;
            // Clamped every frame, not just on scroll, so losing the upgrade (prestige reset)
            // pulls the camera back in.
            targetZoomOffset = Mathf.Clamp(targetZoomOffset, 0f, MaxZoomOffset);

            float targetDistance = baseDistance + targetZoomOffset;
            float distance = cinemachineCamera.CameraDistance;
            if (Mathf.Approximately(distance, targetDistance)) return;

            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime));
            if (Mathf.Abs(distance - targetDistance) < 0.01f) distance = targetDistance;
            cinemachineCamera.CameraDistance = distance;

            // Confiner2D caches the confining area it derived from the view size - it keeps
            // clamping to the old, smaller view (showing past the grid edge) unless told the
            // lens changed.
            confiner.InvalidateLensCache();
        }

        // The wheel belongs to the UI while a modal is open or the pointer is over a scroll list.
        private static bool CanScrollZoom()
        {
            if (ModalTracker.IsAnyModalOpen || InputBlocker.IsBlocked) return false;
            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }

        private void OnUpgradeChanged(UpgradePurchasedEvent evt)
        {
            if (evt.Definition.Effect == UpgradeEffect.Mining_CameraZoom) ZoomToMax();
        }

        private void OnUpgradeLoaded(UpgradeLoadedEvent evt)
        {
            if (evt.Definition.Effect == UpgradeEffect.Mining_CameraZoom) ZoomToMax();
        }

        // A newly bought (or loaded) level zooms all the way out, so the purchase is visible.
        private void ZoomToMax()
        {
            targetZoomOffset = MaxZoomOffset;
        }
    }
}
