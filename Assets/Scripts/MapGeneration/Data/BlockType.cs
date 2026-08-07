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

    public enum BlockTypeId : byte
    {
        GrassyDirt = 0,
        Dirt = 1,
        Wood = 2,
        Stone = 3,
        Coal = 4,
        IronOre = 5,
        GoldOre = 6,
        EmeraldOre = 7,
        DiamondOre = 8,
    }

    // Behavior tag for Hazard/PowerUp blocks; systems outside map-gen (player, miners, VFX)
    // react to this when a cell with a matching category is mined.
    public enum HazardBehavior
    {
        None = 0,
        Explosive = 1,
        FallingRock = 2,
        LowVis = 3,
        WaterPocket = 4,
        GasPocket = 5,
        Lava = 6,
        TreasureChest = 7,
        SightPotion = 8
    }

    [CreateAssetMenu(fileName = "BlockType", menuName = "Map Generation/Block Type")]
    public class BlockType : ScriptableObject
    {
        [Tooltip("Must be unique across the BlockTypeDatabase. 0 is reserved for 'unset'.")]
        public BlockTypeId Id;
        public string DisplayName;
        public BlockCategory Category;
        public HazardBehavior HazardBehavior = HazardBehavior.None;
        public TileBase Tile;

        [Tooltip("Sell value.")]
        public float Value;
        [Tooltip("Inventory weight per unit.")]
        public float Weight;
        public float Health = 1f;
        public Color Tint = Color.white;
    }
}
