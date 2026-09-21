using System.Collections;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Jiggles and flashes in place for telegraphSeconds (mirrors ExplosiveHazardEffect's
    // telegraph), then releases: expands, lingers, then dissipates - spawned by
    // HazardEffectResolver at a mined GasPocket cell's world position. Dispatches a damage-tick
    // event on an interval while it has any radius; the "is the player actually in range"
    // distance check lives in Player.HazardDamageHandler (same place every other hazard's radius
    // check already lives), not here - this effect only owns its own telegraph/expand/linger/
    // dissipate timeline. Chain-ignition into Lava/Explosive (GameDesignDoc) is deferred to a
    // follow-up pass.
    public class GasCloudHazardEffect : MonoBehaviour
    {
        [SerializeField] private float telegraphSeconds = 1f;
        [SerializeField] private float jiggleMagnitude = 0.08f;
        [SerializeField] private float flashIntervalSeconds = 0.1f;
        [SerializeField] private Color flashColor = new(1f, 0.3f, 0.1f);

        [SerializeField] private float expandSeconds = 1f;
        [SerializeField] private float lingerSeconds = 4f;
        [SerializeField] private float dissipateSeconds = 1.5f;
        [SerializeField] private float maxRadius = 3f;
        [SerializeField] private float tickIntervalSeconds = 0.5f;
        [SerializeField] private Color gasColor = new(0.5f, 0.9f, 0.3f, 0.5f);
        [SerializeField] private float visualRotationSpeed = 15f;

        [Tooltip("Optional - assign a warning sprite here (on a prefab wired into HazardEffectResolver) for real art. Safe to leave unset; the telegraph just has no visual.")]
        [SerializeField] private SpriteRenderer warningVisual;

        [Tooltip("Optional - assign a gas VFX sprite here (on a prefab wired into HazardEffectResolver) for real art. Safe to leave unset; the effect still runs without a visual.")]
        [SerializeField] private SpriteRenderer visual;

        [Tooltip("Optional - assign a particle system here (on a prefab wired into HazardEffectResolver). Its shape radius is driven every frame to match the cloud's current radius, so whatever radius is set in the Inspector/module is overwritten at runtime - just pick a Circle/Sphere shape and let this script own the size.")]
        [SerializeField] private ParticleSystem gasParticles;

        private ParticleSystem.ShapeModule particleShape;
        private ParticleSystem.EmissionModule particleEmission;

        private int layerIndex;
        private int cellX;
        private int cellY;
        private float currentRadius;

        public void Begin(int layerIndex, int x, int y)
        {
            this.layerIndex = layerIndex;
            cellX = x;
            cellY = y;
            StartCoroutine(Run());
        }

        // Slow continuous spin for the cloud sprite, for as long as this effect is enabled -
        // scale-zero (and so invisible) during the telegraph via SetRadius(0), so the rotation
        // only actually reads once the cloud is released, with no extra state needed to gate it.
        private void Update()
        {
            if (visual != null) visual.transform.Rotate(0f, 0f, visualRotationSpeed * Time.deltaTime);
        }

        private IEnumerator Run()
        {
            yield return Telegraph();
            Release();

            yield return Lerp(0f, maxRadius, expandSeconds);
            yield return new WaitForSeconds(lingerSeconds);
            yield return Lerp(maxRadius, 0f, dissipateSeconds);

            // Stop spawning new particles once the cloud has fully shrunk, but delay destroying the
            // GameObject until whatever already-emitted particles are still alive have finished
            // their own lifetime naturally, instead of popping out with the rest of the effect.
            float lingerForParticles = 0f;
            if (gasParticles != null)
            {
                gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                lingerForParticles = gasParticles.main.startLifetime.constantMax;
            }
            Destroy(gameObject, lingerForParticles);
        }

        // Same jiggle-in-place-and-blink-a-warning-color shape as ExplosiveHazardEffect.Telegraph -
        // the mined GasPocket block is already gone from the map by this point, so warningVisual
        // stands in for the primed pocket.
        private IEnumerator Telegraph()
        {
            Vector3 origin = transform.position;
            Color baseColor = warningVisual != null ? warningVisual.color : Color.white;
            float elapsed = 0f;
            float flashTimer = 0f;
            bool flashOn = false;

            while (elapsed < telegraphSeconds)
            {
                elapsed += Time.deltaTime;
                flashTimer += Time.deltaTime;

                float offsetX = Mathf.Sin(elapsed * 40f) * jiggleMagnitude;
                transform.position = origin + new Vector3(offsetX, 0f, 0f);

                if (warningVisual != null && flashTimer >= flashIntervalSeconds)
                {
                    flashTimer = 0f;
                    flashOn = !flashOn;
                    warningVisual.color = flashOn ? flashColor : baseColor;
                }

                yield return null;
            }

            transform.position = origin;
            if (warningVisual != null) warningVisual.gameObject.SetActive(false);
        }

        // The moment the telegraph ends - starts the actual cloud (visual, particles, damage
        // ticks). Everything before this is just the warning.
        private void Release()
        {
            if (visual != null) visual.color = gasColor;

            if (gasParticles != null)
            {
                particleShape = gasParticles.shape;
                particleEmission = gasParticles.emission;
                var main = gasParticles.main;
                main.startColor = gasColor;
                gasParticles.Play();
            }

            StartCoroutine(TickDamage());
        }

        private IEnumerator Lerp(float from, float to, float seconds)
        {
            float t = 0f;
            while (seconds > 0f && t < seconds)
            {
                t += Time.deltaTime;
                SetRadius(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetRadius(to);
        }

        private void SetRadius(float radius)
        {
            currentRadius = radius;
            if (visual != null) visual.transform.localScale = Vector3.one * radius * 2f;
            if (gasParticles != null)
            {
                particleShape.radius = Mathf.Max(radius, 0.01f);
                particleEmission.enabled = radius > 0f;
            }
        }

        private IEnumerator TickDamage()
        {
            var wait = new WaitForSeconds(tickIntervalSeconds);
            while (true)
            {
                if (currentRadius > 0f)
                {
                    GameManager.EventService.Dispatch(new GasCloudDamageTickEvent(layerIndex, cellX, cellY, currentRadius));
                }
                yield return wait;
            }
        }
    }
}
