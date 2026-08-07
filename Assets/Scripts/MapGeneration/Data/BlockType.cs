using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapGeneration
{
    public enum BlockCategory
    {
        Dirt,
        Ore,
        Hazard,
        PowerUp
    }

    // Behavior tag for Hazard/PowerUp blocks; systems outside map-gen (player, miners, VFX)
    // react to this when a cell with a matching category is mined.
    public enum HazardBehavior
    {
        None,
        Explosive,
        FallingRock,
        LowVis,
        WaterPocket,
        GasPocket,
        Lava,
        TreasureChest,
        SightPotion
    }

    [CreateAssetMenu(fileName = "BlockType", menuName = "Map Generation/Block Type")]
    public class BlockType : ScriptableObject
    {
        [Tooltip("Must be unique across the BlockTypeDatabase. 0 is reserved for 'unset'.")]
        public byte Id;
        public string DisplayName;
        public BlockCategory Category;
        public HazardBehavior HazardBehavior = HazardBehavior.None;
        public TileBase Tile;

        [Tooltip("Sell value.")]
        public float Value;
        [Tooltip("Inventory weight per unit.")]
        public float Weight;
        public float MiningTime = 1f;
        public Color Tint = Color.white;
    }
}
