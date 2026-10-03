using System;
using System.Collections;
using Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Notifications
{
    // Shared display component for both notification prefabs (icon+text and text-only - iconImage
    // is simply left unassigned on the text-only variant). NotificationManager instantiates one of
    // these per queued notification and drives it via Play(); the animation and screen anchor
    // depend on Urgency - TimeSensitive pops at top-middle and holds before fading (HudToastUI's
    // old spot), Queued rises while fading at bottom-right (OreMinedToastUI's old spot) - per the
    // notification system design.
    public class NotificationToastUI : MonoBehaviour
    {
        private const float HoldSeconds = 2f;
        private const float TimeSensitiveFadeSeconds = 0.3f;
        private const float QueuedRiseDistance = 60f;
        // Longer Queued messages (e.g. power-up explanations) stay up longer so they can be read;
        // short ones (deposit reports) still take the base HoldSeconds.
        private const float QueuedSecondsPerCharacter = 0.06f;
        // Entrance: TimeSensitive drops in from above with a slight overshoot, Queued slides in
        // from the right edge.
        private const float EnterSeconds = 0.28f;
        private const float TimeSensitiveDropDistance = 70f;
        private const float QueuedSlideDistance = 120f;

        private static readonly Vector2 TimeSensitiveAnchor = new(0.5f, 1f);
        private static readonly Vector2 TimeSensitivePosition = new(0f, -40f);
        private static readonly Vector2 QueuedAnchor = new(1f, 0f);
        private static readonly Vector2 QueuedPosition = new(-40f, 40f);

        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text messageLabel;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private float baseHeight;

        private void Awake()
        {
            if (messageLabel == null) Debug.LogError("NotificationToastUI.messageLabel is not assigned.");

            rectTransform = GetComponent<RectTransform>();
            baseHeight = rectTransform.sizeDelta.y;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Play(string message, Sprite icon, Sprite iconBackground, NotificationUrgency urgency, float durationMultiplier, Action onComplete)
        {
            if (messageLabel != null) messageLabel.text = message;
            if (iconImage != null)
            {
                iconImage.SetIcon(icon, iconBackground);
                iconImage.gameObject.SetActive(icon != null);
            }

            FitHeightToMessage(message);

            bool timeSensitive = urgency == NotificationUrgency.TimeSensitive;
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = timeSensitive ? TimeSensitiveAnchor : QueuedAnchor;
            rectTransform.anchoredPosition = timeSensitive ? TimeSensitivePosition : QueuedPosition;
            canvasGroup.alpha = 0f;

            float queuedSeconds = Mathf.Max(HoldSeconds, message.Length * QueuedSecondsPerCharacter) * durationMultiplier;
            StartCoroutine(timeSensitive ? PlayTimeSensitive(HoldSeconds * durationMultiplier, onComplete) : PlayQueued(queuedSeconds, onComplete));
        }

        private IEnumerator Enter(Vector2 offset)
        {
            Vector2 restPosition = rectTransform.anchoredPosition;
            for (float elapsed = 0f; elapsed < EnterSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / EnterSeconds;
                rectTransform.anchoredPosition = restPosition + offset * (1f - Effects.Easing.OutBack(t));
                canvasGroup.alpha = Mathf.Clamp01(t * 2.5f);
                yield return null;
            }
            rectTransform.anchoredPosition = restPosition;
            canvasGroup.alpha = 1f;
        }

        // Drops in, holds for holdSeconds, then a quick fade-out.
        private IEnumerator PlayTimeSensitive(float holdSeconds, Action onComplete)
        {
            yield return Enter(Vector2.up * TimeSensitiveDropDistance);
            yield return new WaitForSeconds(holdSeconds);

            float elapsed = 0f;
            while (elapsed < TimeSensitiveFadeSeconds)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / TimeSensitiveFadeSeconds);
                yield return null;
            }

            Finish(onComplete);
        }

        // Slides in, then rises and fades together across the full duration.
        private IEnumerator PlayQueued(float duration, Action onComplete)
        {
            yield return Enter(Vector2.right * QueuedSlideDistance);
            Vector2 startPosition = rectTransform.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = 1f - t;
                rectTransform.anchoredPosition = startPosition + Vector2.up * (QueuedRiseDistance * t);
                yield return null;
            }

            Finish(onComplete);
        }

        // Grows the toast (never below its prefab height) so long messages - e.g. power-up
        // explanations - stay inside the background. The label is stretched with a fixed inset, so
        // the toast needs the text's height plus that inset. Auto-sizing labels (text-only prefab)
        // already shrink to fit and are left alone. Each pivot sits on the screen edge, so the toast
        // grows away from it.
        private void FitHeightToMessage(string message)
        {
            if (messageLabel == null || messageLabel.enableAutoSizing) return;

            var labelRect = messageLabel.rectTransform;
            float labelWidth = rectTransform.sizeDelta.x + labelRect.sizeDelta.x;
            float textHeight = messageLabel.GetPreferredValues(message, labelWidth, 0f).y;
            float height = Mathf.Max(baseHeight, textHeight - labelRect.sizeDelta.y);
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, height);
        }

        private void Finish(Action onComplete)
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        }
    }
}
