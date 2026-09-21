using System.Collections;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Spawned by HazardEffectResolver at the trigger cell's world position the instant an
    // Explosive block is mined - jiggles and flashes in place for telegraphSeconds (the block
    // itself is already gone from the map by this point, same as every other hazard; this visual
    // stands in for the primed charge), then detonates: plays the burst particles and dispatches
    // ExplosiveDetonatedEvent, which is what actually triggers the blast radius destruction
    // (HazardEffectResolver) and player damage (Player.HazardDamageHandler). Mirrors
    // FallingRockHazardEffect's jiggle-then-resolve shape.
    public class ExplosiveHazardEffect : MonoBehaviour
    {
        [SerializeField] private float telegraphSeconds = 1f;
        [SerializeField] private float jiggleMagnitude = 0.08f;
        [SerializeField] private float flashIntervalSeconds = 0.1f;
        [SerializeField] private Color flashColor = new(1f, 0.3f, 0.1f);

        [Tooltip("Optional - assign a warning sprite here (on a prefab wired into HazardEffectResolver) for real art. Safe to leave unset; the telegraph just has no visual.")]
        [SerializeField] private SpriteRenderer visual;

        [Tooltip("Optional - assign a burst particle system here (on a prefab wired into HazardEffectResolver). Safe to leave unset; the effect just does nothing visually.")]
        [SerializeField] private ParticleSystem burstParticles;

        private int layerIndex;
        private int cellX;
        private int cellY;

        public void Begin(int layerIndex, int x, int y)
        {
            this.layerIndex = layerIndex;
            cellX = x;
            cellY = y;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return Telegraph();
            Detonate();
        }

        private IEnumerator Telegraph()
        {
            Vector3 origin = transform.position;
            Color baseColor = visual != null ? visual.color : Color.white;
            float elapsed = 0f;
            float flashTimer = 0f;
            bool flashOn = false;

            while (elapsed < telegraphSeconds)
            {
                elapsed += Time.deltaTime;
                flashTimer += Time.deltaTime;

                float offsetX = Mathf.Sin(elapsed * 40f) * jiggleMagnitude;
                transform.position = origin + new Vector3(offsetX, 0f, 0f);

                if (visual != null && flashTimer >= flashIntervalSeconds)
                {
                    flashTimer = 0f;
                    flashOn = !flashOn;
                    visual.color = flashOn ? flashColor : baseColor;
                }

                yield return null;
            }

            transform.position = origin;
        }

        private void Detonate()
        {
            if (visual != null) visual.gameObject.SetActive(false);

            float lingerForParticles = 0f;
            if (burstParticles != null)
            {
                burstParticles.Play();
                lingerForParticles = burstParticles.main.startLifetime.constantMax + 0.2f;
            }

            GameManager.EventService.Dispatch(new ExplosiveDetonatedEvent(layerIndex, cellX, cellY));
            Destroy(gameObject, lingerForParticles);
        }
    }
}
