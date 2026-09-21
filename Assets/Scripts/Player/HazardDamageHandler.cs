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
    // does - see MapGeneration.HazardEffectResolver). Explosive/Lava resolve straight off
    // HazardTriggeredEvent (an instant hit the moment the block is mined); FallingRock/GasPocket
    // listen for the delayed/lingering events HazardEffectResolver's spawned effects dispatch once
    // their own telegraph/lifetime elapses. Lava additionally gets a per-frame check for the player
    // standing on top of a still-unmined Lava block - once mined it behaves like any other block
    // (see MapGenerationService.MineCell), no lingering hazard.
    public class HazardDamageHandler : MonoBehaviour
    {
        [SerializeField] private float hazardDamageRadius = 3f;
        [SerializeField] private float explosiveDamage = 25f;
        [SerializeField] private float fallingRockDamage = 20f;
        [SerializeField] private float gasCloudTickDamage = 8f;
        [SerializeField] private float lavaMineDamage = 15f;
        [SerializeField] private float lavaDamage = 10f;
        [SerializeField] private float lavaDamageTickSeconds = 1f;

        private PlayerHealth playerHealth;
        private PlayerController playerController;
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;
        private float lavaDamageTimer;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null) Debug.LogError($"{nameof(HazardDamageHandler)} on {name} requires a PlayerHealth component.");

            playerController = GetComponent<PlayerController>();
            if (playerController == null) Debug.LogError($"{nameof(HazardDamageHandler)} on {name} requires a PlayerController component.");
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
            HandleLavaUnderfoot();
        }

        private void OnHazardTriggered(HazardTriggeredEvent evt)
        {
            switch (evt.Hazard)
            {
                case CustomBehavior.Explosive:
                    TryApplyRadiusDamage(evt.LayerIndex, evt.X, evt.Y, hazardDamageRadius, explosiveDamage, DeathReason.Explosive, BlastResistanceOf);
                    break;
                case CustomBehavior.Lava:
                    TryApplyRadiusDamage(evt.LayerIndex, evt.X, evt.Y, hazardDamageRadius, lavaMineDamage, DeathReason.Lava, LavaResistanceOf);
                    break;
            }
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

        // Per-frame check rather than an event - Lava has no trigger moment while standing on it,
        // just "is there a still-unmined Lava block directly underfoot right now" (same below-cell
        // resolution FallingRockHazardEffect.Fall uses to check what it's landing on).
        private void HandleLavaUnderfoot()
        {
            if (playerController == null || !playerController.IsGrounded)
            {
                lavaDamageTimer = 0f;
                return;
            }

            if (!mapGenerationService.TryWorldToCellInBounds(transform.position, out int layerIndex, out int x, out int y))
            {
                lavaDamageTimer = 0f;
                return;
            }

            Vector3 belowWorldPos = mapGenerationService.CellToWorldCenter(layerIndex, x, y) + Vector3.down * mapGenerationService.CellSize;
            bool belowResolved = mapGenerationService.TryWorldToCellInBounds(belowWorldPos, out int belowLayer, out int belowX, out int belowY);
            var belowBlock = belowResolved ? mapGenerationService.GetBlockTypeAt(belowLayer, belowX, belowY) : null;

            if (belowBlock == null || belowBlock.CustomBehavior != CustomBehavior.Lava)
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
