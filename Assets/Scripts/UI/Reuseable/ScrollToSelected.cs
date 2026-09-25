using Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Reuseable
{
    // Scrolls a ScrollRect just far enough to bring the EventSystem's selected child into view
    // whenever the selection changes, so controller navigation (which moves the selection, not
    // the mouse wheel) can reach rows below the fold. Gamepad only - mouse users scroll themselves.
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollToSelected : MonoBehaviour
    {
        private ScrollRect scrollRect;
        private GameObject lastSelected;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        private void LateUpdate()
        {
            if (GameManager.KeybindService.CurrentScheme != InputScheme.Gamepad || EventSystem.current == null) return;

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == lastSelected) return;
            lastSelected = selected;

            if (selected == null || scrollRect.content == null || !selected.transform.IsChildOf(scrollRect.content)) return;
            ScrollIntoView((RectTransform)selected.transform);
        }

        private void ScrollIntoView(RectTransform target)
        {
            var viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            Rect view = viewport.rect;

            Vector2 overflow = Vector2.zero;
            if (scrollRect.vertical)
            {
                if (bounds.max.y > view.yMax) overflow.y = bounds.max.y - view.yMax;
                else if (bounds.min.y < view.yMin) overflow.y = bounds.min.y - view.yMin;
            }
            if (scrollRect.horizontal)
            {
                if (bounds.max.x > view.xMax) overflow.x = bounds.max.x - view.xMax;
                else if (bounds.min.x < view.xMin) overflow.x = bounds.min.x - view.xMin;
            }
            if (overflow == Vector2.zero) return;

            scrollRect.velocity = Vector2.zero;
            scrollRect.content.anchoredPosition -= overflow;
        }
    }
}
