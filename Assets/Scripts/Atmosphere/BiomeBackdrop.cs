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
    }

    // One biome's parallax backdrop (Atmosphere.ParallaxBackdrop), assigned on every LayerConfig
    // of that biome the same way GradingProfile is - consecutive layers sharing a BiomeBackdrop
    // form one continuous biome span.
    [CreateAssetMenu(fileName = "BiomeBackdrop", menuName = "Atmosphere/Biome Backdrop")]
    public class BiomeBackdrop : ScriptableObject
    {
        public List<BackdropPlane> Planes = new();
    }
}
