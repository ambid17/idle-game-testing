using Events;
using Player;
using TMPro;
using Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Screen-space overlay half of the tutorial popup system (Tutorial.TutorialManager). Shown for
    // any ShowTutorialEvent whose Entry.DisplayType is Modal - the core-goal tutorial and each
    // building's first-open tutorial, which sit over whatever panel (or nothing yet) is already on
    // screen.
    // Its Canvas should use a higher sort order than every other panel so it always renders on top.
    // See UI.Panels.WorldTutorialPopupUI for the world-anchored counterpart. Inherits ModalBase so
    // Escape can dismiss it without also closing a panel or opening the pause menu underneath.
    public class TutorialModalUI : ModalBase
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

        protected override void OnEnable()
        {
            base.OnEnable();
            GameManager.EventService.Add<ShowTutorialEvent>(OnShowTutorial);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.Entry.DisplayType != TutorialDisplayType.Modal) return;
            if (rendererRoot == null || titleLabel == null || bodyLabel == null) return;

            titleLabel.text = evt.Entry.Title;
            bodyLabel.text = evt.Entry.Body;

            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            Opened();
        }

        public override void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;

            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
            Closed();
        }
    }
}
