using System.Collections.Generic;
using Events;
using MapGeneration;
using UnityEngine;

namespace Atmosphere
{
    // Ambient life around the player, driven by the LayerConfig of whatever layer they're in:
    //  - dust sifting down from the ceiling over freshly dug cells (anyone's digging, via CellMinedEvent),
    //  - water drips falling from open ceilings nearby,
    //  - glowing spores drifting up through open ground in deeper layers,
    //  - the layer's ambient loop, crossfaded through AudioService.SetAmbience.
    // Particles are emitted by hand (ParticleSystem.Emit) into three code-built systems at spots
    // sampled from the grid, so they only ever appear in real tunnels and never over fog.
    // Scene-placed singleton (child of GameManager) for its player reference.
    public class MineAtmosphere : Singleton<MineAtmosphere>
    {
        private struct FreshDig
        {
            public Vector3 CeilingPoint;
            public float Age;
            public float Accumulator;
        }

        private const int MaxFreshDigs = 48;
        // Digs further than this from the player (automatons far away) don't bother shedding dust.
        private const float DustMaxDistance = 18f;
        private const float CellScanInterval = 0.5f;
        private const int ParticleSortingOrder = 2;

        [SerializeField] private Transform player;

        private static AtmosphereConfig config => GameManager.AtmosphereConfig;
        private static MapGenerationService map => GameManager.MapGenerationService;

        private readonly List<FreshDig> freshDigs = new();
        private readonly List<Vector3> openCells = new();
        private readonly List<Vector3> ceilingCells = new();
        private float nextCellScanTime;
        private ParticleSystem dustSystem;
        private ParticleSystem dripSystem;
        private ParticleSystem sporeSystem;
        private LayerConfig currentLayerConfig;
        private int currentLayerIndex = -1;
        private float dripAccumulator;
        private float sporeAccumulator;

        protected override void Initialize()
        {
            base.Initialize();
            if (player == null) Debug.LogError("MineAtmosphere.player is not assigned.");

            dustSystem = CreateSystem("Dust", maxParticles: 400, gravity: 0.08f, collideWithGround: false);
            dripSystem = CreateSystem("Drips", maxParticles: 100, gravity: 1.2f, collideWithGround: true);
            sporeSystem = CreateSystem("Spores", maxParticles: 300, gravity: 0f, collideWithGround: false);
            AddDrift(sporeSystem, 0.25f);
            AddDrift(dustSystem, 0.08f);
        }

        private void OnEnable() => GameManager.EventService.Add<CellMinedEvent>(OnCellMined);
        private void OnDisable() => GameManager.EventService.Remove<CellMinedEvent>(OnCellMined);

        private void Update()
        {
            UpdateLayer();
            if (currentLayerConfig == null) return;

            float dt = Time.deltaTime;
            UpdateDust(dt);

            dripAccumulator += currentLayerConfig.DripsPerSecond * dt;
            while (dripAccumulator >= 1f)
            {
                dripAccumulator -= 1f;
                EmitDrip();
            }

            sporeAccumulator += currentLayerConfig.SporesPerSecond * dt;
            while (sporeAccumulator >= 1f)
            {
                sporeAccumulator -= 1f;
                EmitSpore();
            }
        }

        private void UpdateLayer()
        {
            int layerIndex = GameManager.LayerConfigProvider.GetLayerIndexAtWorldY(player.position.y, map.CellSize);
            if (layerIndex == currentLayerIndex) return;

            currentLayerIndex = layerIndex;
            currentLayerConfig = GameManager.LayerConfigProvider.GetConfig(layerIndex);
        }

        #region Dust

        // Only a dig that leaves solid ceiling directly above it sheds dust.
        private void OnCellMined(CellMinedEvent evt)
        {
            var center = map.CellToWorldCenter(evt.LayerIndex, evt.X, evt.Y);
            if ((center - player.position).sqrMagnitude > DustMaxDistance * DustMaxDistance) return;

            float cell = map.CellSize;
            if (map.IsOpenAt(center + Vector3.up * cell)) return;

            if (freshDigs.Count >= MaxFreshDigs) freshDigs.RemoveAt(0);
            freshDigs.Add(new FreshDig { CeilingPoint = center + Vector3.up * (cell * 0.5f - 0.02f) });
        }

        private void UpdateDust(float dt)
        {
            float lifetime = config.DustSecondsAfterDig;
            var color = Color.Lerp(config.DustColor, currentLayerConfig.AmbientParticleColor, 0.3f);
            color.a = config.DustColor.a;

            for (int i = freshDigs.Count - 1; i >= 0; i--)
            {
                var dig = freshDigs[i];
                dig.Age += dt;
                if (dig.Age >= lifetime)
                {
                    freshDigs.RemoveAt(i);
                    continue;
                }

                // Tapers off as the ceiling settles.
                float falloff = 1f - dig.Age / lifetime;
                dig.Accumulator += config.DustPerSecondPerCell * falloff * dt;
                while (dig.Accumulator >= 1f)
                {
                    dig.Accumulator -= 1f;
                    var position = dig.CeilingPoint + Vector3.right * Random.Range(-0.45f, 0.45f) * map.CellSize;
                    Emit(dustSystem, position, new Vector3(Random.Range(-0.05f, 0.05f), -Random.Range(0.1f, 0.35f), 0f),
                        Random.Range(0.03f, 0.07f), Random.Range(1.5f, 2.5f), color);
                }
                freshDigs[i] = dig;
            }
        }

        #endregion

        private void EmitDrip()
        {
            if (!TrySampleOpenCell(requireCeiling: true, out var cellCenter)) return;

            float cell = map.CellSize;
            // Starts a little below the ceiling so its collider doesn't immediately hit the rock it hangs from.
            var position = cellCenter + new Vector3(Random.Range(-0.35f, 0.35f) * cell, cell * 0.5f - 0.12f, 0f);
            Emit(dripSystem, position, Vector3.zero, Random.Range(0.04f, 0.06f), 2.5f, config.DripColor);
        }

        private void EmitSpore()
        {
            if (!TrySampleOpenCell(requireCeiling: false, out var cellCenter)) return;

            float cell = map.CellSize;
            var position = cellCenter + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f) * cell;
            var color = currentLayerConfig.AmbientParticleColor;
            Emit(sporeSystem, position, new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(0.08f, 0.25f), 0f),
                Random.Range(0.06f, 0.14f), Random.Range(4f, 7f), color);
        }

        // A random revealed, open cell near the player - optionally one with solid rock right above it.
        // Picks from a cache rebuilt a couple of times a second: open tunnel is usually a small
        // fraction of the area around the player, so blind random probing mostly missed.
        private bool TrySampleOpenCell(bool requireCeiling, out Vector3 cellCenter)
        {
            if (Time.time >= nextCellScanTime) ScanNearbyCells();

            var pool = requireCeiling ? ceilingCells : openCells;
            if (pool.Count == 0)
            {
                cellCenter = default;
                return false;
            }
            cellCenter = pool[Random.Range(0, pool.Count)];
            return true;
        }

        private void ScanNearbyCells()
        {
            nextCellScanTime = Time.time + CellScanInterval;
            openCells.Clear();
            ceilingCells.Clear();

            float cell = map.CellSize;
            int radius = config.AmbientRadiusCells;
            // Snap to cell centers (cell edges sit on whole multiples of cellSize).
            float originX = (Mathf.Floor(player.position.x / cell) + 0.5f) * cell;
            float originY = (Mathf.Floor(player.position.y / cell) + 0.5f) * cell;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var center = new Vector3(originX + dx * cell, originY + dy * cell, 0f);
                    // Open sky above the surface isn't "the mine".
                    if (center.y > 0f) continue;
                    if (!map.IsOpenAt(center) || !map.IsRevealedAt(center)) continue;

                    openCells.Add(center);
                    if (!map.IsOpenAt(center + Vector3.up * cell)) ceilingCells.Add(center);
                }
            }
        }

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                applyShapeToPosition = false,
            };
            system.Emit(emitParams, 1);
        }

        private ParticleSystem CreateSystem(string systemName, int maxParticles, float gravity, bool collideWithGround)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            // Fade in quickly, hold, fade out - on top of each particle's own start colour.
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            if (collideWithGround)
            {
                // Drips die where they hit the terrain's TilemapCollider2D.
                var collision = system.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision2D;
                collision.collidesWith = LayerMask.GetMask("Ground");
                collision.lifetimeLoss = 1f;
                collision.bounce = 0f;
                collision.radiusScale = 0.2f;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GlowSprites.ParticleMaterial;
            renderer.sortingOrder = ParticleSortingOrder;

            system.Play();
            return system;
        }

        private static void AddDrift(ParticleSystem system, float strength)
        {
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.2f;
        }
    }
}
