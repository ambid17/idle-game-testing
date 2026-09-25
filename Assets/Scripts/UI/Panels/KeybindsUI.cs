using System.Collections.Generic;
using Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    // Options > Controls tab. Clicking a KeybindRowUI's keyboard or gamepad button starts a
    // capture through KeybindService.StartRebind: the next non-reserved key/button pressed on that
    // device is bound to the row's action (KeybindService swaps it off any other action already
    // using it). Escape (keyboard) or Start (gamepad) cancels. Leaving the tab (or closing
    // Options) cancels an in-progress capture via OnDisable.
    public class KeybindsUI : MonoBehaviour
    {
        private const string WaitingKeyText = "Press a key...";
        private const string WaitingButtonText = "Press a button...";

        [SerializeField] private List<KeybindRowUI> rows = new();
        [SerializeField] private Button resetButton;

        private KeybindRowUI capturingRow;
        private InputScheme capturingScheme;
        // UI navigation (Submit in particular) is paused during a capture so the press that binds
        // a key/button can't also click the focused rebind button and start a new capture. It's
        // restored the frame after the capture ends, once that press is no longer "this frame".
        private int restoreNavigationAfterFrame = -1;
        private KeybindService keybinds => GameManager.KeybindService;

        private void Start()
        {
            if (rows.Count == 0) Debug.LogError("KeybindsUI.rows is empty.");
            if (resetButton == null) Debug.LogError("KeybindsUI.resetButton is not assigned.");

            foreach (var row in rows)
            {
                var capturedRow = row;
                row.RebindButton.onClick.AddListener(() => BeginCapture(capturedRow, InputScheme.KeyboardMouse));
                row.GamepadRebindButton.onClick.AddListener(() => BeginCapture(capturedRow, InputScheme.Gamepad));
                row.GamepadRebindButton.interactable = KeybindService.IsRebindable(row.Action, InputScheme.Gamepad);
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
            keybinds.CancelRebind();
            SetNavigationEnabled(true);
        }

        private void Update()
        {
            if (restoreNavigationAfterFrame >= 0 && Time.frameCount > restoreNavigationAfterFrame)
            {
                SetNavigationEnabled(true);
            }
        }

        private void BeginCapture(KeybindRowUI row, InputScheme scheme)
        {
            // Switching straight from one capture to another - end the first (restoring its text)
            // before this one's state is set, since cancelling runs OnCaptureFinished.
            keybinds.CancelRebind();

            capturingRow = row;
            capturingScheme = scheme;
            RefreshAll();
            row.SetKeyText(scheme, scheme == InputScheme.Gamepad ? WaitingButtonText : WaitingKeyText);

            restoreNavigationAfterFrame = -1;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            keybinds.StartRebind(row.Action, scheme, OnCaptureFinished);
        }

        private void OnCaptureFinished()
        {
            capturingRow = null;
            restoreNavigationAfterFrame = Time.frameCount;
            RefreshAll();
        }

        private void SetNavigationEnabled(bool enabled)
        {
            restoreNavigationAfterFrame = -1;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = enabled;
        }

        private void ResetToDefaults()
        {
            keybinds.ResetToDefaults();
            RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                foreach (var scheme in new[] { InputScheme.KeyboardMouse, InputScheme.Gamepad })
                {
                    if (row == capturingRow && scheme == capturingScheme) continue;
                    row.SetKeyText(scheme, keybinds.GetDisplayName(row.Action, scheme));
                }
            }
        }
    }
}
