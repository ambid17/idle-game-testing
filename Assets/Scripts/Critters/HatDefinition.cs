using UnityEngine;

namespace Critters
{
    [CreateAssetMenu(fileName = "Hat", menuName = "Critters/Hat Definition")]
    public class HatDefinition : ScriptableObject
    {
        public HatId Id;
        public string DisplayName;
        [Tooltip("Pivot should be bottom-center so it sits on the automaton's head.")]
        public Sprite Sprite;
        [Tooltip("Unlocks once this many different critter species have been turned in at the Critter Shop.")]
        [Min(1)] public int UnlockAtSpeciesCount = 1;
        [Tooltip("World-space width the hat is scaled to.")]
        [Range(0.1f, 1.5f)] public float Width = 0.6f;
        [Tooltip("Nudge from the top-center of the automaton's sprite, in world units.")]
        public Vector2 Offset;
    }
}
