using System;
using System.Collections;
using Atmosphere;
using UnityEngine;

namespace Effects
{
    // The big spinning sky portal of the building reveal and prestige cinematics: the same art as
    // Player.PlayerPortalTravel's, with a glow behind it and sparkles spiralling into its centre
    // for as long as it is open. Built in code by whichever cinematic owns it (see Create).
    public class CinematicPortal : MonoBehaviour
    {
        private const float SparkleLifetime = 0.6f;

        private SpriteRenderer spriteRenderer;
        private float diameter;
        private float spinSpeed;
        private float sparkleRate;

        public SpriteRenderer Renderer => spriteRenderer;
        public float Diameter => diameter;

        // diameter: world units at full size. spinSpeed: degrees per second, negative = clockwise.
        // sparkleRate: sparkles per second drawn into the portal while it is open.
        public static CinematicPortal Create(Transform parent, string portalName, Sprite sprite, float diameter, float spinSpeed, Color glowColor, float sparkleRate)
        {
            var go = new GameObject(portalName);
            go.transform.SetParent(parent, false);
            var portal = go.AddComponent<CinematicPortal>();
            portal.spriteRenderer = go.AddComponent<SpriteRenderer>();
            portal.spriteRenderer.sprite = sprite;
            portal.diameter = diameter;
            portal.spinSpeed = spinSpeed;
            portal.sparkleRate = sparkleRate;
            // Glow diameter is in the portal's local units, where the sprite itself is bounds.size across.
            GlowSprites.CreateGlow(go.transform, glowColor, sprite.bounds.size.x * 1.5f);
            go.SetActive(false);
            return portal;
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            EmitRimSparkles(Time.deltaTime);
        }

        // Scales the portal between fractions of its full size (0 = closed, which also hides it).
        public IEnumerator Animate(Vector3 position, float fromSize, float toSize, float seconds, Func<float, float> ease)
        {
            transform.position = position;
            gameObject.SetActive(true);

            // Sprite bounds are in unscaled world units, so this maps "1" to diameter.
            float unitScale = diameter / spriteRenderer.sprite.bounds.size.x;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                transform.localScale = Vector3.one * (unitScale * Mathf.Max(0f, Mathf.LerpUnclamped(fromSize, toSize, ease(t / seconds))));
                yield return null;
            }
            transform.localScale = Vector3.one * (unitScale * toSize);
            if (toSize <= 0f) gameObject.SetActive(false);
        }

        // Motes that appear around the rim and spiral into the centre.
        private void EmitRimSparkles(float deltaTime)
        {
            float expected = sparkleRate * deltaTime;
            int count = Mathf.FloorToInt(expected) + (UnityEngine.Random.value < expected % 1f ? 1 : 0);
            // Follows the portal's current size, so nothing spawns outside it while it opens/closes.
            // (Not renderer.bounds - that grows and shrinks as the sprite spins.)
            float rim = spriteRenderer.sprite.bounds.extents.x * transform.localScale.x;
            for (int i = 0; i < count; i++)
            {
                float radius = rim * UnityEngine.Random.Range(0.75f, 1.05f);
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                var tangent = new Vector2(direction.y, -direction.x);
                Vector3 velocity = (-direction + tangent * 0.8f) * (radius / SparkleLifetime);
                GameManager.WorldEffects.Sparkle(transform.position + (Vector3)(direction * radius), velocity, UnityEngine.Random.Range(0.18f, 0.4f), SparkleLifetime);
            }
        }
    }
}
