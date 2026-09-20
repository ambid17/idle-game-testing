using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // Click-drag or WASD to pan, scroll wheel to zoom, on a uGUI RectTransform content container.
    // Lives on a full-bleed transparent raycast-target Image over the tree's viewport, so
    // drag/scroll register anywhere in the empty background, not just on top of nodes.
    [RequireComponent(typeof(Image))]
    public class SkillTreePanZoomUI : MonoBehaviour, IDragHandler, IScrollHandler
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float minZoom = 0.4f;
        [SerializeField] private float maxZoom = 1.5f;
        [SerializeField] private float defaultScale = 0.5f;
        [SerializeField] private float keyboardPanSpeed = 800f;

        private Keyboard keyboard;
        private Canvas canvas;

        private void Awake()
        {
            keyboard = Keyboard.current;
            if (keyboard == null)
            {
                Debug.LogError("SkillTreePanZoomUI: no keyboard found, WASD panning disabled.");
            }

            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("SkillTreePanZoomUI: no parent Canvas found, zoom-to-cursor will be inaccurate.");
            }
        }

        private void Update()
        {
            if (content == null || keyboard == null) return;

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y -= 1f;
            if (keyboard.sKey.isPressed) move.y += 1f;
            if (keyboard.aKey.isPressed) move.x += 1f;
            if (keyboard.dKey.isPressed) move.x -= 1f;
            if (move == Vector2.zero) return;

            content.anchoredPosition = content.anchoredPosition
                + move * (keyboardPanSpeed * Time.deltaTime / content.localScale.x);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (content == null) return;
            content.anchoredPosition = content.anchoredPosition + eventData.delta / content.localScale.x;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (content == null) return;
            float current = content.localScale.x;
            float next = Mathf.Clamp(current + eventData.scrollDelta.y * zoomSpeed, minZoom, maxZoom);
            if (Mathf.Approximately(next, current)) return;

            // Keep the point under the cursor fixed on screen: find where the cursor lands in
            // the content's parent space, then re-derive anchoredPosition so that same parent-
            // space point still corresponds to the same spot in content-local space at the new
            // scale (standard zoom-to-cursor math for a scaled RectTransform).
            var parent = content.parent as RectTransform;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, cam, out Vector2 localPoint))
            {
                Vector2 contentSpacePoint = (localPoint - content.anchoredPosition) / current;
                content.anchoredPosition = localPoint - contentSpacePoint * next;
            }

            content.localScale = new Vector3(next, next, 1f);
        }

        // Recenters and resets zoom - called by SkillTreePanelUI whenever the tree view is
        // opened, so a pan/zoom left over from last time doesn't strand the player looking at
        // empty space.
        public void ResetView()
        {
            if (content == null) return;
            content.anchoredPosition = Vector2.zero;
            content.localScale = new Vector3(defaultScale, defaultScale, 1f);
        }
    }
}
