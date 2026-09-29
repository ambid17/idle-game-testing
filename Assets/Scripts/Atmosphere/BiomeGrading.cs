using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atmosphere
{
    // Per-biome colour grading on top of the base post-processing Volume on this GameObject.
    // Each LayerConfig names a GradingProfile (layers of one biome share one). At Start this builds
    // one child global Volume per distinct profile, then every frame eases each Volume's weight
    // toward 1 if it's the player's current layer's profile and 0 otherwise - so any sequence of
    // layer changes (including turning back mid-fade) crossfades smoothly.
    public class BiomeGrading : MonoBehaviour
    {
        // Above the base Volume (priority 0), so biome overrides win where both set a parameter.
        private const float BiomeVolumePriority = 1f;

        [SerializeField] private Transform player;

        private readonly List<Volume> biomeVolumes = new();
        private int currentLayerIndex = -1;
        private VolumeProfile targetProfile;

        private void Start()
        {
            if (player == null) Debug.LogError("BiomeGrading.player is not assigned.");

            foreach (var layer in GameManager.LayerConfigProvider.LayerConfigs)
            {
                var profile = layer.GradingProfile;
                if (profile == null || biomeVolumes.Exists(v => v.sharedProfile == profile)) continue;

                var go = new GameObject($"Grading ({profile.name})");
                go.transform.SetParent(transform, false);
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = BiomeVolumePriority;
                volume.sharedProfile = profile;
                volume.weight = 0f;
                biomeVolumes.Add(volume);
            }
        }

        private void Update()
        {
            int layerIndex = GameManager.LayerConfigProvider.GetLayerIndexAtWorldY(player.position.y, GameManager.MapGenerationService.CellSize);
            // The first frame snaps straight to the starting layer's grading instead of fading in on load.
            bool firstFrame = currentLayerIndex == -1;
            if (layerIndex != currentLayerIndex)
            {
                currentLayerIndex = layerIndex;
                targetProfile = GameManager.LayerConfigProvider.GetConfig(layerIndex).GradingProfile;
            }

            float step = firstFrame ? 1f : Time.deltaTime / GameManager.AtmosphereConfig.GradingCrossfadeSeconds;
            foreach (var volume in biomeVolumes)
            {
                float target = volume.sharedProfile == targetProfile ? 1f : 0f;
                volume.weight = Mathf.MoveTowards(volume.weight, target, step);
            }
        }
    }
}
