using System.Collections.Generic;
using Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace UI
{
    // Options > Controls tab. Clicking a KeybindRowUI's button starts a capture: the next
    // non-reserved key pressed is bound to that row's action (KeybindService swaps it off any
    // other action already using it), Escape cancels. Leaving the tab (or closing Options)
    // cancels an in-progress capture via OnDisable.
    public class KeybindsUI : MonoBehaviour
    {
        private const string WaitingText = "Press a key...";

        [SerializeField] private List<KeybindRowUI> rows = new();
        [SerializeField] private Button resetButton;

        private KeybindRowUI capturingRow;
        private int captureStartFrame;
        private KeybindService keybinds => GameManager.KeybindService;

        private void Start()
        {
            if (rows.Count == 0) Debug.LogError("KeybindsUI.rows is empty.");
            if (resetButton == null) Debug.LogError("KeybindsUI.resetButton is not assigned.");

            foreach (var row in rows)
            {
                var capturedRow = row;
                row.RebindButton.onClick.AddListener(() => BeginCapture(capturedRow));
            }
            if (resetButton != null) resetButton.onClick.AddListener(ResetToDefaults);

            RefreshAll();
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void OnDisable()
        {
            CancelCapture();
        }

        private void Update()
        {
            // The frame capture starts on may still report the key/click that started it (e.g.
            // Enter/Space submitting the focused button) as pressed this frame - skip it so that
            // press isn't immediately bound.
            if (capturingRow == null || Time.frameCount == captureStartFrame) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelCapture();
                return;
            }

            foreach (KeyControl keyControl in keyboard.allKeys)
            {
                if (keyControl == null || !keyControl.wasPressedThisFrame) continue;
                if (KeybindService.IsReserved(keyControl.keyCode)) continue;

                keybinds.Rebind(capturingRow.Action, keyControl.keyCode);
                EndCapture();
                RefreshAll();
                return;
            }
        }

        private void BeginCapture(KeybindRowUI row)
        {
            // Switching straight from one row's capture to another's - restore the first row's text.
            capturingRow = null;
            RefreshAll();
            capturingRow = row;
            captureStartFrame = Time.frameCount;
            keybinds.BeginCapture();
            row.SetKeyText(WaitingText);
        }

        private void CancelCapture()
        {
            if (capturingRow == null) return;
            EndCapture();
            RefreshAll();
        }

        private void EndCapture()
        {
            capturingRow = null;
            keybinds.EndCapture();
        }

        private void ResetToDefaults()
        {
            if (capturingRow != null) EndCapture();
            keybinds.ResetToDefaults();
            RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (var row in rows)
            {
                if (row == null || row == capturingRow) continue;
                row.SetKeyText(keybinds.GetDisplayName(row.Action));
            }
        }
    }
}
