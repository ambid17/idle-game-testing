using System;
using System.Collections.Generic;
using Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Settings
{
    // Rebindable gameplay actions. Each entry's name must match a Button action in the
    // GameControls.inputactions "Gameplay" map - KeybindService looks them up by name.
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

    // Matches the asset's two control schemes (see KeybindService.GroupFor).
    public enum InputScheme
    {
        KeyboardMouse,
        Gamepad,
    }

    // Player-rebindable bindings for GameAction on top of GameControls.inputactions (assigned on
    // GameManager), for both keyboard and gamepad. Binding overrides persist as one JSON blob in
    // PlayerPrefs. Gameplay reads input through IsPressed/WasPressedThisFrame; UI.KeybindsUI
    // (Options > Controls) drives StartRebind.
    //
    // Hardwired, never rebindable, so a bad rebind can never lock the player out of the menus:
    // Pause (Escape / Start), Back (gamepad B), DevPanel (backquote / Select), and gamepad
    // movement (left stick + d-pad).
    public class KeybindService
    {
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        private const string OverridesKey = "Keybinds.Overrides";
        // Pre-InputActionAsset saves stored one Key enum name per action under this prefix.
        private const string LegacyKeyPrefix = "Keybinds.";

        private static readonly string[] ReservedKeyboardPaths =
        {
            "<Keyboard>/escape", "<Keyboard>/backquote", "<Keyboard>/anyKey",
        };

        // Back, Pause, DevPanel, and the fixed movement controls. Gamepad rebinds are limited to
        // the face buttons, shoulders, triggers and stick clicks.
        private static readonly string[] ReservedGamepadPaths =
        {
            "<Gamepad>/start", "<Gamepad>/select", "<Gamepad>/buttonEast",
            "<Gamepad>/dpad", "<Gamepad>/dpad/*",
            "<Gamepad>/leftStick", "<Gamepad>/leftStick/*",
            "<Gamepad>/rightStick", "<Gamepad>/rightStick/*",
        };

        private readonly InputActionAsset asset;
        private readonly Dictionary<GameAction, InputAction> actions = new();
        private readonly InputAction pauseAction;
        private readonly InputAction backAction;
        private readonly InputAction devPanelAction;
        private readonly InputAction panViewAction;
        private readonly InputAction zoomViewAction;
        private readonly InputAction tabNextAction;
        private readonly InputAction tabPreviousAction;

        private InputActionRebindingExtensions.RebindingOperation rebinding;
        private int captureEndedFrame = -1;

        public InputScheme CurrentScheme { get; private set; } = InputScheme.KeyboardMouse;

        // True while a rebind is waiting for input, and for the rest of the frame one ends in -
        // so the Escape/Start that cancels a capture (or any press that completes one) isn't
        // also read by PlayerController as "close panel / open pause menu", regardless of which
        // Update runs first that frame.
        public bool IsCapturingKey => rebinding != null || captureEndedFrame == Time.frameCount;

        public KeybindService(InputActionAsset asset)
        {
            this.asset = asset;

            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                actions[action] = asset.FindAction("Gameplay/" + action, throwIfNotFound: true);
            }
            pauseAction = asset.FindAction("Gameplay/Pause", throwIfNotFound: true);
            backAction = asset.FindAction("Gameplay/Back", throwIfNotFound: true);
            devPanelAction = asset.FindAction("Gameplay/DevPanel", throwIfNotFound: true);
            panViewAction = asset.FindAction("Menu/PanView", throwIfNotFound: true);
            zoomViewAction = asset.FindAction("Menu/ZoomView", throwIfNotFound: true);
            tabNextAction = asset.FindAction("Menu/TabNext", throwIfNotFound: true);
            tabPreviousAction = asset.FindAction("Menu/TabPrevious", throwIfNotFound: true);

            Load();
            asset.FindActionMap("Gameplay", throwIfNotFound: true).Enable();
            asset.FindActionMap("Menu", throwIfNotFound: true).Enable();

            InputSystem.onActionChange += OnActionChange;
        }

        public bool IsPressed(GameAction action) => actions[action].IsPressed();

        public bool WasPressedThisFrame(GameAction action) => actions[action].WasPressedThisFrame();

        public bool WasPausePressedThisFrame() => pauseAction.WasPressedThisFrame();
        public bool WasBackPressedThisFrame() => backAction.WasPressedThisFrame();
        public bool WasDevPanelPressedThisFrame() => devPanelAction.WasPressedThisFrame();

        // Controller-only menu helpers (skill tree pan/zoom, tab cycling).
        public Vector2 PanViewInput => panViewAction.ReadValue<Vector2>();
        public float ZoomViewInput => zoomViewAction.ReadValue<float>();
        public bool WasTabNextPressedThisFrame() => tabNextAction.WasPressedThisFrame();
        public bool WasTabPreviousPressedThisFrame() => tabPreviousAction.WasPressedThisFrame();

        public static string GroupFor(InputScheme scheme) => scheme == InputScheme.Gamepad ? GamepadGroup : KeyboardGroup;

        // Gamepad movement is always the left stick + d-pad.
        public static bool IsRebindable(GameAction action, InputScheme scheme) =>
            scheme == InputScheme.KeyboardMouse
            || action is not (GameAction.MoveLeft or GameAction.MoveRight or GameAction.FlyUp or GameAction.MoveDown);

        // Name of the control bound for the device the player is currently using.
        public string GetDisplayName(GameAction action) => GetDisplayName(action, CurrentScheme);

        public string GetDisplayName(GameAction action, InputScheme scheme)
        {
            int index = GetBindingIndex(action, scheme);
            return index < 0 ? "-" : actions[action].GetBindingDisplayString(index);
        }

        // Effective control path (e.g. "<Gamepad>/buttonSouth"), for KeyIconDatabase lookups.
        public string GetBindingPath(GameAction action, InputScheme scheme)
        {
            int index = GetBindingIndex(action, scheme);
            return index < 0 ? null : actions[action].bindings[index].effectivePath;
        }

        // Waits for the next non-reserved press on scheme's device and binds it to action. If
        // another action already uses that control it takes this action's old one instead, so two
        // actions never share a control and none is ever left unbound. onFinished runs on both
        // completion and cancel (Escape for keyboard, Start for gamepad).
        public void StartRebind(GameAction action, InputScheme scheme, Action onFinished)
        {
            if (!IsRebindable(action, scheme))
            {
                Debug.LogError($"KeybindService: {action} can't be rebound for {scheme}.");
                return;
            }

            CancelRebind();

            var inputAction = actions[action];
            int index = GetBindingIndex(action, scheme);
            string previousPath = inputAction.bindings[index].effectivePath;
            bool isGamepad = scheme == InputScheme.Gamepad;

            // Interactive rebinding requires the action to be disabled while it listens.
            inputAction.Disable();
            var operation = inputAction.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath(isGamepad ? "<Gamepad>" : "<Keyboard>")
                .WithCancelingThrough(isGamepad ? "<Gamepad>/start" : "<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f);
            foreach (var path in isGamepad ? ReservedGamepadPaths : ReservedKeyboardPaths)
            {
                operation.WithControlsExcluding(path);
            }

            operation
                .OnComplete(_ =>
                {
                    SwapConflictingBinding(action, scheme, previousPath);
                    FinishRebind(inputAction, onFinished);
                })
                .OnCancel(_ => FinishRebind(inputAction, onFinished));

            rebinding = operation;
            operation.Start();
        }

        public void CancelRebind()
        {
            rebinding?.Cancel();
        }

        public void ResetToDefaults()
        {
            CancelRebind();
            asset.RemoveAllBindingOverrides();
            Save();
        }

        private void FinishRebind(InputAction inputAction, Action onFinished)
        {
            rebinding.Dispose();
            rebinding = null;
            captureEndedFrame = Time.frameCount;
            inputAction.Enable();
            Save();
            onFinished?.Invoke();
        }

        private void SwapConflictingBinding(GameAction action, InputScheme scheme, string previousPath)
        {
            string newPath = GetBindingPath(action, scheme);
            foreach (var pair in actions)
            {
                if (pair.Key == action || !IsRebindable(pair.Key, scheme)) continue;

                int otherIndex = GetBindingIndex(pair.Key, scheme);
                if (otherIndex < 0) continue;
                if (string.Equals(pair.Value.bindings[otherIndex].effectivePath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    pair.Value.ApplyBindingOverride(otherIndex, previousPath);
                }
            }
        }

        // The first binding in the scheme's group is the one shown and rebound. The asset lists
        // the gamepad d-pad before the left stick for movement actions.
        private int GetBindingIndex(GameAction action, InputScheme scheme)
        {
            var bindings = actions[action].bindings;
            string group = GroupFor(scheme);
            for (int i = 0; i < bindings.Count; i++)
            {
                if (InputBinding.MaskByGroup(group).Matches(bindings[i])) return i;
            }
            return -1;
        }

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed || obj is not InputAction action) return;

            var device = action.activeControl?.device;
            InputScheme scheme;
            if (device is Gamepad) scheme = InputScheme.Gamepad;
            else if (device is Keyboard || device is Pointer) scheme = InputScheme.KeyboardMouse;
            else return;

            if (scheme == CurrentScheme) return;
            CurrentScheme = scheme;
            GameManager.EventService.Dispatch(new InputSchemeChangedEvent(scheme));
        }

        private void Save()
        {
            PlayerPrefs.SetString(OverridesKey, asset.SaveBindingOverridesAsJson());
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(OverridesKey, null);
            if (!string.IsNullOrEmpty(json))
            {
                asset.LoadBindingOverridesFromJson(json);
            }

            MigrateLegacyBindings();
        }

        // One-time move of pre-InputActionAsset keyboard bindings (Key enum names) into binding
        // overrides. Needs a keyboard to turn a Key into a control path, so without one the old
        // entries are left in place for a later launch.
        private void MigrateLegacyBindings()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool migrated = false;
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                string legacyKey = LegacyKeyPrefix + action;
                if (!PlayerPrefs.HasKey(legacyKey)) continue;

                string saved = PlayerPrefs.GetString(legacyKey);
                PlayerPrefs.DeleteKey(legacyKey);
                migrated = true;

                if (!Enum.TryParse(saved, out Key key) || key == Key.None) continue;
                string path = "<Keyboard>/" + keyboard[key].name;
                int index = GetBindingIndex(action, InputScheme.KeyboardMouse);
                if (actions[action].bindings[index].path != path)
                {
                    actions[action].ApplyBindingOverride(index, path);
                }
            }

            if (migrated) Save();
        }
    }
}
