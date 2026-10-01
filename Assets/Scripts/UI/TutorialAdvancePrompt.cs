using Settings;

namespace UI
{
    // Shared by TutorialModalUI and WorldTutorialPopupUI: Space dismisses the tutorial on screen
    // (KeybindService.WasTutorialAdvancePressedThisFrame), and their "Got it" button says so
    // while the player is on keyboard. On a controller the button is already focused and A
    // presses it, so the label is left plain.
    public static class TutorialAdvancePrompt
    {
        private const string KeyboardHint = " <size=65%>[SPACE]</size>";

        public static bool WasPressedThisFrame()
        {
            var keybinds = GameManager.KeybindService;
            return !keybinds.IsCapturingKey && keybinds.WasTutorialAdvancePressedThisFrame();
        }

        public static string Label(string baseLabel) =>
            GameManager.KeybindService.CurrentScheme == InputScheme.KeyboardMouse ? baseLabel + KeyboardHint : baseLabel;
    }
}
