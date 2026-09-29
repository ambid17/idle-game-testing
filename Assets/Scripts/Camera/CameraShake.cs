using Events;
using Settings;
using Unity.Cinemachine;
using UnityEngine;

namespace CameraControl
{
    // Single entry point for camera shake (GameManager.CameraShake). Fires Cinemachine impulses from
    // this GameObject's CinemachineImpulseSource, picked up by the CinemachineImpulseListener on the
    // scene's CinemachineCamera. Everything shakes through here so the Options "Screen Shake" toggle
    // (SettingsService.ScreenShake) turns all of it off in one place.
    // Shakes on its own for hazards (explosions, falling-rock landings), fading with distance from
    // the player so far-off automaton mishaps don't rattle the screen; Player.DigFeedback calls
    // Shake directly for dig impacts.
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private Transform player;

        [Header("Hazards")]
        [SerializeField, Min(0f)] private float explosionForce = 0.5f;
        [SerializeField, Min(0f)] private float explosionSeconds = 0.4f;
        [SerializeField, Min(0f)] private float rockLandingForce = 0.25f;
        [SerializeField, Min(0f)] private float rockLandingSeconds = 0.2f;
        [Tooltip("Hazards this far (world units) or further from the player don't shake the camera; closer ones scale up linearly to full force.")]
        [SerializeField, Min(0.1f)] private float hazardFalloffDistance = 14f;

        private CinemachineImpulseSource impulseSource;

        private void Awake()
        {
            if (player == null) Debug.LogError($"{nameof(CameraShake)}.player is not assigned.");

            impulseSource = GetComponent<CinemachineImpulseSource>();
            // Uniform: every listener feels the full impulse regardless of where this GameObject is -
            // distance falloff is handled here instead, relative to the player rather than the source.
            impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<ExplosiveDetonatedEvent>(OnExplosiveDetonated);
            GameManager.EventService.Add<FallingRockImpactEvent>(OnFallingRockImpact);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<ExplosiveDetonatedEvent>(OnExplosiveDetonated);
            GameManager.EventService.Remove<FallingRockImpactEvent>(OnFallingRockImpact);
        }

        // velocity: direction and size (world units) of the camera kick.
        public void Shake(Vector2 velocity, float seconds, CinemachineImpulseDefinition.ImpulseShapes shape = CinemachineImpulseDefinition.ImpulseShapes.Bump)
        {
            if (!SettingsService.Instance.ScreenShake) return;

            var impulse = impulseSource.ImpulseDefinition;
            impulse.ImpulseShape = shape;
            impulse.ImpulseDuration = seconds;
            impulseSource.GenerateImpulseWithVelocity(velocity);
        }

        private void OnExplosiveDetonated(ExplosiveDetonatedEvent e)
        {
            Vector3 blast = GameManager.MapGenerationService.CellToWorldCenter(e.LayerIndex, e.X, e.Y);
            float falloff = Falloff(blast);
            if (falloff <= 0f) return;

            // Kick the camera away from the blast.
            Vector2 away = player.position - blast;
            Vector2 direction = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.up;
            Shake(direction * (explosionForce * falloff), explosionSeconds, CinemachineImpulseDefinition.ImpulseShapes.Explosion);
        }

        private void OnFallingRockImpact(FallingRockImpactEvent e)
        {
            if (!e.IsLanding) return;

            float falloff = Falloff(GameManager.MapGenerationService.CellToWorldCenter(e.LayerIndex, e.X, e.Y));
            if (falloff <= 0f) return;
            Shake(Vector2.down * (rockLandingForce * falloff), rockLandingSeconds);
        }

        private float Falloff(Vector3 source) => 1f - Mathf.Clamp01(Vector2.Distance(player.position, source) / hazardFalloffDistance);
    }
}
