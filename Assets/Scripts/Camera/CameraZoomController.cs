using Economy;
using Events;
using Unity.Cinemachine;
using UnityEngine;

namespace CameraControl
{
    // GameDesignDoc "Lantern capstones > zoom, enhance" (Mining_CameraZoom) and "Prestige > Mining >
    // view" (CameraZoomBonus): both add to the camera's base orthographic size to reveal more of
    // the map. Single scene use - editor-referenced per the project's object-reference convention
    // (see CLAUDE.md "Object references"), no singleton needed. Namespaced as CameraControl rather
    // than Camera to avoid shadowing UnityEngine.Camera for any file in this folder.
    public class CameraZoomController : MonoBehaviour
    {
        [SerializeField] private CinemachinePositionComposer cinemachineCamera;

        private float baseDistance;

        private void Awake()
        {
            cinemachineCamera = GetComponent<CinemachinePositionComposer>();
            if (cinemachineCamera == null)
            {
                Debug.LogError($"{nameof(CameraZoomController)} on {name} is missing its cinemachineCamera reference.");
                return;
            }
            baseDistance = cinemachineCamera.CameraDistance;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradeChanged);
            GameManager.EventService.Add<UpgradeLoadedEvent>(OnUpgradeLoaded);
            GameManager.EventService.Add<PrestigeUpgradePurchasedEvent>(OnPrestigeUpgradeChanged);
            ApplyZoom();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradeChanged);
            GameManager.EventService.Remove<UpgradeLoadedEvent>(OnUpgradeLoaded);
            GameManager.EventService.Remove<PrestigeUpgradePurchasedEvent>(OnPrestigeUpgradeChanged);
        }

        private void OnUpgradeChanged(UpgradePurchasedEvent evt)
        {
            if (evt.Definition.Effect == UpgradeEffect.Mining_CameraZoom) ApplyZoom();
        }

        private void OnUpgradeLoaded(UpgradeLoadedEvent evt)
        {
            if (evt.Definition.Effect == UpgradeEffect.Mining_CameraZoom) ApplyZoom();
        }

        private void OnPrestigeUpgradeChanged(PrestigeUpgradePurchasedEvent evt)
        {
            if (evt.Definition.Effect == PrestigeUpgradeEffect.Mining_CameraZoomBonus) ApplyZoom();
        }

        private void ApplyZoom()
        {
            float marketBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.CameraZoomBonus : 0f;
            float prestigeBonus = PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.CameraZoomBonus : 0f;

            cinemachineCamera.CameraDistance = baseDistance + marketBonus + prestigeBonus;
        }
    }
}
