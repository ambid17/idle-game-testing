using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    // Hover/press feel for any Button: swells a little while hovered (or selected on a controller),
    // squashes while held, and springs back with a small overshoot on release. Purely its own
    // transform's scale, which layout ignores, so it never shifts neighbours. Bulk-added with
    // Tools > UI > Add Button Juice To All Buttons. Unscaled time, so it works over the paused game.
    [RequireComponent(typeof(Selectable))]
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float pressScale = 0.93f;
        [Tooltip("How quickly the scale chases its target (higher = snappier).")]
        [SerializeField] private float stiffness = 400f;
        [Tooltip("0..1 - lower is bouncier.")]
        [SerializeField] private float damping = 0.55f;

        private Selectable selectable;
        private Vector3 baseScale;
        private bool hovered;
        private bool selected;
        private bool pressed;
        private float scale = 1f;
        private float velocity;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            scale = 1f;
            velocity = 0f;
            transform.localScale = baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        public void OnPointerDown(PointerEventData eventData) => pressed = eventData.button == PointerEventData.InputButton.Left;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        // Mouse clicks select too - only a controller's selection should count as "hovered".
        public void OnSelect(BaseEventData eventData) => selected = eventData is not PointerEventData;
        public void OnDeselect(BaseEventData eventData) => selected = false;

        // A controller press has no down/up - kick the spring inward so it still squashes and pops.
        public void OnSubmit(BaseEventData eventData)
        {
            if (selectable.IsInteractable()) velocity -= 6f;
        }

        private void Update()
        {
            bool interactable = selectable.IsInteractable();
            float target = !interactable ? 1f : pressed ? pressScale : (hovered || selected) ? hoverScale : 1f;
            if (Mathf.Approximately(scale, target) && Mathf.Abs(velocity) < 0.001f) return;

            // Damped spring: overshoots a touch on release, then settles.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float criticalDamping = 2f * Mathf.Sqrt(stiffness);
            velocity += ((target - scale) * stiffness - velocity * criticalDamping * damping) * dt;
            scale += velocity * dt;
            if (Mathf.Abs(target - scale) < 0.0005f && Mathf.Abs(velocity) < 0.01f)
            {
                scale = target;
                velocity = 0f;
            }
            transform.localScale = baseScale * scale;
        }
    }
}
