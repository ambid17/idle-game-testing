using System.Collections;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Spawned by HazardEffectResolver at the trigger cell's world position the instant an
    // Explosive block is mined - jiggles, swells and flashes in place for telegraphSeconds while
    // its fuse throws sparks (the block itself is already gone from the map by this point, same as
    // every other hazard; this visual stands in for the primed charge), then detonates: dispatches
    // ExplosiveDetonatedEvent, which is what actually triggers the blast radius destruction and the
    // blast visual (HazardEffectResolver) and player damage (Player.HazardDamageHandler). Mirrors
    // FallingRockHazardEffect's jiggle-then-resolve shape.
    public class ExplosiveHazardEffect : MonoBehaviour
    {
        [SerializeField] private float telegraphSeconds = 1f;
        [SerializeField] private float jiggleMagnitude = 0.08f;
        [Tooltip("Flash interval at the start of the fuse; it speeds up to a third of this by detonation.")]
        [SerializeField] private float flashIntervalSeconds = 0.1f;
        [SerializeField] private Color flashColor = new(1f, 0.3f, 0.1f);
        [Tooltip("How much bigger the charge has swollen by the moment it goes off.")]
        [SerializeField] private float swellScale = 0.25f;
        [SerializeField] private float fuseSparkIntervalSeconds = 0.05f;
        [SerializeField] private Color fuseSparkColor = new(1f, 0.75f, 0.3f, 1f);

        [Tooltip("Optional - assign a warning sprite here (on a prefab wired into HazardEffectResolver) for real art. Safe to leave unset; the telegraph just has no visual.")]
        [SerializeField] private SpriteRenderer visual;

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
            Vector3 baseScale = transform.localScale;
            Color baseColor = visual != null ? visual.color : Color.white;
            float elapsed = 0f;
            float flashTimer = 0f;
            float sparkTimer = 0f;
            bool flashOn = false;

            while (elapsed < telegraphSeconds)
            {
                elapsed += Time.deltaTime;
                flashTimer += Time.deltaTime;
                sparkTimer += Time.deltaTime;
                float progress = elapsed / telegraphSeconds;

                float offsetX = Mathf.Sin(elapsed * 40f) * jiggleMagnitude * Mathf.Lerp(0.5f, 1.5f, progress);
                transform.position = origin + new Vector3(offsetX, 0f, 0f);
                transform.localScale = baseScale * (1f + swellScale * progress * progress);

                if (visual != null && flashTimer >= flashIntervalSeconds * Mathf.Lerp(1f, 0.33f, progress))
                {
                    flashTimer = 0f;
                    flashOn = !flashOn;
                    visual.color = flashOn ? flashColor : baseColor;
                }

                if (sparkTimer >= fuseSparkIntervalSeconds)
                {
                    sparkTimer = 0f;
                    var velocity = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 3f), 0f);
                    GameManager.WorldEffects.Sparkle(transform.position + Vector3.up * 0.35f, velocity, Random.Range(0.15f, 0.3f), Random.Range(0.2f, 0.35f), fuseSparkColor);
                }

                yield return null;
            }

            transform.position = origin;
        }

        private void Detonate()
        {
            GameManager.EventService.Dispatch(new ExplosiveDetonatedEvent(layerIndex, cellX, cellY));
            Destroy(gameObject);
        }
    }
}
