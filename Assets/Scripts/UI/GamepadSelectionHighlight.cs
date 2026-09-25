using Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    // One shared frame drawn around whatever the EventSystem has selected, while the player is on
    // a controller - most buttons' own Selected tint is too close to Normal to see, and this
    // covers every Selectable (including runtime-spawned rows and skill tree nodes) without
    // retinting each one. Lives on its own override-sorted Canvas under the main Canvas so it
    // draws above everything, TMP_Dropdown lists included. Never a raycast target.
    [RequireComponent(typeof(RectTransform))]
    public class GamepadSelectionHighlight : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private float padding = 6f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.55f;

        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform rectTransform;
        private Color baseColor;

        private void Start()
        {
            rectTransform = (RectTransform)transform;
            if (frame == null)
            {
                Debug.LogError("GamepadSelectionHighlight.frame is not assigned.");
                return;
            }
            baseColor = frame.color;
            frame.raycastTarget = false;
            frame.enabled = false;
        }

        private void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool show = selected != null
                && selected.activeInHierarchy
                && GameManager.KeybindService.CurrentScheme == InputScheme.Gamepad
                && selected.transform is RectTransform;
            frame.enabled = show;
            if (!show) return;

            // Selected element's corners -> screen -> this object's parent space, so it lines up
            // whatever the selected element's own anchors, pivot or scale (e.g. zoomed skill
            // tree), and even when it's on a world-space canvas (the world tutorial popup).
            ((RectTransform)selected.transform).GetWorldCorners(corners);
            var parent = (RectTransform)rectTransform.parent;
            Camera sourceCamera = GetCanvasCamera(selected.GetComponentInParent<Canvas>());
            Camera ownCamera = GetCanvasCamera(parent.GetComponentInParent<Canvas>());
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[0]), ownCamera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[2]), ownCamera, out Vector2 max);

            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var parentRect = parent.rect;
            rectTransform.anchoredPosition = (min + max) * 0.5f - parentRect.center;
            rectTransform.sizeDelta = max - min + Vector2.one * (padding * 2f);

            float pulse = Mathf.Lerp(pulseMinAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
            frame.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * pulse);
        }

        private static Camera GetCanvasCamera(Canvas canvas)
        {
            if (canvas == null) return null;
            var root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return root.worldCamera != null ? root.worldCamera : Camera.main;
        }
    }
}
