using UnityEngine;

namespace Effects
{
    // Shared world-space particle bursts (GameManager.WorldEffects): pixel-art dust puffs and
    // sparkles, used by the building reveal and prestige cinematics, the player's fall landing,
    // critter catches, artifact finds and treasure chests. Emission is driven by hand (Emit), like
    // Player.DigFeedback; each particle shows one random row of its vertical sheet.
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

        private ParticleSystem dustSystem;
        private ParticleSystem sparkleSystem;

        private void Awake()
        {
            if (particleMaterial == null) Debug.LogError($"{nameof(WorldEffects)}.particleMaterial is not assigned.");
            if (dustSheet == null) Debug.LogError($"{nameof(WorldEffects)}.dustSheet is not assigned.");
            if (sparkleSheet == null) Debug.LogError($"{nameof(WorldEffects)}.sparkleSheet is not assigned.");

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
        public void Puff(Vector3 center, int puffs, float size)
        {
            for (int i = 0; i < puffs; i++)
            {
                Vector2 direction = Random.insideUnitCircle;
                EmitDust(center + (Vector3)(direction * size * 0.3f), direction * (size * 3f), size * Random.Range(0.7f, 1.2f), Random.Range(0.35f, 0.6f), Mathf.Sign(direction.x));
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

        private void EmitDust(Vector3 position, Vector3 velocity, float size, float lifetime, float spinDirection)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = Color.white,
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
