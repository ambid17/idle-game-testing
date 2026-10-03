using System.Collections;
using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace Effects
{
    // Shared world-space particle bursts (GameManager.WorldEffects): pixel-art dust puffs,
    // sparkles and explosions, used by the building reveal and prestige cinematics, the player's
    // fall landing, critter catches, artifact finds, treasure chests and Explosive blocks. Emission is driven by hand (Emit), like
    // Player.DigFeedback; each particle shows one random row of its vertical sheet.
    // Also hands out the loose ore chunks that mining debris, pickups and Depot deposits show.
    public class WorldEffects : MonoBehaviour
    {
        [Tooltip("Alpha-blended material (Sprites/Default) the effect sheets are drawn with.")]
        [SerializeField] private Material particleMaterial;
        [Tooltip("Dust puffs stacked vertically, one per row (Tools/Effects/make_reveal_fx.py).")]
        [SerializeField] private Texture2D dustSheet;
        [SerializeField, Min(1)] private int dustSheetRows = 4;
        [Tooltip("Sparkles stacked vertically, one per row (Tools/Effects/make_reveal_fx.py).")]
        [SerializeField] private Texture2D sparkleSheet;
        [SerializeField, Min(1)] private int sparkleSheetRows = 2;
        [Tooltip("Dust and sparkles draw above the buildings and the player.")]
        [SerializeField] private int sortingOrder = 5;

        [Tooltip("Tint of sparkles that don't ask for a colour of their own (the sparkle sheet is white).")]
        [SerializeField] private Color defaultSparkleColor = new(0.45f, 1f, 1f, 1f);

        [Header("Explosion")]
        [Tooltip("Fireball puffs stacked vertically, one per row (Tools/Effects/make_explosion_fx.py).")]
        [SerializeField] private Texture2D fireSheet;
        [SerializeField, Min(1)] private int fireSheetRows = 4;
        [Tooltip("Spiky blast flashes stacked vertically: row 0 the big main flash, row 1 a small pop (Tools/Effects/make_explosion_fx.py).")]
        [SerializeField] private Texture2D flashSheet;
        [SerializeField] private Color explosionSparkColor = new(1f, 0.75f, 0.3f, 1f);
        [Tooltip("Dark tint over the pale dust art for the smoke an explosion leaves behind.")]
        [SerializeField] private Color explosionSmokeColor = new(0.3f, 0.27f, 0.33f, 0.9f);

        [Header("Ore chunks")]
        [Tooltip("Loose chunks cut out of each ore's tile art: column = BlockTypeId, one row per variant (Tools/Effects/make_ore_chunks.py).")]
        [SerializeField] private Texture2D oreChunkSheet;
        [SerializeField, Min(1)] private int oreChunkVariants = 4;
        [Tooltip("Matches the ore tile art, so a chunk is the size it was in its tile.")]
        [SerializeField, Min(1f)] private float oreChunkPixelsPerUnit = 128f;

        private ParticleSystem dustSystem;
        private ParticleSystem sparkleSystem;
        private ParticleSystem fireSystem;
        private ParticleSystem flashSystem;
        private ParticleSystem popSystem;
        private readonly Dictionary<BlockTypeId, Sprite[]> oreChunkSprites = new();

        public Texture2D OreChunkSheet => oreChunkSheet;
        public int OreChunkVariants => oreChunkVariants;
        // Cells are square, so the sheet's height gives their size.
        public int OreChunkColumns => oreChunkSheet.width / (oreChunkSheet.height / oreChunkVariants);

        // The sparkle sheet's rows as sprites, for UI effects (UI.UiFx) that want the same art.
        private Sprite[] sparkleSprites;
        public Sprite SparkleSprite(int index)
        {
            if (sparkleSprites == null)
            {
                int cell = sparkleSheet.height / sparkleSheetRows;
                sparkleSprites = new Sprite[sparkleSheetRows];
                for (int i = 0; i < sparkleSheetRows; i++)
                {
                    sparkleSprites[i] = Sprite.Create(sparkleSheet, new Rect(0, i * cell, sparkleSheet.width, cell), new Vector2(0.5f, 0.5f), cell);
                }
            }
            return sparkleSprites[Mathf.Abs(index) % sparkleSprites.Length];
        }

        public bool HasOreChunks(BlockType block) => block.Category == BlockCategory.Ore && (int)block.Id < OreChunkColumns;

        // A random loose chunk of this ore, or null for blocks that have none (non-ores).
        public Sprite OreChunk(BlockType block)
        {
            if (!HasOreChunks(block)) return null;

            if (!oreChunkSprites.TryGetValue(block.Id, out var sprites))
            {
                int cell = oreChunkSheet.height / oreChunkVariants;
                sprites = new Sprite[oreChunkVariants];
                for (int i = 0; i < oreChunkVariants; i++)
                {
                    var rect = new Rect((int)block.Id * cell, i * cell, cell, cell);
                    sprites[i] = Sprite.Create(oreChunkSheet, rect, new Vector2(0.5f, 0.5f), oreChunkPixelsPerUnit);
                }
                oreChunkSprites[block.Id] = sprites;
            }
            return sprites[Random.Range(0, sprites.Length)];
        }

        private void Awake()
        {
            if (oreChunkSheet == null) Debug.LogError($"{nameof(WorldEffects)}.oreChunkSheet is not assigned.");
            if (particleMaterial == null) Debug.LogError($"{nameof(WorldEffects)}.particleMaterial is not assigned.");
            if (dustSheet == null) Debug.LogError($"{nameof(WorldEffects)}.dustSheet is not assigned.");
            if (sparkleSheet == null) Debug.LogError($"{nameof(WorldEffects)}.sparkleSheet is not assigned.");
            if (fireSheet == null) Debug.LogError($"{nameof(WorldEffects)}.fireSheet is not assigned.");
            if (flashSheet == null) Debug.LogError($"{nameof(WorldEffects)}.flashSheet is not assigned.");

            var dustMaterial = new Material(particleMaterial) { name = "World Dust", mainTexture = dustSheet };
            dustSystem = CreateSystem("World Dust", dustMaterial, dustSheetRows, 200, gravity: -0.03f);
            var dustSize = dustSystem.sizeOverLifetime;
            dustSize.enabled = true;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.25f)));
            // Puffs shoot out and quickly lose their speed, like a real dust ring.
            var dustDrag = dustSystem.limitVelocityOverLifetime;
            dustDrag.enabled = true;
            dustDrag.limit = 0.3f;
            dustDrag.dampen = 0.08f;
            FadeOutAtEnd(dustSystem, 0.45f);

            var sparkleMaterial = new Material(particleMaterial) { name = "World Sparkles", mainTexture = sparkleSheet };
            sparkleSystem = CreateSystem("World Sparkles", sparkleMaterial, sparkleSheetRows, 400, gravity: 0f);
            FadeOutAtEnd(sparkleSystem, 0.6f);

            // Fire draws over the smoke (dust) it leaves behind, the flash over both.
            var fireMaterial = new Material(particleMaterial) { name = "World Fire", mainTexture = fireSheet };
            fireSystem = CreateSystem("World Fire", fireMaterial, fireSheetRows, 100, gravity: -0.06f);
            fireSystem.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder + 1;
            var fireSize = fireSystem.sizeOverLifetime;
            fireSize.enabled = true;
            fireSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.75f)));
            var fireDrag = fireSystem.limitVelocityOverLifetime;
            fireDrag.enabled = true;
            fireDrag.limit = 0.4f;
            fireDrag.dampen = 0.12f;
            // Burns down: full colour, then darkens towards smoke as it fades.
            var fireColor = fireSystem.colorOverLifetime;
            fireColor.enabled = true;
            var burnDown = new Gradient();
            burnDown.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.45f), new GradientColorKey(new Color(0.55f, 0.42f, 0.45f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fireColor.color = burnDown;

            var flashMaterial = new Material(particleMaterial) { name = "World Flash", mainTexture = flashSheet };
            flashSystem = CreateFlashSystem("World Flash", flashMaterial, 0);
            popSystem = CreateFlashSystem("World Flash Pops", flashMaterial, 1);
        }

        // One system per flash sheet row: Emit can't choose a row per particle.
        private ParticleSystem CreateFlashSystem(string systemName, Material material, int row)
        {
            var system = CreateSystem(systemName, material, 2, 20, gravity: 0f);
            system.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder + 2;
            var sheet = system.textureSheetAnimation;
            sheet.rowMode = ParticleSystemAnimationRowMode.Custom;
            sheet.rowIndex = row;
            // Pops open, then shrinks back as it fades.
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.7f)));
            FadeOutAtEnd(system, 0.4f);
            return system;
        }

        // A blast filling a circle of the given world radius: a spiky flash, a fireball core and a
        // ring of fireballs thrown to the edge, sparks, a few small follow-up pops, then dark smoke
        // billowing up - drawn with the same chunky outlined art as the dust and sparkles.
        public void Explosion(Vector3 center, float radius) => StartCoroutine(PlayExplosion(center, radius));

        private IEnumerator PlayExplosion(Vector3 center, float radius)
        {
            EmitFlash(center, radius * 1.9f, 0.22f, small: false);
            for (int i = 0; i < 5; i++)
            {
                Vector2 direction = Random.insideUnitCircle;
                EmitFire(center + (Vector3)(direction * (radius * 0.25f)), direction * (radius * 1.5f), radius * Random.Range(0.75f, 1f), Random.Range(0.45f, 0.6f));
            }
            SparkleBurst(center, 22, radius * 0.2f, radius * 5f, explosionSparkColor);

            yield return new WaitForSeconds(0.05f);

            const int ringPuffs = 9;
            float startAngle = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < ringPuffs; i++)
            {
                float angle = startAngle + (i + Random.Range(-0.3f, 0.3f)) * (Mathf.PI * 2f / ringPuffs);
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                EmitFire(center + direction * (radius * 0.3f), direction * (radius * Random.Range(2f, 3f)), radius * Random.Range(0.6f, 0.8f), Random.Range(0.35f, 0.5f));
            }

            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(Random.Range(0.04f, 0.08f));
                EmitFlash(center + (Vector3)(Random.insideUnitCircle * (radius * 0.7f)), radius * Random.Range(0.5f, 0.75f), 0.14f, small: true);
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2 direction = Random.insideUnitCircle;
                Vector3 velocity = (Vector3)(direction * (radius * 1.2f)) + Vector3.up * Random.Range(0.8f, 1.6f);
                EmitDust(center + (Vector3)(direction * (radius * 0.5f)), velocity, radius * Random.Range(0.55f, 0.8f), Random.Range(0.9f, 1.4f), Mathf.Sign(direction.x), explosionSmokeColor);
            }
        }

        private void EmitFire(Vector3 position, Vector3 velocity, float size, float lifetime)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                rotation = Random.Range(-25f, 25f),
                angularVelocity = Random.Range(-60f, 60f),
                applyShapeToPosition = false,
            };
            fireSystem.Emit(emitParams, 1);
        }

        private void EmitFlash(Vector3 position, float size, float lifetime, bool small)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                startSize = size,
                startLifetime = lifetime,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false,
            };
            (small ? popSystem : flashSystem).Emit(emitParams, 1);
        }

        // A ring of puffs thrown out sideways along the ground from under something that just
        // landed, plus a few that billow up in front of it. halfWidth: half the landed thing's
        // width. scale: 1 = a building; puff size and throw distance both follow it.
        public void DustRing(Vector3 groundCenter, float halfWidth, int puffs, float scale = 1f)
        {
            for (int i = 0; i < puffs; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool billow = i % 5 == 4;
                float along = billow ? Random.Range(0f, 0.8f) : Random.Range(0.6f, 1.05f);
                Vector3 position = groundCenter + new Vector3(side * halfWidth * along, Random.Range(0.05f, 0.35f) * scale, 0f);
                Vector3 velocity = billow
                    ? new Vector3(side * Random.Range(0.3f, 1.2f), Random.Range(1.5f, 3f), 0f)
                    : new Vector3(side * Random.Range(2.5f, 6.5f), Random.Range(0.2f, 1.6f), 0f);
                EmitDust(position, velocity * scale, Random.Range(0.6f, 1.25f) * scale, Random.Range(0.7f, 1.3f) * Mathf.Lerp(0.6f, 1f, scale), side);
            }
        }

        // A small round poof, e.g. something vanishing.
        public void Puff(Vector3 center, int puffs, float size) => Puff(center, puffs, size, Color.white, Vector3.zero);

        // color tints the (pale) dust art, e.g. dark for smoke; drift is added to every puff's velocity.
        public void Puff(Vector3 center, int puffs, float size, Color color, Vector3 drift)
        {
            for (int i = 0; i < puffs; i++)
            {
                Vector2 direction = Random.insideUnitCircle;
                EmitDust(center + (Vector3)(direction * size * 0.3f), (Vector3)(direction * (size * 3f)) + drift, size * Random.Range(0.7f, 1.2f), Random.Range(0.35f, 0.6f), Mathf.Sign(direction.x), color);
            }
        }

        public void SparkleBurst(Vector3 center, int count, float radius, float speed) => SparkleBurst(center, count, radius, speed, defaultSparkleColor);

        // Sparkles flung outward from a ring of the given radius.
        public void SparkleBurst(Vector3 center, int count, float radius, float speed, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                Vector3 velocity = direction * (speed * Random.Range(0.4f, 1f));
                Sparkle(center + (Vector3)(direction * radius), velocity, Random.Range(0.25f, 0.55f), Random.Range(0.4f, 0.8f), color);
            }
        }

        public void Sparkle(Vector3 position, Vector3 velocity, float size, float lifetime) => Sparkle(position, velocity, size, lifetime, defaultSparkleColor);

        public void Sparkle(Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
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
            sparkleSystem.Emit(emitParams, 1);
        }

        private void EmitDust(Vector3 position, Vector3 velocity, float size, float lifetime, float spinDirection) => EmitDust(position, velocity, size, lifetime, spinDirection, Color.white);

        private void EmitDust(Vector3 position, Vector3 velocity, float size, float lifetime, float spinDirection, Color color)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = Random.Range(-20f, 20f),
                angularVelocity = spinDirection * Random.Range(20f, 90f),
                applyShapeToPosition = false,
            };
            dustSystem.Emit(emitParams, 1);
        }

        private ParticleSystem CreateSystem(string systemName, Material material, int sheetRows, int maxParticles, float gravity)
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

            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 1;
            sheet.numTilesY = sheetRows;
            sheet.animation = ParticleSystemAnimationType.SingleRow;
            sheet.rowMode = ParticleSystemAnimationRowMode.Random;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;

            system.Play();
            return system;
        }

        // Full opacity until fadeStart, then out to 0.
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
