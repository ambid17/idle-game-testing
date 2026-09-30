using UnityEngine;

namespace Atmosphere
{
    // Tuning for the mine's ambient life: ore glow, dust/drip/spore particles and ambience
    // crossfades. Per-layer colours, rates and loops live on each MapGeneration.LayerConfig.
    // Registered on GameManager (GameManager.AtmosphereConfig).
    [CreateAssetMenu(fileName = "AtmosphereConfig", menuName = "Atmosphere/Atmosphere Config")]
    public class AtmosphereConfig : ScriptableObject
    {
        [Header("Glow (ore veins, glowing critters, the Critter Shop lantern) and ambient particles")]
        [Tooltip("Custom/SpriteAdditiveGlow - referenced here (not Shader.Find'd) so builds include it. Null falls back to the default sprite shader (alpha-blended, softer).")]
        public Shader AdditiveShader;
        [Tooltip("Must sort above the fog tilemap (order 1) so glows show faintly through unexplored ground.")]
        public int GlowSortingOrder = 3;

        [Header("Ore glow")]
        [Tooltip("Ores worth at least as much as this layer's Nth most valuable ore glint - the top of each layer's table, plus anything rarer swapped in from deeper.")]
        [Min(1)] public int GlowingOresPerLayer = 2;
        [Tooltip("...and also worth at least this multiple of the layer's weighted-average ore value, so layers of only common ores (the surface) don't glint at all.")]
        [Min(1f)] public float OreGlowValueMultiplier = 1.75f;
        [Tooltip("A vein only glints through the fog once one of its hidden cells is within this many cells of revealed ground - full strength when adjacent, fading out at this distance.")]
        [Min(1)] public int OreGlowHintRadius = 7;
        [Tooltip("Visibility change per second as a vein's glint fades in/out.")]
        [Min(0.01f)] public float OreGlowFadeSpeed = 2f;

        [Header("Ore glint")]
        [Tooltip("Custom/OreShine - the diagonal shine line swept across a vein's hidden cells.")]
        public Shader OreShineShader;
        [Tooltip("Half-width of the shine line's bright core, in cells (a faint halo spreads 3x wider).")]
        [Min(0.01f)] public float OreGlintLineWidth = 0.08f;
        [Range(0f, 1f)] public float OreGlintAlpha = 0.9f;
        [Tooltip("How fast the line travels from the vein's bottom-left to its top-right, in cells per second.")]
        [Min(0.1f)] public float OreGlintSpeed = 6f;
        [Tooltip("Seconds between a vein's glints (random in range).")]
        [Min(0f)] public float OreGlintIntervalMin = 2.5f;
        [Min(0f)] public float OreGlintIntervalMax = 6f;

        [Header("Particles")]
        [Tooltip("Cells around the player (radius) that drips and spores are sampled from.")]
        [Min(1)] public int AmbientRadiusCells = 9;
        [Tooltip("How long a freshly dug cell keeps shedding dust from the ceiling above it.")]
        [Min(0f)] public float DustSecondsAfterDig = 6f;
        [Tooltip("Dust specks per second from each freshly dug cell, fading out over DustSecondsAfterDig.")]
        [Min(0f)] public float DustPerSecondPerCell = 3f;
        [Tooltip("Base dust colour - tinted toward the layer's AmbientParticleColor.")]
        public Color DustColor = new(0.75f, 0.62f, 0.48f, 0.8f);
        public Color DripColor = new(0.6f, 0.85f, 1f, 0.9f);

        [Header("Ambience")]
        [Range(0f, 1f)] public float AmbienceVolume = 0.35f;
        [Min(0f)] public float AmbienceCrossfadeSeconds = 2.5f;

        [Header("Colour grading")]
        [Tooltip("How long BiomeGrading takes to fade between layers' GradingProfiles.")]
        [Min(0.01f)] public float GradingCrossfadeSeconds = 2.5f;

        public void Validate()
        {
            if (GlowSortingOrder <= 1) Debug.LogError("AtmosphereConfig.GlowSortingOrder must be above the fog tilemap's sorting order (1).");
            if (AdditiveShader == null) Debug.LogError("AtmosphereConfig.AdditiveShader is not assigned - glows fall back to the default sprite shader.");
            if (OreShineShader == null) Debug.LogError("AtmosphereConfig.OreShineShader is not assigned - ore veins won't glint.");
        }
    }
}
