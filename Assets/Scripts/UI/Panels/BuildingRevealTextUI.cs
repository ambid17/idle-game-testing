using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Panels
{
    // Bottom-of-screen cinematic subtitle shown during Buildings.BuildingRevealCinematicPlayer's
    // spawn cinematic, explaining whichever building just materialized. Body text appears
    // immediately; a "continue" prompt appears after promptDelaySeconds, and from then on clicking
    // anywhere or pressing Escape (UI.PromptInput - A / B on a controller; Space deliberately
    // doesn't) dismisses it and invokes the caller's callback. Purely a display panel -
    // BuildingRevealCinematicPlayer already owns Player.InputBlocker for the whole cinematic, so
    // unlike TutorialModalUI/WorldTutorialPopupUI this isn't a ModalBase, and it reads the prompt
    // buttons itself rather than through controller focus (it has no GamepadFocus). The prompt's
    // button icon is a UI.PromptSelectIcon in the scene.
    public class BuildingRevealTextUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private GameObject continuePrompt;
        [SerializeField] private Button clickCatcher;

        private Action onDismissed;
        private Coroutine promptCoroutine;
        private bool canDismiss;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("BuildingRevealTextUI.rendererRoot is not assigned.");
            if (bodyLabel == null) Debug.LogError("BuildingRevealTextUI.bodyLabel is not assigned.");
            if (continuePrompt == null) Debug.LogError("BuildingRevealTextUI.continuePrompt is not assigned.");
            if (clickCatcher == null) Debug.LogError("BuildingRevealTextUI.clickCatcher is not assigned.");

            if (clickCatcher != null) clickCatcher.onClick.AddListener(Dismiss);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        public void Show(string body, float promptDelaySeconds, Action onDismissedCallback)
        {
            onDismissed = onDismissedCallback;
            canDismiss = false;
            if (bodyLabel != null) bodyLabel.text = body;
            if (continuePrompt != null) continuePrompt.SetActive(false);
            if (clickCatcher != null) clickCatcher.interactable = false;
            if (rendererRoot != null) rendererRoot.SetActive(true);

            promptCoroutine = StartCoroutine(ShowPromptAfterDelay(promptDelaySeconds));
        }

        private IEnumerator ShowPromptAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (continuePrompt != null) continuePrompt.SetActive(true);
            if (clickCatcher != null) clickCatcher.interactable = true;
            canDismiss = true;
        }

        private void Update()
        {
            if (!canDismiss) return;
            if (PromptInput.WasGamepadSelectPressedThisFrame() || PromptInput.WasClosePressedThisFrame()) Dismiss();
        }

        private void Dismiss()
        {
            if (!canDismiss) return;
            canDismiss = false;

            if (promptCoroutine != null) StopCoroutine(promptCoroutine);
            if (rendererRoot != null) rendererRoot.SetActive(false);

            var callback = onDismissed;
            onDismissed = null;
            callback?.Invoke();
        }
    }
}
