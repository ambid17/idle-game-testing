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
        // PowerUp block types (Category.PowerUp) - player-only mineable, resolved by
        // Player.PlayerPowerUps. Append-only, same rule as every id above.
        TreasureChest = 23,
        DrillOverdrive = 24,
        FuelCanister = 25,
        RepairKit = 26,
        LuckyStrike = 27,
        // Structure blocks (Category.Dirt, Unmineable) - placed by MapFeatureDefinitions such as
        // BandFeature. Append-only, same rule as every id above.
        Hardpan = 28,
        // PowerUp - see the PowerUp block comment above.
        Portal = 29,
        // Structure block (Category.Dirt, mineable but tough) - the masonry of the set-piece
        // rooms stamped by StructureStampFeature.
        AncientBrick = 30,
    }

    // Behavior tag for Hazard/PowerUp blocks; systems outside map-gen (player, miners, VFX)
    // react to this when a cell with a matching category is mined. Hazards resolve through
    // CustomBlockTriggeredEvent; PowerUps are applied directly by Player.PlayerPowerUps.
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
        DrillOverdrive = 8,
        FuelCanister = 9,
        RepairKit = 10,
        LuckyStrike = 11,
        Portal = 12
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
        [Tooltip("Tile is a transparent foreground: ChunkTilemapView paints the layer's (tinted) Dirt tile in the terrain layer and draws Tile on top, so the ground matches the surrounding dirt in every biome.")]
        public bool DrawDirtBehind;
        public Sprite Icon;

        // Ores/artifacts use their transparent tile foreground as their icon, so UI draws the Dirt
        // icon behind it (Image.SetIcon) the same way the tilemap does. Null for blocks with their
        // own opaque icon art (power-ups, hazards, dirt itself).
        public Sprite IconBackground => DrawDirtBehind && Tile is UnityEngine.Tilemaps.Tile tile && tile.sprite == Icon
            ? GameManager.BlockTypeDatabase.Get((byte)BlockTypeId.Dirt).Icon
            : null;

        [Tooltip("Sell value.")]
        public float Value;
        [Tooltip("Inventory weight per unit.")]
        public float Weight;
        public float Health = 1f;
        [Tooltip("Can never be mined by anyone (player, automatons, explosions) - e.g. Hardpan bands. GrassyDirt/FallingRock predate this flag and are special-cased where they're refused.")]
        public bool Unmineable;
        public Color Tint = Color.white;
        [Tooltip("Pixel color on the HUD minimap (UI.MinimapUI).")]
        public Color MinimapColor = new(0.45f, 0.32f, 0.22f, 1f);

        [Tooltip("Shown by the Analyzer ability (Player.PlayerAnalyzer) - one picked at random per scan. Hazards/PowerUps: what it does. Ores: a joke. Artifacts: a lore tidbit. Leave empty for blocks the Analyzer ignores (Dirt).")]
        [TextArea(2, 4)]
        public string[] AnalyzerLines;
    }
}
