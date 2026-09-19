using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Panels
{
    // Bottom-of-screen cinematic subtitle shown during Buildings.BuildingRevealCinematicPlayer's
    // spawn cinematic, explaining whichever building just materialized. Body text appears
    // immediately; a "click to continue" prompt appears after promptDelaySeconds, and clicking
    // anywhere afterward dismisses it and invokes the caller's callback. Purely a display panel -
    // BuildingRevealCinematicPlayer already owns Player.InputBlocker for the whole cinematic, so
    // unlike TutorialModalUI/WorldTutorialPopupUI this isn't a ModalBase (no independent
    // input-blocking or Escape-to-skip).
    public class BuildingRevealTextUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private GameObject continuePrompt;
        [SerializeField] private Button clickCatcher;

        private Action onDismissed;
        private Coroutine promptCoroutine;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("BuildingRevealTextUI.rendererRoot is not assigned.");
            if (bodyLabel == null) Debug.LogError("BuildingRevealTextUI.bodyLabel is not assigned.");
            if (continuePrompt == null) Debug.LogError("BuildingRevealTextUI.continuePrompt is not assigned.");
            if (clickCatcher == null) Debug.LogError("BuildingRevealTextUI.clickCatcher is not assigned.");

            if (clickCatcher != null) clickCatcher.onClick.AddListener(OnClicked);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        public void Show(string body, float promptDelaySeconds, Action onDismissedCallback)
        {
            onDismissed = onDismissedCallback;
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
        }

        private void OnClicked()
        {
            if (promptCoroutine != null) StopCoroutine(promptCoroutine);
            if (rendererRoot != null) rendererRoot.SetActive(false);

            var callback = onDismissed;
            onDismissed = null;
            callback?.Invoke();
        }
    }
}
