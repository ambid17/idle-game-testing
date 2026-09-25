using Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // Click-drag or the movement keybinds (WASD by default) to pan, scroll wheel to zoom, on a uGUI RectTransform content container.
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

        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("SkillTreePanZoomUI: no parent Canvas found, zoom-to-cursor will be inaccurate.");
            }
        }

        private void Update()
        {
            if (content == null) return;

            Vector2 move = Vector2.zero;
            var keybinds = GameManager.KeybindService;
            if (keybinds.IsPressed(GameAction.FlyUp)) move.y -= 1f;
            if (keybinds.IsPressed(GameAction.MoveDown)) move.y += 1f;
            if (keybinds.IsPressed(GameAction.MoveLeft)) move.x += 1f;
            if (keybinds.IsPressed(GameAction.MoveRight)) move.x -= 1f;
            if (move == Vector2.zero) return;

            content.anchoredPosition = content.anchoredPosition
                + move * (keyboardPanSpeed * Time.deltaTime / content.localScale.x);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (content == null) return;
            // anchoredPosition is a translation in the parent's space, not scaled by content's
            // own localScale, so adding the raw screen-space delta keeps the dragged point
            // glued to the cursor at any zoom level - dividing by scale would make drags feel
            // slower zoomed in and faster zoomed out.
            content.anchoredPosition = content.anchoredPosition + eventData.delta;
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
