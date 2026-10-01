using System.Collections.Generic;
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
    // Tutorials that fire while one is already showing (e.g. several triggers landing together)
    // queue up and show one after another. Tutorials also wait while any other ModalBase modal is
    // open (UI.ModalTracker) - except RunModifierPick, which explains the pick modal underneath.
    // Its Canvas should use a higher sort order than every other panel so it always renders on top.
    // See UI.Panels.WorldTutorialPopupUI for the world-anchored counterpart. Inherits ModalBase so
    // Escape can dismiss it without also closing a panel or opening the pause menu underneath.
    public class TutorialModalUI : ModalBase
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button closeButton;

        private readonly Queue<TutorialEntry> pending = new();

        // closeButton's label and its authored text ("Got it"), which gets the Space hint appended
        // - see TutorialAdvancePrompt.
        private TMP_Text closeLabel;
        private string closeLabelText;

        // Hides the root in Awake rather than Start: on a fresh game the CoreGoal tutorial is
        // dispatched during load, which can land before this component's Start. Hiding it in Start
        // would then leave it invisible but still IsOpen, holding ModalTracker open forever and
        // queueing every later tutorial behind it.
        private void Awake()
        {
            if (rendererRoot != null) rendererRoot.SetActive(false);

            if (closeButton != null) closeLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel == null)
            {
                Debug.LogError("TutorialModalUI.closeButton has no TMP_Text label.");
                return;
            }
            closeLabelText = closeLabel.text;
        }

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("TutorialModalUI.rendererRoot is not assigned.");
            if (titleLabel == null) Debug.LogError("TutorialModalUI.titleLabel is not assigned.");
            if (bodyLabel == null) Debug.LogError("TutorialModalUI.bodyLabel is not assigned.");
            if (closeButton == null) Debug.LogError("TutorialModalUI.closeButton is not assigned.");

            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameManager.EventService.Add<ShowTutorialEvent>(OnShowTutorial);
            GameManager.EventService.Add<InputSchemeChangedEvent>(OnInputSchemeChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);
            GameManager.EventService.Remove<InputSchemeChangedEvent>(OnInputSchemeChanged);
        }

        private void OnInputSchemeChanged(InputSchemeChangedEvent evt) => RefreshCloseLabel();

        private void RefreshCloseLabel()
        {
            if (closeLabel == null) return;
            closeLabel.text = TutorialAdvancePrompt.Label(closeLabelText);
        }

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.Entry.DisplayType != TutorialDisplayType.Modal) return;

            pending.Enqueue(evt.Entry);
            TryShowNext();
        }

        // Polls so a tutorial deferred behind another modal shows as soon as that modal closes.
        // Space dismisses the one on screen, which shows the next queued one (Close).
        private void Update()
        {
            if (IsOpen && TutorialAdvancePrompt.WasPressedThisFrame())
            {
                Close();
                return;
            }

            if (pending.Count > 0) TryShowNext();
        }

        private void TryShowNext()
        {
            if (IsOpen || pending.Count == 0) return;
            if (ModalTracker.IsAnyModalOpen && !ShowsOverOtherModals(pending.Peek())) return;
            if (rendererRoot == null || titleLabel == null || bodyLabel == null) return;

            var entry = pending.Dequeue();
            titleLabel.text = entry.Title;
            bodyLabel.text = entry.Body;
            RefreshCloseLabel();

            rendererRoot.SetActive(true);
            SetOpened();
        }

        private static bool ShowsOverOtherModals(TutorialEntry entry) => entry.Id == TutorialId.RunModifierPick;

        public override void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;

            rendererRoot.SetActive(false);
            SetClosed();
            TryShowNext();
        }
    }
}
