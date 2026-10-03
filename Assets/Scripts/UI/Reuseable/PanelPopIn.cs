using System.Collections;
using UnityEngine;

namespace UI
{
    // Fades and scales a panel in whenever its GameObject is activated - put it on a panel's
    // "renderer" root (the object its UI controller toggles), so no controller code changes.
    // Opening only: closing stays instant, so nothing that checks a panel's active state (input
    // blocking, ModalTracker, GamepadFocus) ever sees a half-closed panel. Unscaled time, so it
    // still plays over the paused game.
    [RequireComponent(typeof(RectTransform))]
    public class PanelPopIn : MonoBehaviour
    {
        [SerializeField] private float seconds = 0.16f;
        [SerializeField] private float startScale = 0.94f;
        [Tooltip("Overshoot slightly before settling (a pop); off = a plain ease-out.")]
        [SerializeField] private bool overshoot = true;

        private CanvasGroup canvasGroup;
        private Vector3 baseScale;
        private Coroutine routine;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Play());
        }

        private void OnDisable()
        {
            routine = null;
            transform.localScale = baseScale;
            canvasGroup.alpha = 1f;
        }

        private IEnumerator Play()
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                float eased = overshoot ? Effects.Easing.OutBack(k) : Effects.Easing.OutCubic(k);
                transform.localScale = baseScale * Mathf.LerpUnclamped(startScale, 1f, eased);
                canvasGroup.alpha = Effects.Easing.OutCubic(Mathf.Min(1f, k * 1.6f));
                yield return null;
            }
            transform.localScale = baseScale;
            canvasGroup.alpha = 1f;
            routine = null;
        }
    }
}
