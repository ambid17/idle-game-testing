using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Atmosphere
{
    // Shows a title card (MilestoneBannerRequestedEvent) the first time the player goes deeper
    // into a new biome in a run - biomes are the spans of layers sharing a BiomeBackdrop, the
    // same grouping ParallaxBackdrop uses. Going back up and down again doesn't repeat it; a
    // prestige (new run) starts over. Wherever the player is when a save finishes loading counts
    // as already reached, so loading in deep never announces.
    public class BiomeEntryAnnouncer : MonoBehaviour
    {
        [SerializeField] private Transform player;

        // LayerConfig index -> biome order (0 = the surface biome), built once.
        private readonly List<int> biomeOfLayer = new();
        private readonly List<BiomeBackdrop> biomes = new();
        private bool loaded;
        private int deepestBiome = -1;

        private void Start()
        {
            if (player == null) Debug.LogError($"{nameof(BiomeEntryAnnouncer)}.player is not assigned.");

            foreach (var layer in GameManager.LayerConfigProvider.LayerConfigs)
            {
                if (layer.Backdrop != null && (biomes.Count == 0 || biomes[^1] != layer.Backdrop)) biomes.Add(layer.Backdrop);
                biomeOfLayer.Add(Mathf.Max(0, biomes.Count - 1));
            }
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        // Re-baselined on the next frame, once the player has been put where the save/reset left them.
        private void OnLoadCompleted()
        {
            loaded = true;
            deepestBiome = -1;
        }

        private void OnPrestigeCompleted(PrestigeCompletedEvent evt) => deepestBiome = -1;

        private void Update()
        {
            if (!loaded || biomes.Count == 0) return;

            int layerIndex = GameManager.LayerConfigProvider.GetLayerIndexAtWorldY(player.position.y, GameManager.MapGenerationService.CellSize);
            int biome = biomeOfLayer[Mathf.Clamp(layerIndex, 0, biomeOfLayer.Count - 1)];
            if (deepestBiome < 0)
            {
                deepestBiome = biome;
                return;
            }
            if (biome <= deepestBiome) return;

            deepestBiome = biome;
            var backdrop = biomes[biome];
            if (string.IsNullOrEmpty(backdrop.DisplayName)) return;
            GameManager.EventService.Dispatch(new MilestoneBannerRequestedEvent(backdrop.DisplayName, $"Layer {layerIndex + 1}", backdrop.TitleColor));
        }
    }
}
