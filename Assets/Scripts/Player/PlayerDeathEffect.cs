using System.Collections;
using Events;
using UnityEngine;

namespace Player
{
    // Plays a brief explosion beat at the death location and hides the player sprite before
    // handing off to DeathUI - decouples the death screen's reveal from PlayerDiedEvent itself
    // (see PlayerDeathMenuRequestedEvent) so this component alone controls how long that delay is.
    // Mirrors MapGeneration.ExplosiveHazardEffect's play-then-signal shape.
    public class PlayerDeathEffect : MonoBehaviour
    {
        [Tooltip("Optional - assign a burst particle system here for real art. Safe to leave unset; the delay just runs silently for fallbackSeconds instead.")]
        [SerializeField] private ParticleSystem explosionParticles;
        [SerializeField] private float fallbackSeconds = 1f;

        private SpriteRenderer playerSprite;

        private void Awake()
        {
            playerSprite = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Add<PlayerRevivedEvent>(HandleRevived);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Remove<PlayerRevivedEvent>(HandleRevived);
        }

        private void HandleDied(PlayerDiedEvent evt)
        {
            StartCoroutine(PlayThenRequestMenu());
        }

        private IEnumerator PlayThenRequestMenu()
        {
            if (playerSprite != null) playerSprite.enabled = false;

            float delay = fallbackSeconds;
            if (explosionParticles != null)
            {
                explosionParticles.Play();
                delay = explosionParticles.main.startLifetime.constantMax + 0.2f;
            }

            yield return new WaitForSeconds(delay);

            GameManager.EventService.Dispatch<PlayerDeathMenuRequestedEvent>();
        }

        private void HandleRevived()
        {
            if (playerSprite != null) playerSprite.enabled = true;
        }
    }
}
