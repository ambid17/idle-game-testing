using System;
using Economy;
using Events;
using MapGeneration;
using UnityEngine;

namespace Player
{
    // Single source of hazard damage for every HazardBehavior, regardless of who mined the cell -
    // the player triggering their own hazard, or (per automationImplementation.md) a Mining
    // Automaton triggering one nearby (automatons never take damage themselves, only the player
    // does - see MapGeneration.HazardEffectResolver). Explosive resolves straight off
    // HazardTriggeredEvent (the blast is instant); FallingRock/GasPocket listen for the delayed/
    // lingering events HazardEffectResolver's spawned effects dispatch once their own telegraph/
    // lifetime elapses; Lava is a per-frame contact check against CellData.HazardousSurface, since
    // it has no further trigger moment after the cell is mined.
    public class HazardDamageHandler : MonoBehaviour
    {
        [SerializeField] private float hazardDamageRadius = 3f;
        [SerializeField] private float explosiveDamage = 25f;
        [SerializeField] private float fallingRockDamage = 20f;
        [SerializeField] private float gasCloudTickDamage = 8f;
        [SerializeField] private float lavaDamage = 10f;
        [SerializeField] private float lavaDamageTickSeconds = 1f;

        private PlayerHealth playerHealth;
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private float lavaDamageTimer;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null) Debug.LogError($"{nameof(HazardDamageHandler)} on {name} requires a PlayerHealth component.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<HazardTriggeredEvent>(OnHazardTriggered);
            GameManager.EventService.Add<FallingRockImpactEvent>(OnFallingRockImpact);
            GameManager.EventService.Add<GasCloudDamageTickEvent>(OnGasCloudDamageTick);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<HazardTriggeredEvent>(OnHazardTriggered);
            GameManager.EventService.Remove<FallingRockImpactEvent>(OnFallingRockImpact);
            GameManager.EventService.Remove<GasCloudDamageTickEvent>(OnGasCloudDamageTick);
        }

        private void Update()
        {
            if (playerHealth == null || playerHealth.IsDead || mapGenerationService == null) return;
            HandleLavaContact();
        }

        private void OnHazardTriggered(HazardTriggeredEvent evt)
        {
            if (evt.Hazard != CustomBehavior.Explosive) return;
            TryApplyRadiusDamage(evt.LayerIndex, evt.X, evt.Y, hazardDamageRadius, explosiveDamage, DeathReason.Explosive, BlastResistanceOf);
        }

        private void OnFallingRockImpact(FallingRockImpactEvent evt) =>
            TryApplyRadiusDamage(evt.LayerIndex, evt.X, evt.Y, evt.Radius, fallingRockDamage, DeathReason.FallingRock, FallingRockResistanceOf);

        private void OnGasCloudDamageTick(GasCloudDamageTickEvent evt) =>
            TryApplyRadiusDamage(evt.LayerIndex, evt.X, evt.Y, evt.Radius, gasCloudTickDamage, DeathReason.GasPocket, GasResistanceOf);

        private void TryApplyRadiusDamage(int layerIndex, int x, int y, float radius, float baseDamage, DeathReason reason, Func<float> resistanceOf)
        {
            if (playerHealth == null || playerHealth.IsDead || mapGenerationService == null) return;

            Vector3 hazardWorldPos = mapGenerationService.CellToWorldCenter(layerIndex, x, y);
            if (Vector3.Distance(transform.position, hazardWorldPos) > radius) return;

            float damage = baseDamage * Mathf.Max(0f, 1f - resistanceOf());
            if (damage <= 0f) return;

            playerHealth.TakeDamage(damage, reason);
        }

        // Per-frame contact check against the player's current cell rather than an event - Lava's
        // only trigger moment is the initial mining (HazardEffectResolver already marks
        // CellData.HazardousSurface then), everything after that is "is the player standing on/in
        // an already-mined lava cell right now".
        private void HandleLavaContact()
        {
            if (!mapGenerationService.TryWorldToCellInBounds(transform.position, out int layerIndex, out int x, out int y)) return;
            if (!mapGenerationService.IsHazardousSurface(layerIndex, x, y))
            {
                lavaDamageTimer = 0f;
                return;
            }

            lavaDamageTimer += Time.deltaTime;
            if (lavaDamageTimer < lavaDamageTickSeconds) return;
            lavaDamageTimer = 0f;

            float damage = lavaDamage * Mathf.Max(0f, 1f - LavaResistanceOf());
            if (damage <= 0f) return;
            playerHealth.TakeDamage(damage, DeathReason.Lava);
        }

        // GameDesignDoc "Prestige > Survival": one resistance perk per hazard, same shape as the
        // pre-existing GasResistance (level * per-level value, multiplicatively reduces damage).
        private static float BlastResistanceOf() => PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.BlastResistance : 0f;
        private static float FallingRockResistanceOf() => PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.FallingRockResistance : 0f;
        private static float GasResistanceOf() => PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.GasResistance : 0f;
        private static float LavaResistanceOf() => PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.LavaResistance : 0f;
    }
}
