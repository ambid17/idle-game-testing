using System;
using Events;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    // The story's choice modal (Story.SealChamber): a title, a body and up to two options plus a
    // "not now" button - take or leave a Keystone, Release or Reseal at the Vault. Opened with
    // StoryChoiceRequestedEvent; Escape (via ModalBase) is the same as the cancel button. Blocks
    // player input while open. Per the UI panel rule this controller stays enabled on the panel
    // root and only toggles the child rendererRoot.
    public class StoryChoiceUI : ModalBase
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button optionAButton;
        [SerializeField] private TMP_Text optionALabel;
        [SerializeField] private Button optionBButton;
        [SerializeField] private TMP_Text optionBLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;

        private Action onOptionA;
        private Action onOptionB;
        // The press that opened this (Interact closing the mural dialog) mustn't also pick an option.
        private int openedFrame = -1;

        private void Awake()
        {
            if (rendererRoot == null) Debug.LogError("StoryChoiceUI.rendererRoot is not assigned.");
            if (titleLabel == null) Debug.LogError("StoryChoiceUI.titleLabel is not assigned.");
            if (bodyLabel == null) Debug.LogError("StoryChoiceUI.bodyLabel is not assigned.");
            if (optionAButton == null) Debug.LogError("StoryChoiceUI.optionAButton is not assigned.");
            if (optionALabel == null) Debug.LogError("StoryChoiceUI.optionALabel is not assigned.");
            if (optionBButton == null) Debug.LogError("StoryChoiceUI.optionBButton is not assigned.");
            if (optionBLabel == null) Debug.LogError("StoryChoiceUI.optionBLabel is not assigned.");
            if (cancelButton == null) Debug.LogError("StoryChoiceUI.cancelButton is not assigned.");
            if (cancelLabel == null) Debug.LogError("StoryChoiceUI.cancelLabel is not assigned.");

            optionAButton.onClick.AddListener(() => Choose(onOptionA));
            optionBButton.onClick.AddListener(() => Choose(onOptionB));
            cancelButton.onClick.AddListener(() => Choose(null));
            rendererRoot.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameManager.EventService.Add<StoryChoiceRequestedEvent>(Show);
        }

        protected override void OnDisable()
        {
            GameManager.EventService.Remove<StoryChoiceRequestedEvent>(Show);
            if (IsOpen) InputBlocker.SetBlocked(false);
            base.OnDisable();
        }

        private void Show(StoryChoiceRequestedEvent evt)
        {
            if (IsOpen) return;

            titleLabel.text = evt.Title;
            bodyLabel.text = evt.Body;
            cancelLabel.text = evt.CancelLabel;
            optionALabel.text = evt.OptionALabel;
            onOptionA = evt.OnOptionA;

            bool hasOptionB = evt.OnOptionB != null;
            optionBButton.gameObject.SetActive(hasOptionB);
            optionBButton.interactable = evt.OptionBEnabled;
            optionBLabel.text = evt.OptionBLabel;
            onOptionB = evt.OnOptionB;

            openedFrame = Time.frameCount;
            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            SetOpened();

            // Focus the safe button, so a stray Submit never makes a permanent choice.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        }

        public override void Close()
        {
            if (!IsOpen) return;

            rendererRoot.SetActive(false);
            InputBlocker.SetBlocked(false);
            SetClosed();
        }

        private void Choose(Action chosen)
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;

            Close();
            chosen?.Invoke();
        }
    }
}
