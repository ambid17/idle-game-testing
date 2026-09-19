using Events;
using Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Debug-only cheat panel for manual testing - see DevPanelEconomyTab/ProgressionTab/
    // ResourceTab/TimeTutorialTab/SaveMapTab for what each tab does. Follows PauseMenuUI's
    // pattern exactly (rendererRoot toggle, InputBlocker while open, UICloseEvent to close), but
    // self-destructs outside the Editor/Development Builds so it never ships in a release build.
    public class DevPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TabGroupUI tabGroup;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                Destroy(gameObject);
                return;
            }

            if (rendererRoot == null) Debug.LogError("DevPanelUI.rendererRoot is not assigned.");
            if (tabGroup == null) Debug.LogError("DevPanelUI.tabGroup is not assigned.");
            if (closeButton == null) Debug.LogError("DevPanelUI.closeButton is not assigned.");

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<DevPanelOpenRequestedEvent>(Open);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DevPanelOpenRequestedEvent>(Open);
            GameManager.EventService.Remove<UICloseEvent>(Close);
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
