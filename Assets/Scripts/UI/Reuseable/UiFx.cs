using System;
using Atmosphere;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Small code-driven UI particle effects (sparkle bursts, glow flashes, a dot travelling a line)
    // for panels and the HUD - the screen-space counterpart of Effects.WorldEffects. Every particle
    // is a UiFxParticle Image parented under the given RectTransform and positioned by localPosition
    // (e.g. from parent.InverseTransformPoint), never a raycast target. Sparkles reuse
    // WorldEffects' pixel sparkle art, tinted.
    public static class UiFx
    {
        public static readonly Color Cyan = new(0.45f, 1f, 1f, 1f);
        public static readonly Color Gold = new(1f, 0.85f, 0.3f, 1f);

        // Sparkles flung outward from a ring of the given radius around localPosition.
        public static void SparkleBurst(RectTransform parent, Vector2 localPosition, int count, float radius, float speed, float size, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                var particle = Spawn(parent, GameManager.WorldEffects.SparkleSprite(UnityEngine.Random.Range(0, 8)), color, size * UnityEngine.Random.Range(0.6f, 1.1f));
                particle.transform.localPosition = localPosition + direction * radius;
                particle.Velocity = direction * (speed * UnityEngine.Random.Range(0.4f, 1f));
                particle.Drag = 3f;
                particle.Lifetime = UnityEngine.Random.Range(0.45f, 0.8f);
                particle.EndScale = 0.4f;
                particle.PopIn = 0.15f;
                particle.FadeStart = 0.5f;
                particle.Spin = UnityEngine.Random.Range(-180f, 180f);
            }
        }

        // A soft round glow that swells out and fades - the "flash" under a pop.
        public static void GlowFlash(RectTransform parent, Vector2 localPosition, float size, Color color, float seconds = 0.4f)
        {
            var particle = Spawn(parent, GlowSprites.RadialGlow, color, size);
            particle.transform.localPosition = localPosition;
            particle.Lifetime = seconds;
            particle.StartScale = 0.5f;
            particle.EndScale = 1.4f;
            particle.FadeStart = 0.15f;
        }

        // A glowing dot that travels from one local position to another, then calls onArrive.
        public static void Travel(RectTransform parent, Vector2 from, Vector2 to, float size, Color color, float seconds, Action onArrive)
        {
            var particle = Spawn(parent, GlowSprites.RadialGlow, color, size);
            particle.HasPath = true;
            particle.PathFrom = from;
            particle.PathTo = to;
            particle.transform.localPosition = from;
            particle.Lifetime = seconds;
            particle.FadeStart = 1f;
            particle.OnArrive = onArrive;
        }

        private static UiFxParticle Spawn(RectTransform parent, Sprite sprite, Color color, float size)
        {
            var go = new GameObject("UI Fx", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * size;
            rect.localScale = Vector3.zero;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;

            return go.AddComponent<UiFxParticle>();
        }
    }
}
