using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atmosphere
{
    [Serializable]
    public class BackdropPlane
    {
        [Tooltip("Seamless, tileable in both directions. Import as a FullRect sprite.")]
        public Sprite Sprite;
        [Tooltip("World units behind the mine (z). Farther = scrolls slower. Planes of different biomes at the same depth crossfade into each other at the biome boundary.")]
        [Min(0.1f)] public float Depth = 10f;
        [Tooltip("Tint (and dimming) - farther planes should be darker so they read as distant.")]
        public Color Tint = Color.white;
        [Tooltip("Horizontal bands the art is composed in (Tools/Backdrops/cave_layout.py), each with empty rows at its edges. When set, biome boundaries snap to the nearest band edge so the cut never slices a formation. 0 = opaque art cut on a jagged seam instead.")]
        [Min(0)] public int Bands;
    }

    // One biome's parallax backdrop (Atmosphere.ParallaxBackdrop), assigned on every LayerConfig
    // of that biome the same way GradingProfile is - consecutive layers sharing a BiomeBackdrop
    // form one continuous biome span.
    [CreateAssetMenu(fileName = "BiomeBackdrop", menuName = "Atmosphere/Biome Backdrop")]
    public class BiomeBackdrop : ScriptableObject
    {
        [Tooltip("The biome's name, shown on the title card when the player first reaches it in a run (Atmosphere.BiomeEntryAnnouncer).")]
        public string DisplayName;
        [Tooltip("Colour of that title.")]
        public Color TitleColor = Color.white;
        public List<BackdropPlane> Planes = new();
    }
}
