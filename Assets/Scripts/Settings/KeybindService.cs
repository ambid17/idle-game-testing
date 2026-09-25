using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Settings
{
    // Rebindable gameplay actions. Persisted by name (see KeybindService), so reordering or
    // inserting entries here is safe for saved bindings.
    public enum GameAction
    {
        MoveLeft,
        MoveRight,
        FlyUp,
        MoveDown,
        InteractPrimary,
        InteractSecondary,
        InteractTertiary,
    }

    // Player-rebindable keyboard bindings for GameAction, persisted via PlayerPrefs like
    // SettingsService. Gameplay reads input through IsPressed/WasPressedThisFrame instead of
    // hardcoded Keyboard keys; UI.KeybindsUI (Options > Controls) drives Rebind. Escape and
    // backquote stay hardwired (pause/close and the dev panel) and can't be bound to an action,
    // so a bad rebind can never lock the player out of the menus.
    public class KeybindService
    {
        private const string KeyPrefix = "Keybinds.";

        private static readonly Dictionary<GameAction, Key> Defaults = new()
        {
            { GameAction.MoveLeft, Key.A },
            { GameAction.MoveRight, Key.D },
            { GameAction.FlyUp, Key.W },
            { GameAction.MoveDown, Key.S },
            { GameAction.InteractPrimary, Key.E },
            { GameAction.InteractSecondary, Key.R },
            { GameAction.InteractTertiary, Key.F },
        };

        private readonly Dictionary<GameAction, Key> bindings = new();

        private bool isCapturing;
        private int captureEndedFrame = -1;

        // True while UI.KeybindsUI is waiting for a key press, and for the rest of the frame a
        // capture ends in - so the Escape that cancels a capture (or any key that completes one)
        // isn't also read by PlayerController as "close panel / open pause menu", regardless of
        // which of the two Updates runs first that frame.
        public bool IsCapturingKey => isCapturing || captureEndedFrame == Time.frameCount;

        public KeybindService()
        {
            Load();
        }

        public Key GetKey(GameAction action) => bindings[action];

        public bool IsPressed(GameAction action)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[bindings[action]].isPressed;
        }

        public bool WasPressedThisFrame(GameAction action)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[bindings[action]].wasPressedThisFrame;
        }

        public string GetDisplayName(GameAction action) => GetDisplayName(bindings[action]);

        // Uses the active keyboard layout's name for the key where available (e.g. AZERTY
        // players see the letter printed on their keycap, not the US-layout Key enum name).
        public static string GetDisplayName(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return key.ToString();
            string displayName = keyboard[key].displayName;
            return string.IsNullOrEmpty(displayName) ? key.ToString() : displayName;
        }

        public static bool IsReserved(Key key) => key == Key.None || key == Key.Escape || key == Key.Backquote;

        // If another action already uses key, it takes this action's old key instead, so two
        // actions can never share a key and no action is ever left unbound.
        public void Rebind(GameAction action, Key key)
        {
            if (IsReserved(key))
            {
                Debug.LogError($"KeybindService: {key} is reserved and can't be bound to {action}.");
                return;
            }

            Key previousKey = bindings[action];
            if (previousKey == key) return;

            foreach (var other in new List<GameAction>(bindings.Keys))
            {
                if (other != action && bindings[other] == key)
                {
                    SetBinding(other, previousKey);
                }
            }

            SetBinding(action, key);
        }

        public void ResetToDefaults()
        {
            foreach (var pair in Defaults)
            {
                SetBinding(pair.Key, pair.Value);
            }
        }

        public void BeginCapture()
        {
            isCapturing = true;
        }

        public void EndCapture()
        {
            isCapturing = false;
            captureEndedFrame = Time.frameCount;
        }

        private void SetBinding(GameAction action, Key key)
        {
            bindings[action] = key;
            PlayerPrefs.SetString(KeyPrefix + action, key.ToString());
        }

        // Stored as the Key enum's name rather than its int value, so a future Input System
        // version renumbering Key can't silently remap saved bindings.
        private void Load()
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                Key key = Defaults[action];
                string saved = PlayerPrefs.GetString(KeyPrefix + action, null);
                if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out Key parsed) && !IsReserved(parsed))
                {
                    key = parsed;
                }
                bindings[action] = key;
            }
        }
    }
}
