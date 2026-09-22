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

        private static readonly Vector2 TimeSensitiveAnchor = new(0.5f, 1f);
        private static readonly Vector2 TimeSensitivePosition = new(0f, -40f);
        private static readonly Vector2 QueuedAnchor = new(1f, 0f);
        private static readonly Vector2 QueuedPosition = new(-40f, 40f);

        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text messageLabel;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (messageLabel == null) Debug.LogError("NotificationToastUI.messageLabel is not assigned.");

            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Play(string message, Sprite icon, NotificationUrgency urgency, Action onComplete)
        {
            if (messageLabel != null) messageLabel.text = message;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(icon != null);
            }

            bool timeSensitive = urgency == NotificationUrgency.TimeSensitive;
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = timeSensitive ? TimeSensitiveAnchor : QueuedAnchor;
            rectTransform.anchoredPosition = timeSensitive ? TimeSensitivePosition : QueuedPosition;
            canvasGroup.alpha = 1f;

            StartCoroutine(timeSensitive ? PlayTimeSensitive(onComplete) : PlayQueued(onComplete));
        }

        // Solid display for HoldSeconds, then a quick fade-out - no movement.
        private IEnumerator PlayTimeSensitive(Action onComplete)
        {
            yield return new WaitForSeconds(HoldSeconds);

            float elapsed = 0f;
            while (elapsed < TimeSensitiveFadeSeconds)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / TimeSensitiveFadeSeconds);
                yield return null;
            }

            Finish(onComplete);
        }

        // Rises and fades together across the full HoldSeconds - no separate static phase.
        private IEnumerator PlayQueued(Action onComplete)
        {
            Vector2 startPosition = rectTransform.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < HoldSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / HoldSeconds);
                canvasGroup.alpha = 1f - t;
                rectTransform.anchoredPosition = startPosition + Vector2.up * (QueuedRiseDistance * t);
                yield return null;
            }

            Finish(onComplete);
        }

        private void Finish(Action onComplete)
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        }
    }
}
