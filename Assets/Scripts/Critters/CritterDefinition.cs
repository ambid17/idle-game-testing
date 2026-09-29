using UnityEngine;

namespace Critters
{
    // How a critter gets around its pocket (see Critter's per-style update methods). Flyers
    // (Hover/Flutter/Float) roam any open cell near home; ground movers (Crawl/Hop/Slither/Peek)
    // stick to a floor and turn around at walls and ledges.
    public enum CritterMovement
    {
        Hover,
        Flutter,
        Float,
        Crawl,
        Hop,
        Slither,
        Peek,
    }

    [CreateAssetMenu(fileName = "Critter", menuName = "Critters/Critter Definition")]
    public class CritterDefinition : ScriptableObject
    {
        public CritterId Id;
        public string DisplayName;
        [Tooltip("Collection card flavor text (CritterShopUI).")]
        [TextArea(2, 4)] public string Description;
        [Tooltip("Assumed to face right - Critter flips it to face its direction of travel.")]
        public Sprite Sprite;

        [Header("Behavior")]
        public CritterMovement Movement;
        [Tooltip("World units per second.")]
        [Min(0.05f)] public float Speed = 1f;
        [Tooltip("How far, in cells, it strays from the spot it spawned.")]
        [Min(0.5f)] public float WanderRadius = 2f;
        [Tooltip("World-space height the sprite is scaled to (a cell is 1 unit).")]
        [Range(0.2f, 1f)] public float Size = 0.55f;
        [Tooltip("Soft glow around the critter, drawn above fog so it hints at an unexplored pocket. Alpha 0 = no glow.")]
        public Color GlowColor = Color.clear;

        [Header("Critter Shop")]
        [Tooltip("Dollars paid per critter of this species turned in at the Critter Shop.")]
        [Min(0f)] public double TurnInValue = 250;
        [Tooltip("What the shopkeeper says the first time this species is turned in.")]
        [TextArea(2, 4)] public string ShopkeeperQuip;

        public bool IsFlyer => Movement is CritterMovement.Hover or CritterMovement.Flutter or CritterMovement.Float;
    }
}
