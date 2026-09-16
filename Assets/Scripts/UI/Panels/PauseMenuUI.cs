using Events;
using Persistence;
using Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Escape-key pause menu. PlayerController dispatches PauseMenuOpenRequestedEvent on Escape,
    // but only when nothing else was already blocking input (see PlayerController.Update) - so
    // this never has to fight another modal for the screen. Follows ControlCenterUI's
    // InputBlocker-modal pattern: blocks player input while open, and closes via the same
    // UICloseEvent Escape already dispatches to close every other panel, so a second Escape
    // press (or any panel-close trigger) resumes play. Per the resolved scope, this only shows a
    // menu - it does not actually pause game systems (Time.timeScale, automation, etc.).
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private OptionsUI optionsUI;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("PauseMenuUI.rendererRoot is not assigned.");
            if (resumeButton == null) Debug.LogError("PauseMenuUI.resumeButton is not assigned.");
            if (optionsButton == null) Debug.LogError("PauseMenuUI.optionsButton is not assigned.");
            if (quitButton == null) Debug.LogError("PauseMenuUI.quitButton is not assigned.");
            if (optionsUI == null) Debug.LogError("PauseMenuUI.optionsUI is not assigned.");

            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PauseMenuOpenRequestedEvent>(Open);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PauseMenuOpenRequestedEvent>(Open);
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
            if (optionsUI != null) optionsUI.Close();
            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
        }

        private void OpenOptions()
        {
            if (optionsUI != null) optionsUI.Open();
        }

        // Per the resolved scope: quitting always saves first so no progress is lost.
        private void Quit()
        {
            SaveService.Instance.Save();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
