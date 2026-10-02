using System.Collections.Generic;
using Audio;
using Events;
using Player;
using Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    // Generic click-through conversation box: portrait, speaker name, and a typewriter body line.
    // Anyone opens it with DialogRequestedEvent and hears back via DialogFinishedEvent (same
    // ConversationId). Clicking the box, Interact, Space or Enter first finishes the line being
    // typed, then advances; Escape (via ModalBase) skips the rest. Its Canvas should sort above
    // the building panels so a shop can talk over its own UI.
    public class DialogUI : ModalBase
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("Shown once the current line has finished typing (e.g. a bouncing arrow).")]
        [SerializeField] private GameObject continueIndicator;
        [Tooltip("Invisible button covering the dialog box - click anywhere to advance. Also takes gamepad focus so A/Submit advances.")]
        [SerializeField] private Button advanceButton;
        [SerializeField] private float charactersPerSecond = 45f;
        [Tooltip("A blip plays every this many revealed characters.")]
        [SerializeField] private int charactersPerBlip = 3;

        private readonly List<DialogLine> lines = new();
        private string conversationId;
        private int lineIndex;
        private float visibleCharacters;
        private int lastBlipCharacter;
        private int openedFrame = -1;
        // One press can arrive twice in a frame - gamepad A is both UI Submit on advanceButton and
        // InteractPrimary in Update - and must only count once, or it finishes the line being
        // typed and skips to the next one in the same press.
        private int advancedFrame = -1;

        private bool IsTyping => bodyLabel.maxVisibleCharacters < bodyLabel.textInfo.characterCount;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("DialogUI.rendererRoot is not assigned.");
            if (portraitImage == null) Debug.LogError("DialogUI.portraitImage is not assigned.");
            if (speakerLabel == null) Debug.LogError("DialogUI.speakerLabel is not assigned.");
            if (bodyLabel == null) Debug.LogError("DialogUI.bodyLabel is not assigned.");
            if (continueIndicator == null) Debug.LogError("DialogUI.continueIndicator is not assigned.");
            if (advanceButton == null) Debug.LogError("DialogUI.advanceButton is not assigned.");

            advanceButton.onClick.AddListener(Advance);
            rendererRoot.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameManager.EventService.Add<DialogRequestedEvent>(OnDialogRequested);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameManager.EventService.Remove<DialogRequestedEvent>(OnDialogRequested);
        }

        private void OnDialogRequested(DialogRequestedEvent evt)
        {
            if (evt.Lines == null || evt.Lines.Count == 0)
            {
                Debug.LogError($"DialogUI: conversation '{evt.ConversationId}' has no lines.");
                GameManager.EventService.Dispatch(new DialogFinishedEvent(evt.ConversationId));
                return;
            }

            // A new conversation replaces one still open - finish the old one so its requester isn't left waiting.
            if (IsOpen) Finish();

            conversationId = evt.ConversationId;
            lines.Clear();
            lines.AddRange(evt.Lines);
            lineIndex = 0;
            // The key press that started this conversation mustn't also skip its first line.
            openedFrame = Time.frameCount;

            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            SetOpened();
            ShowLine();

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(advanceButton.gameObject);
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (IsTyping)
            {
                visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
                int shown = Mathf.Min(Mathf.FloorToInt(visibleCharacters), bodyLabel.textInfo.characterCount);
                bodyLabel.maxVisibleCharacters = shown;
                if (shown - lastBlipCharacter >= charactersPerBlip)
                {
                    lastBlipCharacter = shown;
                    GameManager.AudioService.Play(SoundId.DialogBlip);
                }
                continueIndicator.SetActive(!IsTyping);
            }

            if (Time.frameCount == openedFrame) return;

            var keyboard = Keyboard.current;
            bool keyAdvance = GameManager.KeybindService.WasPressedThisFrame(GameAction.InteractPrimary)
                || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));
            if (keyAdvance) Advance();
        }

        private void Advance()
        {
            if (!IsOpen || Time.frameCount == openedFrame || Time.frameCount == advancedFrame) return;
            advancedFrame = Time.frameCount;

            if (IsTyping)
            {
                bodyLabel.maxVisibleCharacters = bodyLabel.textInfo.characterCount;
                continueIndicator.SetActive(true);
                return;
            }

            lineIndex++;
            if (lineIndex >= lines.Count)
            {
                Close();
                return;
            }
            ShowLine();
        }

        private void ShowLine()
        {
            var line = lines[lineIndex];
            speakerLabel.text = line.Speaker;
            portraitImage.sprite = line.Portrait;
            portraitImage.enabled = line.Portrait != null;

            bodyLabel.text = line.Text;
            bodyLabel.maxVisibleCharacters = 0;
            // Populate textInfo now so IsTyping knows the real character count this frame.
            bodyLabel.ForceMeshUpdate();
            visibleCharacters = 0f;
            lastBlipCharacter = 0;
            continueIndicator.SetActive(false);
        }

        public override void Close()
        {
            if (!IsOpen) return;
            Finish();
        }

        private void Finish()
        {
            string finishedId = conversationId;
            conversationId = null;
            lines.Clear();

            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
            SetClosed();
            GameManager.EventService.Dispatch(new DialogFinishedEvent(finishedId));
        }
    }
}
