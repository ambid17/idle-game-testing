using Audio;
using Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    // Click/hover sounds for any Selectable (Button, Toggle, Dropdown...). Add it next to the
    // Selectable, or bulk-add it with Tools > Audio > Add UIButtonSound To All Selectables.
    // Hover plays on mouse-enter, or on gamepad selection only: a mouse click also selects the
    // button, which would otherwise play the hover sound on top of the click.
    [RequireComponent(typeof(Selectable))]
    public class UIButtonSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private SoundId clickSound = SoundId.UIClick;
        [SerializeField] private SoundId hoverSound = SoundId.UIHover;

        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) PlayIfInteractable(clickSound);
        }

        public void OnSubmit(BaseEventData eventData) => PlayIfInteractable(clickSound);

        public void OnPointerEnter(PointerEventData eventData) => PlayIfInteractable(hoverSound);

        public void OnSelect(BaseEventData eventData)
        {
            if (GameManager.KeybindService.CurrentScheme == InputScheme.Gamepad) PlayIfInteractable(hoverSound);
        }

        private void PlayIfInteractable(SoundId id)
        {
            if (id != SoundId.None && selectable.IsInteractable()) GameManager.AudioService.Play(id);
        }
    }
}
