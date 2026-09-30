using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapGeneration
{
    // Slices a biome's back-wall texture (LayerConfig.BackgroundTexture) into one Tile per cell of
    // a single repeat, built once per texture at runtime and shared by every chunk of that biome.
    // Sliced here rather than in the texture importer so a new biome texture only needs dropping
    // onto a LayerConfig - no Sprite Editor grid slicing to author or keep in sync with
    // BackgroundRepeatCells.
    public static class BackgroundTileCache
    {
        private static readonly Dictionary<Texture2D, Tile[]> tilesByTexture = new();

        // Row-major from the texture's TOP row down, matching chunk rows (y grows downward).
        public static Tile[] Get(Texture2D texture, int repeatCells)
        {
            // Unity-null check too: runtime Tiles don't survive leaving Play Mode, the static does.
            if (tilesByTexture.TryGetValue(texture, out var cached) && cached.Length == repeatCells * repeatCells && cached[0] != null)
            {
                return cached;
            }

            float cellPixels = texture.width / (float)repeatCells;
            var tiles = new Tile[repeatCells * repeatCells];
            for (int row = 0; row < repeatCells; row++)
            {
                for (int col = 0; col < repeatCells; col++)
                {
                    // Texture rects are bottom-up; flip so row 0 is the top strip.
                    var rect = new Rect(col * cellPixels, texture.height - (row + 1) * cellPixels, cellPixels, cellPixels);
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), cellPixels, 0, SpriteMeshType.FullRect);
                    tile.colliderType = Tile.ColliderType.None;
                    tiles[row * repeatCells + col] = tile;
                }
            }

            tilesByTexture[texture] = tiles;
            return tiles;
        }
    }
}
