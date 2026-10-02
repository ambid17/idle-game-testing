using Events;
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
        private Image image;

        private void Awake() => image = GetComponent<Image>();

        private void OnEnable()
        {
            GameManager.EventService.Add<InputSchemeChangedEvent>(OnInputSchemeChanged);
            Refresh();
        }

        private void OnDisable() => GameManager.EventService.Remove<InputSchemeChangedEvent>(OnInputSchemeChanged);

        private void OnInputSchemeChanged(InputSchemeChangedEvent evt) => Refresh();

        private void Refresh() => image.sprite = PromptInput.SelectIcon;
    }
}
