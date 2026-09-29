using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    // Put on anything that should show as a dot on the HUD minimap (player, buildings, automatons,
    // drones, chests). Registers itself while enabled, so spawned/despawned and hidden-until-revealed
    // objects appear and disappear on their own without MinimapUI tracking each system. Plain static
    // registry, same reasoning as Player.InputBlocker - simple polled state, no scene wiring.
    public class MinimapMarker : MonoBehaviour
    {
        private static readonly List<MinimapMarker> active = new();
        public static IReadOnlyList<MinimapMarker> Active => active;

        [SerializeField] private Color color = Color.white;
        [Tooltip("Dot diameter in minimap UI pixels.")]
        [SerializeField] private float size = 5f;
        [Tooltip("Pin to the minimap's edge when out of view instead of hiding - for landmarks like the Depot.")]
        [SerializeField] private bool clampToEdge;
        [Tooltip("Draw above every other marker (the player).")]
        [SerializeField] private bool drawOnTop;

        public Color Color => color;
        public float Size => size;
        public bool ClampToEdge => clampToEdge;
        public bool DrawOnTop => drawOnTop;

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
    }
}
