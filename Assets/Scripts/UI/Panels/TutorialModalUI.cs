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

        // The press that brought a tutorial up (buying an upgrade with A, dismissing the previous
        // tutorial) mustn't also dismiss it, however many ways that one press is delivered -
        // PromptInput here, UI Submit on closeButton, ModalCloseRequestedEvent.
        private int shownFrame = -1;

        // Hides the root in Awake rather than Start: on a fresh game the CoreGoal tutorial is
        // dispatched during load, which can land before this component's Start. Hiding it in Start
        // would then leave it invisible but still IsOpen, holding ModalTracker open forever and
        // queueing every later tutorial behind it.
        private void Awake()
        {
            if (rendererRoot != null) rendererRoot.SetActive(false);
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.Entry.DisplayType != TutorialDisplayType.Modal) return;

            pending.Enqueue(evt.Entry);
            TryShowNext();
        }

        // Polls so a tutorial deferred behind another modal shows as soon as that modal closes.
        // Controller A dismisses the one on screen, which shows the next queued one (Close).
        // Escape / B get here through ModalBase; Space deliberately doesn't dismiss.
        private void Update()
        {
            if (IsOpen && PromptInput.WasGamepadSelectPressedThisFrame())
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

            rendererRoot.SetActive(true);
            shownFrame = Time.frameCount;
            SetOpened();
        }

        private static bool ShowsOverOtherModals(TutorialEntry entry) => entry.Id == TutorialId.RunModifierPick;

        public override void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;
            if (Time.frameCount == shownFrame) return;

            rendererRoot.SetActive(false);
            SetClosed();
            TryShowNext();
        }
    }
}
