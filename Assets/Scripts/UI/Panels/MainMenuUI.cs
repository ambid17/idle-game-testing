using System.Collections;
using Events;
using Persistence;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    // Launch flow: splash art (held for a moment, skippable with any key/click/button) fades out
    // to reveal the main menu - Continue / New Game / Options / Quit over its own background.
    // Lives in the gameplay scene as a full-screen overlay rather than a separate scene, so it
    // reuses OptionsUI and the already-loaded singletons; the game loads and keeps running
    // underneath (same "menus don't pause" scope as PauseMenuUI), with player input blocked until
    // the menu is dismissed. Anything that pops up at load (offline earnings, the first tutorial)
    // sits below this panel in the Canvas and is simply waiting there once the player starts.
    public class MainMenuUI : MonoBehaviour
    {
        // Covers the splash too. Read by PlayerController so Escape/B can't close popups hidden
        // underneath the menu.
        public static bool IsOpen { get; private set; }

        // Set right before New Game reloads the scene, so the fresh scene drops straight into
        // gameplay instead of showing the splash and menu a second time.
        private static bool skipNextOpen;

        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private CanvasGroup splashGroup;
        // Holds the title and buttons (and their GamepadFocus). Activated only once the splash
        // ends, so it takes controller focus after any popup that opened underneath during load.
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private TMP_Text newGameLabel;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private OptionsUI optionsUI;
        [SerializeField] private float splashHoldSeconds = 2.5f;
        [SerializeField] private float splashFadeSeconds = 0.6f;
        [SerializeField] private bool showInEditor = true;

        private const string NewGameText = "New Game";
        private const string NewGameConfirmText = "Erase save?";

        private bool hasSaveFile;
        private bool awaitingNewGameConfirm;

        private void Awake()
        {
            if (rendererRoot == null) Debug.LogError("MainMenuUI.rendererRoot is not assigned.");
            if (splashGroup == null) Debug.LogError("MainMenuUI.splashGroup is not assigned.");
            if (menuRoot == null) Debug.LogError("MainMenuUI.menuRoot is not assigned.");
            if (continueButton == null) Debug.LogError("MainMenuUI.continueButton is not assigned.");
            if (newGameButton == null) Debug.LogError("MainMenuUI.newGameButton is not assigned.");
            if (newGameLabel == null) Debug.LogError("MainMenuUI.newGameLabel is not assigned.");
            if (optionsButton == null) Debug.LogError("MainMenuUI.optionsButton is not assigned.");
            if (quitButton == null) Debug.LogError("MainMenuUI.quitButton is not assigned.");
            if (optionsUI == null) Debug.LogError("MainMenuUI.optionsUI is not assigned.");

            continueButton.onClick.AddListener(Close);
            newGameButton.onClick.AddListener(OnNewGameClicked);
            optionsButton.onClick.AddListener(optionsUI.Open);
            quitButton.onClick.AddListener(Quit);

            bool show = !skipNextOpen && (showInEditor || !Application.isEditor);
            skipNextOpen = false;

            // Shown from Awake (not Start) so the game world never flashes for a frame first.
            IsOpen = show;
            rendererRoot.SetActive(show);
            menuRoot.SetActive(false);
            if (!show) return;

            InputBlocker.SetBlocked(true);
            splashGroup.gameObject.SetActive(true);
            splashGroup.alpha = 1f;
        }

        private void Start()
        {
            if (!IsOpen) return;

            hasSaveFile = SaveService.Instance.HasSaveFile;
            continueButton.gameObject.SetActive(hasSaveFile);
            newGameLabel.text = NewGameText;
            StartCoroutine(PlaySplash());
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<MainMenuBackRequestedEvent>(OnBackRequested);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<MainMenuBackRequestedEvent>(OnBackRequested);
        }

        // A scene reload (or leaving Play Mode with domain reload off) must not leave the static
        // flag or the input block behind.
        private void OnDestroy()
        {
            if (!IsOpen) return;
            IsOpen = false;
            InputBlocker.SetBlocked(false);
        }

        // Unscaled time throughout, so DigFeedback's hit-stop or any future pause can't stall it.
        private IEnumerator PlaySplash()
        {
            // Skip the first frame, so the click/keypress that launched the game doesn't count.
            yield return null;

            for (float t = 0f; t < splashHoldSeconds && !WasSkipPressed(); t += Time.unscaledDeltaTime)
            {
                yield return null;
            }

            menuRoot.SetActive(true);

            for (float t = 0f; t < splashFadeSeconds; t += Time.unscaledDeltaTime)
            {
                splashGroup.alpha = 1f - t / splashFadeSeconds;
                yield return null;
            }

            splashGroup.gameObject.SetActive(false);
        }

        private static bool WasSkipPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            var gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        // With no save there's nothing to lose, so New Game just starts. With one, the first
        // click arms a confirmation (the label changes) and the second wipes the save and reloads.
        private void OnNewGameClicked()
        {
            if (!hasSaveFile)
            {
                Close();
                return;
            }

            if (!awaitingNewGameConfirm)
            {
                awaitingNewGameConfirm = true;
                newGameLabel.text = NewGameConfirmText;
                return;
            }

            SaveService.Instance.DeleteSaveData();
            skipNextOpen = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnBackRequested()
        {
            optionsUI.Close();
            awaitingNewGameConfirm = false;
            newGameLabel.text = NewGameText;
        }

        private void Close()
        {
            optionsUI.Close();
            IsOpen = false;
            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
