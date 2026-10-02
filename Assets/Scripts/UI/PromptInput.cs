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
    // PromptSelectIcon shows the Select button's icon next to a prompt.
    public static class PromptInput
    {
        public static bool WasSelectPressedThisFrame()
        {
            var keybinds = GameManager.KeybindService;
            return !keybinds.IsCapturingKey && keybinds.WasPromptSelectPressedThisFrame();
        }

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
    }
}
