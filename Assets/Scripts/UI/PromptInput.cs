using Settings;
using UnityEngine;

namespace UI
{
    // One set of buttons for every "read this, then continue" prompt (TutorialModalUI,
    // WorldTutorialPopupUI, Panels.BuildingRevealTextUI): Select is Space / gamepad A, Close is
    // Escape / gamepad B. Read straight from the Menu action map rather than through UI
    // Submit/Cancel, so a prompt works whether or not its button holds the controller selection.
    // ModalBase prompts already get Close from PlayerController (ModalCloseRequestedEvent), so
    // they only poll Select; a prompt that isn't a ModalBase polls both.
    // On keyboard, Space dismissing these prompts felt wrong, so all three only close with
    // Escape and poll WasGamepadSelectPressedThisFrame for A.
    // PromptSelectIcon shows the button's icon next to a prompt.
    public static class PromptInput
    {
        public static bool WasSelectPressedThisFrame()
        {
            var keybinds = GameManager.KeybindService;
            return !keybinds.IsCapturingKey && keybinds.WasPromptSelectPressedThisFrame();
        }

        // Select on a controller only (A) - Space doesn't count.
        public static bool WasGamepadSelectPressedThisFrame() =>
            GameManager.KeybindService.CurrentScheme == InputScheme.Gamepad && WasSelectPressedThisFrame();

        public static bool WasClosePressedThisFrame()
        {
            var keybinds = GameManager.KeybindService;
            return !keybinds.IsCapturingKey && keybinds.WasPromptClosePressedThisFrame();
        }

        // Icon of whatever Select is bound to on the device the player is using.
        public static Sprite SelectIcon
        {
            get
            {
                var keybinds = GameManager.KeybindService;
                var scheme = keybinds.CurrentScheme;
                return GameManager.KeyIconDatabase.GetIcon(keybinds.GetPromptSelectPath(scheme), scheme);
            }
        }

        // Icon of whatever Close is bound to on the device the player is using.
        public static Sprite CloseIcon
        {
            get
            {
                var keybinds = GameManager.KeybindService;
                var scheme = keybinds.CurrentScheme;
                return GameManager.KeyIconDatabase.GetIcon(keybinds.GetPromptClosePath(scheme), scheme);
            }
        }
    }
}
