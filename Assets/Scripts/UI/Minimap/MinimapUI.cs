using System.Collections.Generic;
using Economy;
using MapGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // HUD minimap unlocked by the Market's Mining_Minimap upgrade. The terrain is drawn straight from
    // MineWorld's cell data at one texel per cell - BlockType.MinimapColor for revealed blocks,
    // tunnel/fog/sky colors otherwise - so it respects fog of war and never renders the scene twice.
    // Artifact cells pulse so they stand out. The view is centered on the player and scrolls
    // smoothly: the texture carries a one-cell margin and the RawImage's uvRect slides by the
    // player's sub-cell offset. MinimapMarker dots are pooled Images laid over the terrain.
    //
    // Only reads already-loaded chunks (MineWorld.TryGetLoadedChunk) - unloaded layers draw as fog
    // rather than being generated just because the minimap looked at them.
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private GameObject renderer;
        [SerializeField] private Transform player;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform markerContainer;
        [SerializeField] private Sprite markerSprite;

        [Header("View")]
        [Tooltip("Cells visible across the minimap at upgrade level 1; later levels add UpgradeManager.Mining_MinimapRangeBonus.")]
        [SerializeField] private int baseViewWidthCells = 20;
        [Tooltip("Top rows drawn even when unrevealed - matches ChunkTilemapView.surfaceFogGradientRows, where the surface row has no fog in-game.")]
        [SerializeField] private int surfaceVisibleRows = 1;

        [Header("Colors")]
        [SerializeField] private Color skyColor = new(0.09f, 0.13f, 0.22f, 1f);
        [SerializeField] private Color fogColor = new(0.02f, 0.02f, 0.03f, 1f);
        [SerializeField] private Color tunnelColor = new(0.13f, 0.1f, 0.09f, 1f);
        [SerializeField] private Color outOfBoundsColor = new(0.06f, 0.06f, 0.06f, 1f);
        [SerializeField] private Color artifactPulseColor = Color.white;
        [SerializeField] private float artifactPulseSpeed = 4f;

        private readonly Color32[] blockColors = new Color32[256];
        private readonly List<Image> markerPool = new();
        private Texture2D texture;
        private Color32[] pixels;
        private int viewWidth;
        private int viewHeight;

        private MapGenerationService mapGen => GameManager.MapGenerationService;
        private LayerConfigProvider layers => GameManager.LayerConfigProvider;

        private void Start()
        {
            if (renderer == null) Debug.LogError($"{nameof(MinimapUI)} is missing renderer.");
            if (player == null) Debug.LogError($"{nameof(MinimapUI)} is missing player.");
            if (mapImage == null) Debug.LogError($"{nameof(MinimapUI)} is missing mapImage.");
            if (markerContainer == null) Debug.LogError($"{nameof(MinimapUI)} is missing markerContainer.");

            foreach (var blockType in GameManager.BlockTypeDatabase.BlockTypes)
            {
                if (blockType != null) blockColors[(byte)blockType.Id] = blockType.MinimapColor;
            }
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }

        private void LateUpdate()
        {
            bool unlocked = UpgradeManager.Instance.Mining_MinimapUnlocked;
            if (renderer.activeSelf != unlocked) renderer.SetActive(unlocked);
            if (!unlocked || mapGen.World == null) return;

            EnsureTexture();

            float cellSize = mapGen.CellSize;
            // Continuous cell-space position: column, and depth-in-blocks matching
            // LayerConfigProvider.GetDepthInBlocksAtWorldY (depth d spans [d, d+1)).
            float playerColumn = player.position.x / cellSize;
            float playerDepth = -player.position.y / cellSize + 1f;

            // Texture covers the view plus a one-cell margin on every side for smooth scrolling.
            int leftColumn = Mathf.FloorToInt(playerColumn) - viewWidth / 2 - 1;
            int topDepth = Mathf.FloorToInt(playerDepth) - viewHeight / 2 - 1;
            DrawTerrain(leftColumn, topDepth);

            int textureWidth = viewWidth + 2;
            int textureHeight = viewHeight + 2;
            float u = (playerColumn - viewWidth * 0.5f - leftColumn) / textureWidth;
            float vTop = 1f - (playerDepth - viewHeight * 0.5f - topDepth) / textureHeight;
            mapImage.uvRect = new Rect(u, vTop - (float)viewHeight / textureHeight, (float)viewWidth / textureWidth, (float)viewHeight / textureHeight);

            DrawMarkers(cellSize);
        }

        // Width comes from the upgrade; height follows the map rect's aspect ratio.
        private void EnsureTexture()
        {
            var rect = mapImage.rectTransform.rect;
            int width = baseViewWidthCells + UpgradeManager.Instance.Mining_MinimapRangeBonus;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * rect.height / rect.width));
            if (texture != null && width == viewWidth && height == viewHeight) return;

            viewWidth = width;
            viewHeight = height;
            if (texture != null) Destroy(texture);
            texture = new Texture2D(viewWidth + 2, viewHeight + 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[texture.width * texture.height];
            mapImage.texture = texture;
        }

        private void DrawTerrain(int leftColumn, int topDepth)
        {
            int width = texture.width;
            int height = texture.height;
            int gridWidth = mapGen.World.GridWidth;
            Color32 sky = skyColor, fog = fogColor, tunnel = tunnelColor, outOfBounds = outOfBoundsColor;
            Color32 artifact = Color.Lerp(blockColors[(byte)BlockTypeId.Artifact], artifactPulseColor, 0.5f + 0.5f * Mathf.Sin(Time.time * artifactPulseSpeed));

            for (int row = 0; row < height; row++)
            {
                int depth = topDepth + row;
                // Texture rows run bottom-up, depth runs top-down.
                int rowStart = (height - 1 - row) * width;

                ChunkData chunk = null;
                int y = 0;
                if (depth >= 0)
                {
                    int layerIndex = layers.GetLayerIndexAtDepth(depth);
                    y = depth - layers.GetLayerOffset(layerIndex);
                    if (!mapGen.World.TryGetLoadedChunk(layerIndex, out chunk) || y >= chunk.Height) chunk = null;
                }

                for (int column = 0; column < width; column++)
                {
                    int x = leftColumn + column;
                    Color32 color;
                    if (x < 0 || x >= gridWidth) color = outOfBounds;
                    else if (depth < 0) color = sky;
                    else if (chunk == null || x >= chunk.Width) color = fog;
                    else
                    {
                        var cell = chunk.Cells[chunk.Index(x, y)];
                        if (cell.Mined) color = tunnel;
                        else if (!cell.Revealed && depth >= surfaceVisibleRows) color = fog;
                        else if (cell.BlockTypeId == (byte)BlockTypeId.Artifact) color = artifact;
                        else color = blockColors[cell.BlockTypeId];
                    }
                    pixels[rowStart + column] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private void DrawMarkers(float cellSize)
        {
            var mapRect = markerContainer.rect;
            float pixelsPerCell = mapRect.width / viewWidth;
            Vector2 halfExtents = mapRect.size * 0.5f;
            Vector3 center = player.position;

            var markers = MinimapMarker.Active;
            int shown = 0;
            Image topMarker = null;
            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                Vector2 offset = (Vector2)(marker.transform.position - center) / cellSize * pixelsPerCell;
                float radius = marker.Size * 0.5f;
                Vector2 limit = halfExtents - new Vector2(radius, radius);
                bool outside = Mathf.Abs(offset.x) > limit.x || Mathf.Abs(offset.y) > limit.y;
                if (outside && !marker.ClampToEdge) continue;
                if (outside) offset = new Vector2(Mathf.Clamp(offset.x, -limit.x, limit.x), Mathf.Clamp(offset.y, -limit.y, limit.y));

                var image = GetPooledMarker(shown++);
                image.color = marker.Color;
                image.rectTransform.sizeDelta = new Vector2(marker.Size, marker.Size);
                image.rectTransform.anchoredPosition = offset;
                if (marker.DrawOnTop) topMarker = image;
            }

            for (int i = shown; i < markerPool.Count; i++)
            {
                if (markerPool[i].gameObject.activeSelf) markerPool[i].gameObject.SetActive(false);
            }

            if (topMarker != null) topMarker.transform.SetAsLastSibling();
        }

        private Image GetPooledMarker(int index)
        {
            if (index < markerPool.Count)
            {
                var pooled = markerPool[index];
                if (!pooled.gameObject.activeSelf) pooled.gameObject.SetActive(true);
                return pooled;
            }

            var go = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(markerContainer, false);
            var image = go.GetComponent<Image>();
            image.sprite = markerSprite;
            image.raycastTarget = false;
            markerPool.Add(image);
            return image;
        }
    }
}
