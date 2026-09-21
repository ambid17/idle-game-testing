using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapGeneration
{
    public enum BlockCategory
    {
        Dirt,
        Ore,
        Hazard,
        PowerUp,
        Artifact
    }

    public enum BlockTypeId : byte
    {
        GrassyDirt = 0,
        Dirt = 1,
        ScrapAlloy = 2,
        Stone = 3,
        Coal = 4,
        IronOre = 5,
        GoldOre = 6,
        EmeraldOre = 7,
        DiamondOre = 8,
        Artifact = 9,
        TitaniumAlloy = 10,
        Voidstone = 11,
        PlasmaQuartz = 12,
        NaniteOre = 13,
        GravitonShard = 14,
        FusionCoreCrystal = 15,
        SingularityOre = 16,
        AetherCircuitry = 17,
        PrecursorAlloy = 18,
        // Hazard block types (Category.Hazard) - one BlockTypeId per HazardBehavior below, backing
        // the hazard deepening pass. Append-only, same rule as every id above.
        Explosive = 19,
        FallingRock = 20,
        GasPocket = 21,
        Lava = 22,
    }

    // Behavior tag for Hazard/PowerUp blocks; systems outside map-gen (player, miners, VFX)
    // react to this when a cell with a matching category is mined.
    public enum CustomBehavior
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
        public CustomBehavior CustomBehavior = CustomBehavior.None;
        public TileBase Tile;
        public Sprite Icon;

        [Tooltip("Sell value.")]
        public float Value;
        [Tooltip("Inventory weight per unit.")]
        public float Weight;
        public float Health = 1f;
        public Color Tint = Color.white;
    }
}
