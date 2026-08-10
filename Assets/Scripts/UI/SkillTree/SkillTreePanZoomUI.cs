using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // Click-drag to pan, scroll wheel to zoom, on a uGUI RectTransform content container. Lives
    // on a full-bleed transparent raycast-target Image over the tree's viewport, so drag/scroll
    // register anywhere in the empty background, not just on top of nodes.
    [RequireComponent(typeof(Image))]
    public class SkillTreePanZoomUI : MonoBehaviour, IDragHandler, IScrollHandler
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float minZoom = 0.4f;
        [SerializeField] private float maxZoom = 1.5f;
        [SerializeField] private float maxPanRadius = 1600f;

        public void OnDrag(PointerEventData eventData)
        {
            if (content == null) return;
            content.anchoredPosition = ClampPan(content.anchoredPosition + eventData.delta / content.localScale.x);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (content == null) return;
            float next = Mathf.Clamp(content.localScale.x + eventData.scrollDelta.y * zoomSpeed, minZoom, maxZoom);
            content.localScale = new Vector3(next, next, 1f);
        }

        private Vector2 ClampPan(Vector2 position) => Vector2.ClampMagnitude(position, maxPanRadius);

        // Recenters and resets zoom - called by SkillTreePanelUI whenever the tree view is
        // opened, so a pan/zoom left over from last time doesn't strand the player looking at
        // empty space.
        public void ResetView()
        {
            if (content == null) return;
            content.anchoredPosition = Vector2.zero;
            content.localScale = Vector3.one;
        }
    }
}
