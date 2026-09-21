using System.Collections;
using Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Full-screen red flash driven by PlayerDamagedEvent - generic feedback for any damage
    // source (hazards, fall damage, etc), separate from DeathUI's own reason-specific screen.
    // Flash strength scales a little with hit size so a light gas tick doesn't flash as hard as
    // an explosive blast.
    public class DamageScreenEffectUI : MonoBehaviour
    {
        private const float ReferenceDamage = 25f;

        [SerializeField] private GameObject renderer;
        [SerializeField] private Image flashImage;
        [SerializeField] private float minFlashAlpha = 0.15f;
        [SerializeField] private float maxFlashAlpha = 0.45f;
        [SerializeField] private float fadeSeconds = 0.4f;

        private Coroutine fadeRoutine;

        private void Start()
        {
            if (renderer == null) Debug.LogError($"{nameof(DamageScreenEffectUI)} on {name} is missing its renderer reference.");
            if (flashImage == null) Debug.LogError($"{nameof(DamageScreenEffectUI)} on {name} is missing its flashImage reference.");
            if (renderer != null) renderer.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<PlayerDamagedEvent>(Flash);
        private void OnDisable() => GameManager.EventService.Remove<PlayerDamagedEvent>(Flash);

        private void Flash(PlayerDamagedEvent evt)
        {
            if (renderer == null || flashImage == null) return;

            float alpha = Mathf.Lerp(minFlashAlpha, maxFlashAlpha, Mathf.Clamp01(evt.Amount / ReferenceDamage));
            var color = flashImage.color;
            flashImage.color = new Color(color.r, color.g, color.b, alpha);
            renderer.SetActive(true);

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            Color startColor = flashImage.color;
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                flashImage.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, elapsed / fadeSeconds));
                yield return null;
            }
            flashImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f);
            renderer.SetActive(false);
            fadeRoutine = null;
        }
    }
}
