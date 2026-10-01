using System;
using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Atmosphere
{
    // The cavern seen through dug-out cells: each biome's BiomeBackdrop planes, placed at real
    // depth (z) behind the mine so the perspective camera gives true parallax for free - no
    // per-frame scrolling code. Each biome's planes span the world-Y range of that biome's layers
    // (read from LayerConfigProvider, so prestige layer-height changes just move the spans on the
    // next Build). Custom/SpriteParallaxBackdrop clips every plane to the mine's own rect (as
    // seen from the camera), so nothing leaks above the surface or past the grid's sides.
    //
    // Biome boundaries are hard cuts, never crossfades (two half-faded caves on top of each other
    // read as a double exposure):
    // - Banded planes (the mid/near formation layers, BackdropPlane.Bands > 0) cut on the band
    //   edge nearest the boundary. Their art keeps those rows empty, so whole formations sit on
    //   one side or the other.
    // - Opaque planes (the far walls) meet on a jagged, pixel-stepped seam with a dark crevice
    //   along it - both sides compute the same seam, so they interlock exactly.
    //
    // The sky above the surface works the same way with its own stack of SkyBands (low sky,
    // clouds, space...), clipped the other way round - only above the surface, at any x. Each
    // band's farthest plane stays opaque under the band above's fade-in, so the camera's skybox
    // never shows through a transition.
    public class ParallaxBackdrop : MonoBehaviour
    {
        [Serializable]
        public class SkyBand
        {
            public BiomeBackdrop Backdrop;
            [Tooltip("World units this band covers above the one below it. Ignored on the last band, which extends forever (skyTopExtent).")]
            [Min(1f)] public float Height = 100f;
        }

        // The mine's tilemaps sit at z = 0 (ChunkStreamingManager positions views on x/y only).
        private const float MineZ = 0f;
        // A fade edge far enough away that it never fades (float.MaxValue overflows in the shader).
        private const float NoFade = 1e7f;

        [Tooltip("Material using Custom/SpriteParallaxBackdrop - referenced (not Shader.Find'd) so builds include the shader.")]
        [SerializeField] private Material planeMaterial;
        [Tooltip("Camera-to-mine distance the planes are scaled for: at this distance every plane's tiles look the same size on screen as its sprite does at the mine's depth, whatever its own depth.")]
        [SerializeField] private float referenceCameraDistance = 10f;
        [Tooltip("Extra world width (at mine depth) each plane covers beyond each side of the grid, so it still fills the view with the camera at the grid's edge or zoomed out.")]
        [SerializeField] private float horizontalMargin = 40f;
        [Tooltip("How far (at mine depth) the shallowest biome's planes extend above the surface - the view from above looks down through the surface at a steep parallax angle. Clipped at the surface anyway.")]
        [SerializeField] private float surfaceOverhang = 40f;
        [Tooltip("How far below its top the deepest biome's planes extend - it repeats forever past the last authored layer, like LayerConfigProvider does.")]
        [SerializeField] private float deepestBiomeExtent = 4000f;

        [Header("Biome seams (opaque planes)")]
        [Tooltip("How far (at mine depth) the jagged seam between two biomes' opaque planes wanders above and below the boundary.")]
        [SerializeField] private float seamJag = 1.5f;
        [Tooltip("Step size (at mine depth) of the seam's jags, so it reads as pixel art.")]
        [SerializeField] private float seamPixel = 0.16f;
        [Tooltip("Brightness right at the seam - the crevice where two biomes' rock meets.")]
        [Range(0f, 1f)] [SerializeField] private float seamShade = 0.3f;
        [Tooltip("How far (at mine depth) from the seam the crevice darkening fades out.")]
        [SerializeField] private float seamShadeLength = 1.5f;

        [Header("Sky (above the surface)")]
        [Tooltip("Bands stacked upward from the surface; the last one repeats forever.")]
        [SerializeField] private List<SkyBand> skyBands = new();
        [Tooltip("World units (on each plane) over which one sky band fades into the next.")]
        [SerializeField] private float skyFadeLength = 40f;
        [Tooltip("How far the sky extends past the surface in both directions: up for the last band, and down under the first so a camera high above the surface still sees sky when looking down through it at a steep parallax angle. Clipped at the surface anyway.")]
        [SerializeField] private float skyExtent = 4000f;

        [Header("Backing (behind every plane, so crossfades and transparent planes never show the sky)")]
        [SerializeField] private Color backingColor = new(0.02f, 0.015f, 0.03f, 1f);
        [SerializeField] private float backingDepth = 80f;
        [Tooltip("Sorting order of the backing; planes sort above it, farthest first, and all stay below the mine's tilemaps (order -1 and up).")]
        [SerializeField] private int backingSortingOrder = -100;

        private readonly List<GameObject> spawned = new();
        private Sprite backingSprite;

        private static readonly int MineRectId = Shader.PropertyToID("_MineRect");
        private static readonly int FadeTopId = Shader.PropertyToID("_FadeTop");
        private static readonly int FadeBottomId = Shader.PropertyToID("_FadeBottom");
        private static readonly int FadeLengthId = Shader.PropertyToID("_FadeLength");
        private static readonly int SkyClipId = Shader.PropertyToID("_SkyClip");
        private static readonly int EdgeModeId = Shader.PropertyToID("_EdgeMode");
        private static readonly int EdgeJagId = Shader.PropertyToID("_EdgeJag");
        private static readonly int EdgePixelId = Shader.PropertyToID("_EdgePixel");
        private static readonly int EdgeShadeId = Shader.PropertyToID("_EdgeShade");
        private static readonly int EdgeShadeLengthId = Shader.PropertyToID("_EdgeShadeLength");
        private static readonly int TileRectId = Shader.PropertyToID("_TileRect");

        private void OnEnable()
        {
            GameManager.EventService.Add<GridWidthChangedEvent>(OnGridWidthChanged);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<GridWidthChangedEvent>(OnGridWidthChanged);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void Start()
        {
            if (planeMaterial == null) Debug.LogError($"{nameof(ParallaxBackdrop)}.planeMaterial is not assigned.");
            Build();
        }

        // Grid width moves the side clip and plane widths; prestige (layer-height perk) and save
        // restores move the biome spans.
        private void OnGridWidthChanged(GridWidthChangedEvent evt) => Build();
        private void OnPrestigeCompleted(PrestigeCompletedEvent evt) => Build();

        private void Build()
        {
            foreach (var go in spawned) Destroy(go);
            spawned.Clear();

            var provider = GameManager.LayerConfigProvider;
            float cellSize = GameManager.MapGenerationService.CellSize;
            float gridWorldWidth = GameManager.MapGenerationService.World.GridWidth * cellSize;
            float surfaceY = LayerTopY(0);
            var mineRect = new Vector4(0f, gridWorldWidth, surfaceY, MineZ);

            var sortingOrders = BuildSortingOrders(provider.LayerConfigs);
            BuildBacking(gridWorldWidth, surfaceY, mineRect);

            // Walk every authored layer, grouping runs of consecutive layers that share a backdrop.
            int deepestLayer = 0;
            foreach (var layer in provider.LayerConfigs) deepestLayer = Mathf.Max(deepestLayer, layer.LayerIndex);

            int spanStart = 0;
            for (int i = 1; i <= deepestLayer + 1; i++)
            {
                var spanBackdrop = provider.GetConfig(spanStart).Backdrop;
                bool spanEnds = i > deepestLayer || provider.GetConfig(i).Backdrop != spanBackdrop;
                if (!spanEnds) continue;

                if (spanBackdrop != null)
                {
                    bool isFirst = spanStart == 0;
                    bool isLast = i > deepestLayer;
                    foreach (var plane in spanBackdrop.Planes)
                    {
                        float top = isFirst ? float.PositiveInfinity : LayerTopY(spanStart);
                        float bottom = isLast ? float.NegativeInfinity : LayerTopY(i);
                        BuildMinePlane(plane, top, bottom, LayerTopY(spanStart), surfaceY, gridWorldWidth, mineRect, sortingOrders[plane.Depth]);
                    }
                }
                spanStart = i;
            }

            BuildSky(surfaceY, gridWorldWidth, mineRect);
        }

        private void BuildSky(float surfaceY, float gridWorldWidth, Vector4 mineRect)
        {
            // Farthest first; at the same depth the higher band draws on top, so it can fade in
            // over the lower band's still-opaque plane. Sky and mine planes never overlap on screen
            // (opposite clips), so their orders don't need to be distinct.
            var skyPlanes = new List<(int band, BackdropPlane plane)>();
            for (int b = 0; b < skyBands.Count; b++)
            {
                if (skyBands[b].Backdrop == null)
                {
                    Debug.LogError($"{nameof(ParallaxBackdrop)} sky band {b} has no Backdrop.");
                    continue;
                }
                foreach (var plane in skyBands[b].Backdrop.Planes) skyPlanes.Add((b, plane));
            }
            skyPlanes.Sort((x, y) => x.plane.Depth != y.plane.Depth ? y.plane.Depth.CompareTo(x.plane.Depth) : x.band.CompareTo(y.band));

            float bandBottom = surfaceY;
            var bandBottoms = new float[skyBands.Count];
            for (int b = 0; b < skyBands.Count; b++)
            {
                bandBottoms[b] = bandBottom;
                bandBottom += skyBands[b].Height;
            }

            for (int i = 0; i < skyPlanes.Count; i++)
            {
                var (b, plane) = skyPlanes[i];
                bool isFirst = b == 0;
                bool isLast = b == skyBands.Count - 1;
                float bottom = isFirst ? surfaceY - skyExtent : bandBottoms[b];
                float top = isLast ? bandBottoms[b] + skyExtent : bandBottoms[b] + skyBands[b].Height;

                // The band's farthest (opaque) plane runs on under the next band's fade-in instead
                // of fading out itself; nearer (transparent) planes crossfade as usual.
                bool isFarthest = true;
                foreach (var other in skyBands[b].Backdrop.Planes) isFarthest &= other.Depth <= plane.Depth;
                bool fadeTop = !isLast && !isFarthest;
                if (!isLast && isFarthest) top += skyFadeLength * 0.5f;

                BuildPlane(plane, top, bottom, fadeTop, !isFirst, skyFadeLength, true, gridWorldWidth, mineRect, backingSortingOrder + 1 + i);
            }
        }

        private static float LayerTopY(int layerIndex)
        {
            // Row 0 of a chunk view is its cell (x, 0), spanning local y 0..1.
            float cellSize = GameManager.MapGenerationService.CellSize;
            return (-GameManager.LayerConfigProvider.GetLayerOffset(layerIndex) + 1) * cellSize;
        }

        // Farthest planes draw first. Planes of different biomes at the same depth share an order -
        // they only overlap inside a crossfade, where draw order doesn't matter.
        private Dictionary<float, int> BuildSortingOrders(List<MapGeneration.LayerConfig> layers)
        {
            var depths = new List<float>();
            foreach (var layer in layers)
            {
                if (layer.Backdrop == null) continue;
                foreach (var plane in layer.Backdrop.Planes)
                {
                    if (!depths.Contains(plane.Depth)) depths.Add(plane.Depth);
                }
            }
            depths.Sort((a, b) => b.CompareTo(a));

            var orders = new Dictionary<float, int>();
            for (int i = 0; i < depths.Count; i++) orders[depths[i]] = backingSortingOrder + 1 + i;
            return orders;
        }

        private void BuildPlane(BackdropPlane plane, float top, float bottom, bool fadeTop, bool fadeBottom,
            float fade, bool sky, float gridWorldWidth, Vector4 mineRect, int sortingOrder)
        {
            // Overlap the neighbouring biome by half a fade on each shared edge.
            if (fadeTop) top += fade * 0.5f;
            if (fadeBottom) bottom -= fade * 0.5f;

            var renderer = CreateRenderer($"{plane.Sprite.name} (z {plane.Depth})", plane.Sprite, plane.Tint, sortingOrder);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;

            // Scaling by (distance + depth) / distance keeps tiles the same apparent size on screen
            // as they'd be at the mine's depth.
            float scale = 1f + plane.Depth / referenceCameraDistance;
            float width = (gridWorldWidth + horizontalMargin * 2f) * scale;
            renderer.transform.localScale = Vector3.one * scale;
            renderer.transform.position = new Vector3(gridWorldWidth * 0.5f, (top + bottom) * 0.5f, MineZ + plane.Depth);
            renderer.size = new Vector2(width, top - bottom) / scale;

            ApplyClip(renderer, mineRect, fadeTop ? top : NoFade, fadeBottom ? bottom : -NoFade, fade, sky);
        }

        // top / bottom: the biome boundaries, or +-infinity where the plane runs on past the
        // surface or below the last authored layer.
        private void BuildMinePlane(BackdropPlane plane, float top, float bottom, float spanTop, float surfaceY,
            float gridWorldWidth, Vector4 mineRect, int sortingOrder)
        {
            float scale = 1f + plane.Depth / referenceCameraDistance;
            Vector2 tileSize = plane.Sprite.bounds.size * scale;
            bool banded = plane.Bands > 0;
            float bandHeight = tileSize.y / Mathf.Max(1, plane.Bands);
            float jag = banded ? 0f : seamJag * scale;

            // Tiles repeat from world y = 0 (_TileRect), so band edges sit at multiples of bandHeight.
            float Cut(float y) => banded ? Mathf.Round(y / bandHeight) * bandHeight : y;
            bool hasTop = !float.IsInfinity(top);
            bool hasBottom = !float.IsInfinity(bottom);
            float cutTop = hasTop ? Cut(top) : NoFade;
            float cutBottom = hasBottom ? Cut(bottom) : -NoFade;

            // Parallax magnifies how far past the surface a deep plane is looked at.
            float quadTop = hasTop ? cutTop + jag + 1f : surfaceY + surfaceOverhang * scale;
            float quadBottom = hasBottom ? cutBottom - jag - 1f : spanTop - deepestBiomeExtent;

            var renderer = CreateRenderer($"{plane.Sprite.name} (z {plane.Depth})", plane.Sprite, plane.Tint, sortingOrder);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            float width = (gridWorldWidth + horizontalMargin * 2f) * scale;
            renderer.transform.localScale = Vector3.one * scale;
            renderer.transform.position = new Vector3(gridWorldWidth * 0.5f, (quadTop + quadBottom) * 0.5f, MineZ + plane.Depth);
            renderer.size = new Vector2(width, quadTop - quadBottom) / scale;

            var block = ClipBlock(renderer, mineRect, cutTop, cutBottom, 1f, false);
            block.SetFloat(EdgeModeId, 1f);
            block.SetFloat(EdgeJagId, jag);
            block.SetFloat(EdgePixelId, seamPixel * scale);
            block.SetFloat(EdgeShadeId, banded ? 1f : seamShade);
            block.SetFloat(EdgeShadeLengthId, seamShadeLength * scale);
            block.SetVector(TileRectId, new Vector4(gridWorldWidth * 0.5f, 0f, tileSize.x, tileSize.y));
            renderer.SetPropertyBlock(block);
        }

        private void BuildBacking(float gridWorldWidth, float surfaceY, Vector4 mineRect)
        {
            if (backingSprite == null)
            {
                var white = Texture2D.whiteTexture;
                backingSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            }

            var renderer = CreateRenderer("Backing", backingSprite, backingColor, backingSortingOrder);
            float scale = 1f + backingDepth / referenceCameraDistance;
            float top = surfaceY + surfaceOverhang * scale;
            float bottom = surfaceY - deepestBiomeExtent * scale;
            renderer.transform.position = new Vector3(gridWorldWidth * 0.5f, (top + bottom) * 0.5f, MineZ + backingDepth);
            renderer.transform.localScale = new Vector3((gridWorldWidth + horizontalMargin * 2f) * scale, top - bottom, 1f);

            ApplyClip(renderer, mineRect, NoFade, -NoFade, 1f, false);
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            spawned.Add(go);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = planeMaterial;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplyClip(SpriteRenderer renderer, Vector4 mineRect, float fadeTop, float fadeBottom, float fade, bool sky)
        {
            renderer.SetPropertyBlock(ClipBlock(renderer, mineRect, fadeTop, fadeBottom, fade, sky));
        }

        private static MaterialPropertyBlock ClipBlock(SpriteRenderer renderer, Vector4 mineRect, float fadeTop, float fadeBottom, float fade, bool sky)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetVector(MineRectId, mineRect);
            block.SetFloat(FadeTopId, fadeTop);
            block.SetFloat(FadeBottomId, fadeBottom);
            block.SetFloat(FadeLengthId, fade);
            block.SetFloat(SkyClipId, sky ? 1f : 0f);
            return block;
        }
    }
}
