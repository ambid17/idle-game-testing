using UnityEngine;
using UnityEngine.Tilemaps;

namespace Museum
{
    // One of the rune tablets an Artifact block can show. Which rune a given artifact cell carries is
    // hashed from its seed and cell (MuseumCollectionDatabase.GetRuneAt), so it needs no map data.
    // Turning a newly found rune in at the Museum curator (MuseumCuratorController) adds it to the
    // collection; accessories unlock by how many different runes are in it.
    [CreateAssetMenu(fileName = "Rune", menuName = "Museum/Rune Definition")]
    public class RuneDefinition : ScriptableObject
    {
        [Tooltip("0-based, unique, matches this rune's position in MuseumCollectionDatabase.Runes and its row in the rune debris sheet. Saved as this int - append-only.")]
        public int Index;
        public string DisplayName;
        [Tooltip("The curator's (confident, almost certainly wrong) translation. Shown in the rune grid once donated, and read out when it's turned in.")]
        [TextArea(2, 4)]
        public string CuratorTranslation;
        [Tooltip("The full tablet with this rune - UI icon, notifications and pickup nugget.")]
        public Sprite Icon;
        [Tooltip("Transparent tablet foreground drawn over the layer's dirt (like the Artifact BlockType's own Tile).")]
        public TileBase Tile;
    }
}
