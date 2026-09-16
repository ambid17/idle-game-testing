using Events;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Screen-space overlay half of the tutorial popup system (Tutorial.TutorialManager). Shown for
    // any ShowTutorialEvent with no WorldPosition - the core-goal tutorial and each building's
    // first-open tutorial, which sit over whatever panel (or nothing yet) is already on screen.
    // Its Canvas should use a higher sort order than every other panel so it always renders on top.
    // See UI.Panels.WorldTutorialPopupUI for the world-anchored counterpart.
    public class TutorialModalUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("TutorialModalUI.rendererRoot is not assigned.");
            if (titleLabel == null) Debug.LogError("TutorialModalUI.titleLabel is not assigned.");
            if (bodyLabel == null) Debug.LogError("TutorialModalUI.bodyLabel is not assigned.");
            if (closeButton == null) Debug.LogError("TutorialModalUI.closeButton is not assigned.");

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<ShowTutorialEvent>(OnShowTutorial);
        private void OnDisable() => GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.WorldPosition.HasValue) return;
            if (rendererRoot == null || titleLabel == null || bodyLabel == null) return;

            titleLabel.text = evt.Entry.Title;
            bodyLabel.text = evt.Entry.Body;

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
