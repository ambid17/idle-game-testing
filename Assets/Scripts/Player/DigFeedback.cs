using System.Collections;
using System.Collections.Generic;
using Atmosphere;
using MapGeneration;
using UnityEngine;

namespace Player
{
    // Purely cosmetic "juice" for the player's digging, driven by PlayerMining:
    //  - Hit: a few chips flick off the block face toward the player on every pickaxe hit.
    //  - Break: a burst of chunks (tinted by the block, plus dirt-coloured ones for ores) and a dust
    //    puff, a screen shake scaled by the block's effective health, and a brief hit-stop on hard blocks.
    //  - Pickup: collected ore/artifacts pop out of the cell as tinted nuggets that home in on the
    //    player. The inventory is credited immediately by PlayerMining as before - this is visual only.
    // Shake goes through GameManager.CameraShake (which honours the Options "Screen Shake" toggle).
    public class DigFeedback : MonoBehaviour
    {
        private struct Nugget
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Age;
        }

        private const int DebrisSheetRows = 4;
        private const int MaxNuggets = 24;

        [Header("Rendering")]
        [Tooltip("Alpha-blended material (Sprites/Default) carrying the DebrisChips sheet - 4 chip shapes stacked vertically.")]
        [SerializeField] private Material debrisMaterial;
        [Tooltip("Grayscale nugget sprite, tinted with the block's MinimapColor.")]
        [SerializeField] private Sprite nuggetSprite;
        [Tooltip("Debris and nuggets draw above terrain, fog and the player.")]
        [SerializeField] private int sortingOrder = 6;

        [Header("Debris")]
        [SerializeField, Min(0)] private int chipsPerHit = 3;
        [SerializeField, Min(0)] private int chunksPerBreak = 12;
        [Tooltip("Share of an ore's break chunks tinted with the ore colour - the rest use the dirt colour of the tile background.")]
        [SerializeField, Range(0f, 1f)] private float oreChunkFraction = 0.45f;
        [SerializeField, Min(0)] private int dustPerBreak = 5;

        [Header("Screen shake (scaled by the broken block's effective health)")]
        [Tooltip("Effective health (BlockType.Health x layer BlockHealth) at which shake starts, and at which it's strongest.")]
        [SerializeField] private Vector2 shakeHealthRange = new(0.4f, 3f);
        [SerializeField, Min(0f)] private float minShakeForce = 0.04f;
        [SerializeField, Min(0f)] private float maxShakeForce = 0.2f;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.15f;

        [Header("Hit-stop (brief freeze when a hard block breaks)")]
        [SerializeField, Min(0f)] private float hitStopHealthThreshold = 1.5f;
        [SerializeField, Min(0f)] private float hitStopSeconds = 0.05f;
        [Tooltip("Minimum real time between hit-stops, so vein mining or fast digging can't stutter.")]
        [SerializeField, Min(0f)] private float hitStopCooldown = 0.4f;

        [Header("Pickup nuggets")]
        [SerializeField, Min(0.05f)] private float nuggetSize = 0.4f;
        [Tooltip("Seconds a nugget arcs out of the cell before homing in on the player.")]
        [SerializeField, Min(0f)] private float nuggetPopSeconds = 0.3f;
        [SerializeField, Min(0f)] private float nuggetHomingAcceleration = 60f;
        [SerializeField, Min(0.5f)] private float nuggetMaxSpeed = 22f;

        private ParticleSystem debrisSystem;
        private ParticleSystem dustSystem;
        private ParticleSystem sparkleSystem;
        private readonly List<Nugget> nuggets = new();
        private readonly Stack<Transform> nuggetPool = new();
        private Coroutine hitStopRoutine;
        private float nextHitStopTime;
        private Color dirtColor;

        private void Awake()
        {
            if (debrisMaterial == null) Debug.LogError($"{nameof(DigFeedback)} on {name} is missing its debrisMaterial reference.");
            if (nuggetSprite == null) Debug.LogError($"{nameof(DigFeedback)} on {name} is missing its nuggetSprite reference.");
        }

        private void Start()
        {
            dirtColor = GameManager.BlockTypeDatabase.Get((byte)BlockTypeId.Dirt).MinimapColor;

            debrisSystem = CreateSystem("Dig Debris", debrisMaterial, 300, gravity: 2.2f);
            var sheet = debrisSystem.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 1;
            sheet.numTilesY = DebrisSheetRows;
            sheet.animation = ParticleSystemAnimationType.SingleRow;
            sheet.rowMode = ParticleSystemAnimationRowMode.Random;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            var collision = debrisSystem.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = LayerMask.GetMask("Ground");
            collision.bounce = 0.35f;
            collision.dampen = 0.35f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.5f;
            FadeOutAtEnd(debrisSystem, 0.75f);

            var dustMaterial = new Material(debrisMaterial) { name = "Dig Dust", mainTexture = GlowSprites.SoftDot.texture };
            dustSystem = CreateSystem("Dig Dust", dustMaterial, 100, gravity: -0.02f);
            FadeOutAtEnd(dustSystem, 0.2f);
            var dustSize = dustSystem.sizeOverLifetime;
            dustSize.enabled = true;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

            sparkleSystem = CreateSystem("Pickup Sparkles", GlowSprites.ParticleMaterial, 150, gravity: 0f);
            FadeOutAtEnd(sparkleSystem, 0.3f);
        }

        private void OnDisable()
        {
            // A hit-stop cut short (scene unload, player disabled) must not leave the game frozen.
            if (hitStopRoutine == null) return;
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
            Time.timeScale = 1f;
        }

        private void Update() => UpdateNuggets(Time.deltaTime);

        // A pickaxe hit on the block at cellCenter, dug in digDirection (from the player toward the block).
        public void Hit(Vector3 cellCenter, Vector2Int digDirection, BlockType block)
        {
            Vector3 away = -(Vector2)digDirection;
            Vector3 face = cellCenter + away * 0.45f;
            for (int i = 0; i < chipsPerHit; i++)
            {
                var velocity = away * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(1f, 2.5f) + (Vector3)(Random.insideUnitCircle * 0.8f);
                EmitDebris(face + (Vector3)(Random.insideUnitCircle * 0.2f), velocity, Random.Range(0.08f, 0.14f), Random.Range(0.35f, 0.55f), ChipColor(block));
            }
        }

        // The block at cellCenter broke. primary = the block the player was working on (vein-mined
        // bonus cells pass false: they get debris, but no extra shake or hit-stop).
        public void Break(Vector3 cellCenter, Vector2Int digDirection, BlockType block, float effectiveHealth, bool primary)
        {
            for (int i = 0; i < chunksPerBreak; i++)
            {
                var offset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f);
                var velocity = offset.normalized * Random.Range(1f, 3.5f) + Vector3.up * Random.Range(1.5f, 3.5f);
                EmitDebris(cellCenter + offset, velocity, Random.Range(0.12f, 0.24f), Random.Range(0.6f, 1.1f), ChipColor(block));
            }

            var dust = Color.Lerp(dirtColor, Color.white, 0.35f);
            dust.a = 0.45f;
            for (int i = 0; i < dustPerBreak; i++)
            {
                var velocity = (Vector3)(Random.insideUnitCircle * 0.6f) + Vector3.up * 0.2f;
                Emit(dustSystem, cellCenter + (Vector3)(Random.insideUnitCircle * 0.3f), velocity, Random.Range(0.5f, 0.9f), Random.Range(0.5f, 0.8f), dust, 0f);
            }

            if (!primary) return;

            float strength = Mathf.InverseLerp(shakeHealthRange.x, shakeHealthRange.y, effectiveHealth);
            if (effectiveHealth >= shakeHealthRange.x)
            {
                GameManager.CameraShake.Shake((Vector2)digDirection * Mathf.Lerp(minShakeForce, maxShakeForce, strength), shakeSeconds);
            }

            if (effectiveHealth >= hitStopHealthThreshold && hitStopSeconds > 0f && Time.unscaledTime >= nextHitStopTime && hitStopRoutine == null)
            {
                nextHitStopTime = Time.unscaledTime + hitStopCooldown;
                hitStopRoutine = StartCoroutine(HitStop());
            }
        }

        // Nuggets popping out of cellCenter and flying to the player - one per unit collected.
        public void Pickup(Vector3 cellCenter, BlockType block, int count)
        {
            for (int i = 0; i < count && nuggets.Count < MaxNuggets; i++)
            {
                var nuggetTransform = nuggetPool.Count > 0 ? nuggetPool.Pop() : CreateNugget();
                nuggetTransform.position = cellCenter;
                nuggetTransform.GetComponent<SpriteRenderer>().color = block.MinimapColor;
                var glowColor = block.MinimapColor;
                glowColor.a = 0.5f;
                nuggetTransform.GetChild(0).GetComponent<SpriteRenderer>().color = glowColor;
                nuggetTransform.gameObject.SetActive(true);

                nuggets.Add(new Nugget
                {
                    Transform = nuggetTransform,
                    Velocity = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(3.5f, 5f), 0f),
                    Age = 0f,
                });
            }
        }

        private IEnumerator HitStop()
        {
            // Only freeze from normal speed - never stomp on anything else that changed timeScale.
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                hitStopRoutine = null;
                yield break;
            }

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopSeconds);
            Time.timeScale = 1f;
            hitStopRoutine = null;
        }

        private void UpdateNuggets(float dt)
        {
            Vector3 target = transform.position;
            for (int i = nuggets.Count - 1; i >= 0; i--)
            {
                var nugget = nuggets[i];
                nugget.Age += dt;
                Vector3 position = nugget.Transform.position;

                if (nugget.Age < nuggetPopSeconds)
                {
                    nugget.Velocity += Vector3.down * (14f * dt);
                }
                else
                {
                    // Steer toward the player, accelerating, so the pop arc bends smoothly into a dash.
                    Vector3 toTarget = target - position;
                    nugget.Velocity += toTarget.normalized * (nuggetHomingAcceleration * dt);
                    nugget.Velocity = Vector3.ClampMagnitude(nugget.Velocity, nuggetMaxSpeed);
                    // Bleed off sideways drift so nuggets don't orbit the player.
                    nugget.Velocity = Vector3.Lerp(nugget.Velocity, toTarget.normalized * nugget.Velocity.magnitude, 6f * dt);
                }

                position += nugget.Velocity * dt;
                nugget.Transform.position = position;
                // Slight squash-and-stretch pulse while flying.
                float pulse = 1f + 0.12f * Mathf.Sin(nugget.Age * 30f);
                nugget.Transform.localScale = new Vector3(nuggetSize * pulse, nuggetSize / pulse, 1f);

                bool arrived = nugget.Age >= nuggetPopSeconds && (target - position).sqrMagnitude < 0.3f * 0.3f;
                // Safety net: a nugget that somehow never arrives still cleans itself up.
                if (arrived || nugget.Age > 3f)
                {
                    if (arrived) EmitSparkles(position, nugget.Transform.GetComponent<SpriteRenderer>().color);
                    nugget.Transform.gameObject.SetActive(false);
                    nuggetPool.Push(nugget.Transform);
                    nuggets.RemoveAt(i);
                    continue;
                }
                nuggets[i] = nugget;
            }
        }

        private Transform CreateNugget()
        {
            var go = new GameObject("Pickup Nugget");
            go.transform.SetParent(transform.parent, false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = nuggetSprite;
            body.sortingOrder = sortingOrder + 1;

            var glow = GlowSprites.CreateGlow(go.transform, Color.white, 2.2f);
            glow.sortingOrder = sortingOrder;
            return go.transform;
        }

        private void EmitSparkles(Vector3 position, Color color)
        {
            var sparkle = Color.Lerp(color, Color.white, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                var velocity = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1f, 2.5f));
                Emit(sparkleSystem, position, velocity, Random.Range(0.08f, 0.16f), Random.Range(0.25f, 0.4f), sparkle, 0f);
            }
        }

        // Ores and artifacts shed a mix of their own colour and the dirt of their tile background.
        private Color ChipColor(BlockType block)
        {
            bool hasDirtBackground = block.Category == BlockCategory.Ore || block.Category == BlockCategory.Artifact;
            var color = hasDirtBackground && Random.value > oreChunkFraction ? dirtColor : block.MinimapColor;
            // Small per-chip brightness variation keeps a burst from looking flat.
            float shade = Random.Range(0.85f, 1.1f);
            return new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
        }

        private void EmitDebris(Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            Emit(debrisSystem, position, velocity, size, lifetime, color, Random.Range(0f, 360f));
        }

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float lifetime, Color color, float rotation)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = rotation,
                angularVelocity = Random.Range(-360f, 360f),
                applyShapeToPosition = false,
            };
            system.Emit(emitParams, 1);
        }

        private ParticleSystem CreateSystem(string systemName, Material material, int maxParticles, float gravity)
        {
            var go = new GameObject(systemName);
            // Not parented to the player: world-space particles shouldn't inherit its sorting group or scale.
            go.transform.SetParent(transform.parent, false);
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

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;

            system.Play();
            return system;
        }

        // Full opacity (on top of each particle's start colour) until fadeStart, then out to 0.
        private static void FadeOutAtEnd(ParticleSystem system, float fadeStart)
        {
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }
    }
}
