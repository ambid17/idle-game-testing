using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace UI
{
    // Reusable "count towards" text: instead of snapping straight to a new number, SetValue()
    // ticks the displayed value from whatever it currently shows to the new target at a fixed
    // numbers-per-second rate (clamped to a min/max duration so a huge jump still resolves
    // quickly). Attach alongside any TMP_Text that displays a changing count/value - e.g. ore
    // counts and their $ totals in OreRowUI/GoodsRowUI - and call SetValue() instead of writing
    // .text directly. Runs on unscaled time so it still plays while the game is paused.
    [RequireComponent(typeof(TMP_Text))]
    public class AnimatedCounter : MonoBehaviour
    {
        [SerializeField] private float countsPerSecond = 60f;
        [SerializeField] private float minDuration = 0.15f;
        [SerializeField] private float maxDuration = 0.75f;

        private TMP_Text label;
        private Coroutine animateRoutine;
        private float displayedValue;
        private float targetValue;
        private System.Func<float, string> formatter = v => Mathf.RoundToInt(v).ToString();

        // Resolved lazily rather than in Awake() - a row variant (e.g. HudInventoryRow's
        // ValueLabel) can keep this GameObject inactive by design, and Unity never calls Awake()
        // on a component whose GameObject is inactive in the hierarchy. GetComponent() itself
        // works fine on an inactive object, so this still resolves correctly the first time
        // something (Bind/SetFormatter/SetValue) touches this counter, active or not.
        private TMP_Text Label => label != null ? label : (label = GetComponent<TMP_Text>());

        // Overrides how the displayed value is rendered (default is a plain rounded int) - e.g.
        // pass v => $"${v:0}" for a dollar total.
        public void SetFormatter(System.Func<float, string> formatter)
        {
            this.formatter = formatter;
            Render();
        }

        public void SetValue(float value, bool instant = false)
        {
            targetValue = value;

            if (instant || !isActiveAndEnabled)
            {
                if (animateRoutine != null)
                {
                    StopCoroutine(animateRoutine);
                    animateRoutine = null;
                }
                displayedValue = value;
                Render();
                return;
            }

            if (animateRoutine != null) StopCoroutine(animateRoutine);
            animateRoutine = StartCoroutine(AnimateToTarget());
        }

        private IEnumerator AnimateToTarget()
        {
            var start = displayedValue;
            var end = targetValue;
            var delta = Mathf.Abs(end - start);
            var duration = delta <= 0f ? 0f : Mathf.Clamp(delta / countsPerSecond, minDuration, maxDuration);

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                displayedValue = Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                Render();
                yield return null;
            }

            displayedValue = end;
            Render();
            animateRoutine = null;
        }

        private void Render()
        {
            Label.text = formatter(displayedValue);
        }
    }
}
