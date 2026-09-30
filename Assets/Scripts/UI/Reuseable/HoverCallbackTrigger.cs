using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Reuseable
{
    // Reports hover/controller-selection changes to a callback, for callers that drive one shared
    // tooltip from many elements (e.g. buttons inside a masked ScrollRect, where a per-button
    // tooltip child would be clipped). Unlike EventTrigger it only implements these four handlers,
    // so it doesn't swallow the drag/scroll events a parent ScrollRect needs.
    public class HoverCallbackTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private Action<bool> onHoverChanged;

        public void Bind(Action<bool> callback) => onHoverChanged = callback;

        public void OnPointerEnter(PointerEventData eventData) => onHoverChanged?.Invoke(true);
        public void OnPointerExit(PointerEventData eventData) => onHoverChanged?.Invoke(false);
        public void OnSelect(BaseEventData eventData) => onHoverChanged?.Invoke(true);
        public void OnDeselect(BaseEventData eventData) => onHoverChanged?.Invoke(false);
    }
}
