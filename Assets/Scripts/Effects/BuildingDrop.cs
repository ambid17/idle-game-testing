using System.Collections;
using UnityEngine;

namespace Effects
{
    // The "building plops onto the ground" motion shared by the building reveal and prestige
    // cinematics. Building sprites pivot at their centre and have transparent padding around the
    // art, so everything here works from the art's real extents (ArtBounds).
    public static class BuildingDrop
    {
        // Where the art actually ends, from the sprite's tight mesh. Pivot-relative, in world units.
        public static Bounds ArtBounds(Sprite sprite, Vector3 scale)
        {
            Vector2 min = sprite.vertices[0];
            Vector2 max = min;
            foreach (Vector2 vertex in sprite.vertices)
            {
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(Vector2.Scale(min, scale), Vector2.Scale(max, scale));
            return bounds;
        }

        // Falls from one point to its resting place, stretching as it picks up speed.
        public static IEnumerator Fall(Transform building, Vector3 from, Vector3 restPosition, Vector3 restScale, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float stretch = 1f + 0.12f * k;
                building.position = Vector3.LerpUnclamped(from, restPosition, k * k);
                building.localScale = new Vector3(restScale.x / stretch, restScale.y * stretch, restScale.z);
                yield return null;
            }
            building.position = restPosition;
        }

        // Squashes flat on impact and springs back, keeping the base of the art planted.
        // squash: how much it flattens at the moment of impact (0.25 = 25% shorter, and wider to match).
        public static IEnumerator Settle(Transform building, Vector3 restPosition, Vector3 restScale, float artBottomOffset, float squash, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float amount = squash * Mathf.Exp(-5f * k) * Mathf.Cos(k * Mathf.PI * 4f);
                float vertical = 1f - amount;
                building.localScale = new Vector3(restScale.x * (1f + amount * 0.6f), restScale.y * vertical, restScale.z);
                building.position = restPosition + Vector3.up * (artBottomOffset * (1f - vertical));
                yield return null;
            }
            building.position = restPosition;
            building.localScale = restScale;
        }
    }
}
