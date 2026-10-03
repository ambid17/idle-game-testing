using UnityEngine;

namespace Effects
{
    // Easing curves shared by the code-driven animations (0..1 in, roughly 0..1 out).
    public static class Easing
    {
        public static float InQuad(float t) => t * t;

        public static float OutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float InOutSine(float t) => 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);

        // Overshoots past 1 before settling - the "pop".
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
