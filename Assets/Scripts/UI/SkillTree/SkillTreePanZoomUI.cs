using System.Collections.Generic;
using Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // Click-drag, the movement keybinds (WASD by default) or the right stick to pan; scroll wheel or
    // the gamepad triggers to zoom, on a uGUI RectTransform content container.
    // Panning with the right stick shows a crosshair at the center of the view and moves the
    // controller selection to whichever node is under it, so the stick alone can pick an upgrade.
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
        // Scale change per second at full trigger pull.
        [SerializeField] private float gamepadZoomSpeed = 1f;

        private Canvas canvas;
        // Set by CenterOn (controller selection moving between nodes); eased toward in Update and
        // dropped the moment the player pans or zooms by hand.
        private Vector2? panTarget;

        // Built at runtime (two bars) as a sibling just above content, so it draws over the nodes.
        private RectTransform crosshair;
        private readonly List<SkillTreeNodeUI> nodeBuffer = new();
        // True while the right stick is panning - the selection then follows the crosshair, so
        // CenterOn mustn't pull the view to each node the crosshair passes over.
        private bool stickPanning;

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

            var keybinds = GameManager.KeybindService;
            if (keybinds.CurrentScheme != InputScheme.Gamepad) SetCrosshairVisible(false);

            float zoomInput = keybinds.ZoomViewInput;
            if (!Mathf.Approximately(zoomInput, 0f))
            {
                panTarget = null;
                var viewport = (RectTransform)transform;
                Vector2 viewportCenter = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), viewport.TransformPoint(viewport.rect.center));
                ZoomAround(viewportCenter, content.localScale.x + zoomInput * gamepadZoomSpeed * Time.deltaTime);
            }

            Vector2 move = Vector2.zero;
            // Keyboard only - on a gamepad the movement actions' d-pad/left stick navigate between
            // nodes instead (SkillTreeNodeUI pans the view to follow the selection).
            if (keybinds.CurrentScheme == InputScheme.KeyboardMouse)
            {
                if (keybinds.IsPressed(GameAction.FlyUp)) move.y -= 1f;
                if (keybinds.IsPressed(GameAction.MoveDown)) move.y += 1f;
                if (keybinds.IsPressed(GameAction.MoveLeft)) move.x += 1f;
                if (keybinds.IsPressed(GameAction.MoveRight)) move.x -= 1f;
            }
            // Right stick: pushing it moves the view the way the stick points.
            Vector2 stick = keybinds.PanViewInput;
            stickPanning = stick != Vector2.zero;
            move -= stick;
            move = Vector2.ClampMagnitude(move, 1f);
            if (move == Vector2.zero)
            {
                EaseTowardPanTarget();
                return;
            }

            panTarget = null;

            content.anchoredPosition = content.anchoredPosition
                + move * (keyboardPanSpeed * Time.deltaTime / content.localScale.x);

            if (stickPanning)
            {
                SetCrosshairVisible(true);
                SelectNodeUnderCrosshair();
            }
        }

        // Selects the node whose rect covers the center of the viewport (the closest one, should
        // two overlap it). With no node there the selection stays where it was.
        private void SelectNodeUnderCrosshair()
        {
            if (EventSystem.current == null) return;

            var viewport = (RectTransform)transform;
            Vector3 center = viewport.TransformPoint(viewport.rect.center);

            SkillTreeNodeUI best = null;
            float bestSqrDistance = float.MaxValue;
            content.GetComponentsInChildren(false, nodeBuffer);
            foreach (var node in nodeBuffer)
            {
                var rect = (RectTransform)node.transform;
                Vector2 local = rect.InverseTransformPoint(center);
                if (!rect.rect.Contains(local)) continue;

                float sqrDistance = (local - rect.rect.center).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = node;
                }
            }

            if (best != null && EventSystem.current.currentSelectedGameObject != best.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(best.gameObject);
            }
        }

        private void SetCrosshairVisible(bool visible)
        {
            if (crosshair == null)
            {
                if (!visible) return;
                BuildCrosshair();
            }

            if (visible)
            {
                var viewport = (RectTransform)transform;
                crosshair.position = viewport.TransformPoint(viewport.rect.center);
            }
            if (crosshair.gameObject.activeSelf != visible) crosshair.gameObject.SetActive(visible);
        }

        private void BuildCrosshair()
        {
            var root = new GameObject("Crosshair", typeof(RectTransform));
            crosshair = (RectTransform)root.transform;
            crosshair.SetParent(content.parent, false);
            crosshair.SetSiblingIndex(content.GetSiblingIndex() + 1);
            crosshair.sizeDelta = Vector2.zero;

            AddCrosshairBar(new Vector2(36f, 4f));
            AddCrosshairBar(new Vector2(4f, 36f));
        }

        private void AddCrosshairBar(Vector2 size)
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(Outline));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(crosshair, false);
            rect.sizeDelta = size;
            bar.GetComponent<Image>().raycastTarget = false;
            var outline = bar.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private void OnDisable()
        {
            stickPanning = false;
            SetCrosshairVisible(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (content == null) return;
            // anchoredPosition is a translation in the parent's space, not scaled by content's
            // own localScale, so adding the raw screen-space delta keeps the dragged point
            // glued to the cursor at any zoom level - dividing by scale would make drags feel
            // slower zoomed in and faster zoomed out.
            panTarget = null;
            content.anchoredPosition = content.anchoredPosition + eventData.delta;
        }

        // Pans (smoothly) so target sits at the center of the viewport - used when a controller
        // moves the selection onto a node, which may be off-screen. instant skips the easing, for
        // putting the view back on a node as the tree opens.
        public void CenterOn(RectTransform target, bool instant = false)
        {
            if (content == null || content.parent == null) return;
            // The crosshair picked this node - the view is already where the player put it.
            if (stickPanning) return;
            SetCrosshairVisible(false);
            var parent = content.parent;
            var viewport = (RectTransform)transform;
            Vector3 viewportCenter = parent.InverseTransformPoint(viewport.TransformPoint(viewport.rect.center));
            Vector3 targetPoint = parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 destination = content.anchoredPosition + (Vector2)(viewportCenter - targetPoint);
            if (instant)
            {
                content.anchoredPosition = destination;
                panTarget = null;
            }
            else
            {
                panTarget = destination;
            }
        }

        private void EaseTowardPanTarget()
        {
            if (panTarget == null) return;
            content.anchoredPosition = Vector2.Lerp(content.anchoredPosition, panTarget.Value, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if ((content.anchoredPosition - panTarget.Value).sqrMagnitude < 1f)
            {
                content.anchoredPosition = panTarget.Value;
                panTarget = null;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (content == null) return;
            panTarget = null;
            ZoomAround(eventData.position, content.localScale.x + eventData.scrollDelta.y * zoomSpeed);
        }

        private Camera GetCanvasCamera() =>
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        private void ZoomAround(Vector2 screenPoint, float targetScale)
        {
            float current = content.localScale.x;
            float next = Mathf.Clamp(targetScale, minZoom, maxZoom);
            if (Mathf.Approximately(next, current)) return;

            // Keep the point under the cursor fixed on screen: find where the cursor lands in
            // the content's parent space, then re-derive anchoredPosition so that same parent-
            // space point still corresponds to the same spot in content-local space at the new
            // scale (standard zoom-to-cursor math for a scaled RectTransform).
            var parent = content.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, GetCanvasCamera(), out Vector2 localPoint))
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
            panTarget = null;
            SetCrosshairVisible(false);
            content.anchoredPosition = Vector2.zero;
            content.localScale = new Vector3(defaultScale, defaultScale, 1f);
        }
    }
}
