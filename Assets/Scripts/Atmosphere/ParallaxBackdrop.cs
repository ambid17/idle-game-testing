using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Atmosphere
{
    // The cavern seen through dug-out cells: each biome's BiomeBackdrop planes, placed at real
    // depth (z) behind the mine so the perspective camera gives true parallax for free - no
    // per-frame scrolling code. Each biome's planes span the world-Y range of that biome's layers
    // (read from LayerConfigProvider, so prestige layer-height changes just move the spans on the
    // next Build) and crossfade into the next biome's planes over fadeLength at the boundary.
    // Custom/SpriteParallaxBackdrop clips every plane to the mine's own rect (as seen from the
    // camera), so nothing leaks above the surface or past the grid's sides.
    public class ParallaxBackdrop : MonoBehaviour
    {
        // The mine's tilemaps sit at z = 0 (ChunkStreamingManager positions views on x/y only).
        private const float MineZ = 0f;
        // A fade edge far enough away that it never fades (float.MaxValue overflows in the shader).
        private const float NoFade = 1e7f;

        [Tooltip("Material using Custom/SpriteParallaxBackdrop - referenced (not Shader.Find'd) so builds include the shader.")]
        [SerializeField] private Material planeMaterial;
        [Tooltip("Camera-to-mine distance the planes are scaled for: at this distance every plane's tiles look the same size on screen as its sprite does at the mine's depth, whatever its own depth.")]
        [SerializeField] private float referenceCameraDistance = 10f;
        [Tooltip("World units (on each plane) over which one biome's plane fades into the next's.")]
        [SerializeField] private float fadeLength = 12f;
        [Tooltip("Extra world width (at mine depth) each plane covers beyond each side of the grid, so it still fills the view with the camera at the grid's edge or zoomed out.")]
        [SerializeField] private float horizontalMargin = 40f;
        [Tooltip("How far (at mine depth) the shallowest biome's planes extend above the surface - the view from above looks down through the surface at a steep parallax angle. Clipped at the surface anyway.")]
        [SerializeField] private float surfaceOverhang = 40f;
        [Tooltip("How far below its top the deepest biome's planes extend - it repeats forever past the last authored layer, like LayerConfigProvider does.")]
        [SerializeField] private float deepestBiomeExtent = 4000f;

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
                        // Parallax magnifies how far past the surface a deep plane is looked at.
                        float scale = 1f + plane.Depth / referenceCameraDistance;
                        float top = isFirst ? surfaceY + surfaceOverhang * scale : LayerTopY(spanStart);
                        float bottom = isLast ? LayerTopY(spanStart) - deepestBiomeExtent : LayerTopY(i);
                        BuildPlane(plane, top, bottom, !isFirst, !isLast, gridWorldWidth, mineRect, sortingOrders[plane.Depth]);
                    }
                }
                spanStart = i;
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
            float gridWorldWidth, Vector4 mineRect, int sortingOrder)
        {
            // Overlap the neighbouring biome by half a fade on each shared edge.
            if (fadeTop) top += fadeLength * 0.5f;
            if (fadeBottom) bottom -= fadeLength * 0.5f;

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

            ApplyClip(renderer, mineRect, fadeTop ? top : NoFade, fadeBottom ? bottom : -NoFade);
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

            ApplyClip(renderer, mineRect, NoFade, -NoFade);
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

        private void ApplyClip(SpriteRenderer renderer, Vector4 mineRect, float fadeTop, float fadeBottom)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetVector(MineRectId, mineRect);
            block.SetFloat(FadeTopId, fadeTop);
            block.SetFloat(FadeBottomId, fadeBottom);
            block.SetFloat(FadeLengthId, fadeLength);
            renderer.SetPropertyBlock(block);
        }
    }
}
