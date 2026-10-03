using System.Collections;
using System.Collections.Generic;
using Audio;
using Events;
using TMPro;
using UnityEngine;

namespace UI
{
    // Big centred title card for milestones (MilestoneBannerRequestedEvent): a dark band sweeps
    // open across the screen, the title punches in over it in the milestone's colour, the
    // subtitle fades in under it, it holds, then everything fades away. One at a time - requests
    // that arrive while one is showing wait their turn. Never blocks input. Unscaled time.
    public class MilestoneBannerUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("The dark band behind the text - opens horizontally from the centre.")]
        [SerializeField] private RectTransform band;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private float openSeconds = 0.3f;
        [SerializeField] private float holdSeconds = 2.2f;
        [SerializeField] private float fadeSeconds = 0.5f;

        private readonly Queue<MilestoneBannerRequestedEvent> pending = new();
        private bool playing;

        private void Awake()
        {
            if (rendererRoot == null) Debug.LogError($"{nameof(MilestoneBannerUI)}.rendererRoot is not assigned.");
            if (canvasGroup == null) Debug.LogError($"{nameof(MilestoneBannerUI)}.canvasGroup is not assigned.");
            if (band == null) Debug.LogError($"{nameof(MilestoneBannerUI)}.band is not assigned.");
            if (titleLabel == null) Debug.LogError($"{nameof(MilestoneBannerUI)}.titleLabel is not assigned.");
            if (subtitleLabel == null) Debug.LogError($"{nameof(MilestoneBannerUI)}.subtitleLabel is not assigned.");
            rendererRoot.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<MilestoneBannerRequestedEvent>(OnRequested);

        private void OnDisable()
        {
            GameManager.EventService.Remove<MilestoneBannerRequestedEvent>(OnRequested);
            StopAllCoroutines();
            pending.Clear();
            playing = false;
            rendererRoot.SetActive(false);
        }

        private void OnRequested(MilestoneBannerRequestedEvent evt)
        {
            pending.Enqueue(evt);
            if (!playing) StartCoroutine(PlayQueue());
        }

        private IEnumerator PlayQueue()
        {
            playing = true;
            while (pending.Count > 0) yield return Play(pending.Dequeue());
            playing = false;
        }

        private IEnumerator Play(MilestoneBannerRequestedEvent evt)
        {
            titleLabel.text = evt.Title;
            titleLabel.color = evt.Color;
            subtitleLabel.text = evt.Subtitle;
            subtitleLabel.gameObject.SetActive(!string.IsNullOrEmpty(evt.Subtitle));
            canvasGroup.alpha = 1f;
            rendererRoot.SetActive(true);
            GameManager.AudioService.Play(SoundId.Milestone);

            var subtitleColor = subtitleLabel.color;
            for (float t = 0f; t < openSeconds * 2f; t += Time.unscaledDeltaTime)
            {
                float open = Mathf.Clamp01(t / openSeconds);
                band.localScale = new Vector3(Effects.Easing.OutCubic(open), 1f, 1f);
                // The title lands just after the band starts opening; the subtitle trails it.
                float title = Mathf.Clamp01((t - openSeconds * 0.3f) / openSeconds);
                titleLabel.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, Effects.Easing.OutBack(title));
                titleLabel.alpha = title;
                subtitleColor.a = Mathf.Clamp01((t - openSeconds) / openSeconds);
                subtitleLabel.color = subtitleColor;
                yield return null;
            }
            band.localScale = Vector3.one;
            titleLabel.rectTransform.localScale = Vector3.one;
            titleLabel.alpha = 1f;
            subtitleColor.a = 1f;
            subtitleLabel.color = subtitleColor;

            yield return new WaitForSecondsRealtime(holdSeconds);

            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha = 1f - t / fadeSeconds;
                yield return null;
            }
            rendererRoot.SetActive(false);
        }
    }
}
