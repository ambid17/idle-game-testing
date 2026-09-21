using UnityEngine;

namespace MapGeneration
{
    // Spawned by HazardEffectResolver at the trigger cell's world position the instant an
    // Explosive block is mined - a one-shot particle burst only, no lingering state. All the
    // actual block destruction and player damage happen elsewhere (see HazardEffectResolver.
    // ResolveExplosive and Player.HazardDamageHandler); this just plays the visual and cleans
    // itself up once the burst's particles have run their course.
    public class ExplosiveHazardEffect : MonoBehaviour
    {
        [Tooltip("Optional - assign a burst particle system here (on a prefab wired into HazardEffectResolver). Safe to leave unset; the effect just does nothing visually.")]
        [SerializeField] private ParticleSystem burstParticles;

        public void Begin()
        {
            if (burstParticles == null)
            {
                Destroy(gameObject);
                return;
            }

            burstParticles.Play();
            Destroy(gameObject, burstParticles.main.startLifetime.constantMax + 0.2f);
        }
    }
}
