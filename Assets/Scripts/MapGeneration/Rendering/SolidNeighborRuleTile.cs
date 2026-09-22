using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapGeneration.Rendering
{
    // Inverse of EmptyNeighborRuleTile: this tile sits in MINED (empty) cells and needs to know
    // which neighbors are solid (any block type at all - ore, dirt, stone), not which are empty.
    // Every mined cell holds this same shared tile instance (not null, so its edges can react to
    // solid neighbors), so "solid" means "occupied by a tile that isn't this one", not just
    // "non-null" - a mined neighbor is non-null too since it also holds this tile.
    public class SolidNeighborRuleTile : RuleTile
    {
        public const int AnySolid = 3;

        public override bool RuleMatch(int neighbor, TileBase tile)
        {
            if (neighbor == AnySolid) return tile != null && tile != this;
            return base.RuleMatch(neighbor, tile);
        }
    }
}
