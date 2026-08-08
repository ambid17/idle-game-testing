using Events;
using MapGeneration;
using UnityEngine;

namespace Player
{
    // Listens for HazardTriggeredEvent regardless of who mined the hazard cell - the player
    // triggering their own hazard, or (per automationImplementation.md) a Mining Automaton
    // triggering one nearby - and damages the player if they're within hazardDamageRadius of it.
    // No hazard-damage system existed anywhere before this; this is the single source of hazard
    // damage for both cases. Only implements simple proximity damage - richer per-hazard mechanics
    // (explosion block destruction, gas chain-ignition, falling-rock physics, water flooding) don't
    // exist anywhere in the codebase yet and are out of scope here.
    public class HazardDamageHandler : MonoBehaviour
    {
        [SerializeField] private float hazardDamageRadius = 3f;
        [SerializeField] private float explosiveDamage = 25f;
        [SerializeField] private float fallingRockDamage = 20f;
        [SerializeField] private float gasPocketDamage = 15f;
        [SerializeField] private float lavaDamage = 15f;

        private PlayerHealth playerHealth;
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null) Debug.LogError($"{nameof(HazardDamageHandler)} on {name} requires a PlayerHealth component.");
        }

        private void OnEnable() => GameManager.EventService.Add<HazardTriggeredEvent>(OnHazardTriggered);
        private void OnDisable() => GameManager.EventService.Remove<HazardTriggeredEvent>(OnHazardTriggered);

        private void OnHazardTriggered(HazardTriggeredEvent evt)
        {
            if (playerHealth == null || playerHealth.IsDead) return;

            float damage = DamageFor(evt.Hazard);
            if (damage <= 0f) return;

            Vector3 hazardWorldPos = mapGenerationService.CellToWorldCenter(evt.LayerIndex, evt.X, evt.Y);
            if (Vector3.Distance(transform.position, hazardWorldPos) > hazardDamageRadius) return;

            playerHealth.TakeDamage(damage);
        }

        private float DamageFor(HazardBehavior hazard) => hazard switch
        {
            HazardBehavior.Explosive => explosiveDamage,
            HazardBehavior.FallingRock => fallingRockDamage,
            HazardBehavior.GasPocket => gasPocketDamage,
            HazardBehavior.Lava => lavaDamage,
            _ => 0f
        };
    }
}
