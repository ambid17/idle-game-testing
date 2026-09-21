using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapGeneration.Rendering
{
    // RuleTile's stock neighbor codes only distinguish This (1, same tile asset) vs NotThis
    // (2, any different tile - including ore/stone). Dirt's dug-edge art must only trigger when
    // the neighbor cell is genuinely empty (mined), not merely a different block type sitting
    // unmined next to it. Nothing (3) adds that missing "neighbor tile is literally null" check.
    public class EmptyNeighborRuleTile : RuleTile
    {
        public const int Nothing = 3;

        public override bool RuleMatch(int neighbor, TileBase tile)
        {
            if (neighbor == Nothing) return tile == null;
            return base.RuleMatch(neighbor, tile);
        }
    }
}
