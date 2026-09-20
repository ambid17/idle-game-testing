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

        private void Awake()
        {
            keyboard = Keyboard.current;
            if (keyboard == null)
            {
                Debug.LogError("SkillTreePanZoomUI: no keyboard found, WASD panning disabled.");
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
            float next = Mathf.Clamp(content.localScale.x + eventData.scrollDelta.y * zoomSpeed, minZoom, maxZoom);
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
