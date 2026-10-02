using Events;
using Settings;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Put on an Image beside a prompt's button or "continue" text: shows the icon of the
    // PromptInput Select button (Space / gamepad A) for the device the player is using, and swaps
    // it when they switch between keyboard and controller.
    [RequireComponent(typeof(Image))]
    public class PromptSelectIcon : MonoBehaviour
    {
        // For prompts Space doesn't dismiss (the tutorial popups, the building reveal): shows the
        // Close key (Escape) on keyboard instead. A controller still shows Select (A).
        [SerializeField] private bool closeKeyOnKeyboard;

        private Image image;

        private void Awake() => image = GetComponent<Image>();

        private void OnEnable()
        {
            GameManager.EventService.Add<InputSchemeChangedEvent>(OnInputSchemeChanged);
            Refresh();
        }

        private void OnDisable() => GameManager.EventService.Remove<InputSchemeChangedEvent>(OnInputSchemeChanged);

        private void OnInputSchemeChanged(InputSchemeChangedEvent evt) => Refresh();

        private void Refresh()
        {
            bool showClose = closeKeyOnKeyboard && GameManager.KeybindService.CurrentScheme == InputScheme.KeyboardMouse;
            image.sprite = showClose ? PromptInput.CloseIcon : PromptInput.SelectIcon;
        }
    }
}
