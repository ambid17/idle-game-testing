using UnityEngine;

namespace Atmosphere
{
    // Procedural soft sprites shared by every glow and ambient particle - generated once at runtime
    // so there's no texture asset to author or keep in sync. Both are white; callers tint them.
    public static class GlowSprites
    {
        private const int GlowResolution = 64;
        private const int DotResolution = 16;

        private static Sprite radialGlow;
        private static Sprite softDot;
        private static Material glowMaterial;
        private static Material particleMaterial;

        // 1 world unit across, smooth quadratic falloff to fully transparent at the edge.
        public static Sprite RadialGlow => radialGlow != null ? radialGlow : radialGlow = CreateRadial(GlowResolution, 2f, "RadialGlow");

        // 1 world unit across, tighter falloff - reads as a small solid speck when scaled down.
        public static Sprite SoftDot => softDot != null ? softDot : softDot = CreateRadial(DotResolution, 0.6f, "SoftDot");

        // Shared by every glow SpriteRenderer (the renderer supplies the sprite texture).
        public static Material GlowMaterial => glowMaterial != null ? glowMaterial : glowMaterial = new Material(ResolveShader()) { name = "AtmosphereGlow" };

        // Particle renderers don't get a texture from anywhere else, so this one carries SoftDot's.
        public static Material ParticleMaterial => particleMaterial != null
            ? particleMaterial
            : particleMaterial = new Material(ResolveShader()) { name = "AtmosphereParticles", mainTexture = SoftDot.texture };

        private static Shader ResolveShader()
        {
            var configured = GameManager.AtmosphereConfig.AdditiveShader;
            return configured != null ? configured : Shader.Find("Sprites/Default");
        }

        public static SpriteRenderer CreateGlow(Transform parent, Color color, float diameter)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RadialGlow;
            renderer.color = color;
            renderer.sharedMaterial = GlowMaterial;
            renderer.sortingOrder = GameManager.AtmosphereConfig.GlowSortingOrder;
            return renderer;
        }

        private static Sprite CreateRadial(int size, float falloffPower, string spriteName)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Pow(t, falloffPower) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
