using System;
using System.Collections.Generic;
using Economy;
using UnityEngine;

namespace UI.SkillTree
{
    // Computes a grid position for every node in a skill tree: the root (the one node with no
    // Prerequisite) sits in the center, and every other node sits one cell from its Prerequisite
    // in its own Direction. Pure/static so it's usable without any MonoBehaviour and easy to
    // sanity-check independent of prefab/scene setup.
    public static class SkillTreeLayout
    {
        public static Dictionary<ISkillTreeLayoutNode, Vector2> Compute(
            IReadOnlyList<ISkillTreeLayoutNode> nodes,
            SkillTreeLayoutConfig config)
        {
            var positions = new Dictionary<ISkillTreeLayoutNode, Vector2>();
            if (nodes == null || nodes.Count == 0) return positions;

            // Children keyed by parent, built in input order so which sibling wins a contested
            // cell is deterministic. An out-of-set Prerequisite counts as no Prerequisite.
            var nodeSet = new HashSet<ISkillTreeLayoutNode>(nodes);
            var childrenOf = new Dictionary<ISkillTreeLayoutNode, List<ISkillTreeLayoutNode>>();
            var roots = new List<ISkillTreeLayoutNode>();
            foreach (var node in nodes)
            {
                if (node.Prerequisite == null || !nodeSet.Contains(node.Prerequisite))
                {
                    roots.Add(node);
                    continue;
                }
                if (!childrenOf.TryGetValue(node.Prerequisite, out var list))
                {
                    list = new List<ISkillTreeLayoutNode>();
                    childrenOf[node.Prerequisite] = list;
                }
                list.Add(node);
            }

            if (roots.Count != 1)
            {
                Debug.LogError($"SkillTreeLayout: expected exactly 1 root upgrade (no Prerequisite), found {roots.Count}: {string.Join(", ", roots.ConvertAll(r => r.Name))}. Extra roots are placed below the center.");
            }

            var cells = new Dictionary<ISkillTreeLayoutNode, Vector2Int>();
            var occupied = new HashSet<Vector2Int>();
            foreach (var root in roots)
            {
                PlaceTree(root, FindFreeCell(Vector2Int.zero, Vector2Int.down, occupied), childrenOf, cells, occupied);
            }

            // Anything still unplaced is on a Prerequisite cycle (never reachable from a root).
            foreach (var node in nodes)
            {
                if (cells.ContainsKey(node)) continue;
                Debug.LogError($"SkillTreeLayout: '{node.Name}' is on a cyclic Prerequisite chain; placing it below the center.");
                PlaceTree(node, FindFreeCell(Vector2Int.zero, Vector2Int.down, occupied), childrenOf, cells, occupied);
            }

            foreach (var pair in cells)
            {
                positions[pair.Key] = (Vector2)pair.Value * config.nodeSpacing;
            }
            return positions;
        }

        // Breadth-first from `root`, so nodes nearer the root claim their cells first. A node whose
        // cell is already taken slides further out in its Direction until it finds a free one.
        private static void PlaceTree(
            ISkillTreeLayoutNode root,
            Vector2Int rootCell,
            Dictionary<ISkillTreeLayoutNode, List<ISkillTreeLayoutNode>> childrenOf,
            Dictionary<ISkillTreeLayoutNode, Vector2Int> cells,
            HashSet<Vector2Int> occupied)
        {
            cells[root] = rootCell;
            occupied.Add(rootCell);

            var queue = new Queue<ISkillTreeLayoutNode>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                if (!childrenOf.TryGetValue(parent, out var kids)) continue;

                foreach (var child in kids)
                {
                    if (cells.ContainsKey(child)) continue;

                    var step = ToOffset(child.Direction);
                    var wanted = cells[parent] + step;
                    var cell = FindFreeCell(wanted, step, occupied);
                    if (cell != wanted)
                    {
                        Debug.LogWarning($"SkillTreeLayout: '{child.Name}' overlaps another node {child.Direction} of '{parent.Name}'; pushed further {child.Direction}. Change its Direction to fix.");
                    }

                    cells[child] = cell;
                    occupied.Add(cell);
                    queue.Enqueue(child);
                }
            }
        }

        private static Vector2Int FindFreeCell(Vector2Int start, Vector2Int step, HashSet<Vector2Int> occupied)
        {
            var cell = start;
            while (occupied.Contains(cell)) cell += step;
            return cell;
        }

        private static Vector2Int ToOffset(SkillTreeDirection direction) => direction switch
        {
            SkillTreeDirection.North => Vector2Int.up,
            SkillTreeDirection.East => Vector2Int.right,
            SkillTreeDirection.South => Vector2Int.down,
            SkillTreeDirection.West => Vector2Int.left,
            _ => Vector2Int.right
        };
    }

    [Serializable]
    public class SkillTreeLayoutConfig
    {
        [Tooltip("Distance between neighboring grid cells, in Content units.")]
        public float nodeSpacing = 260f;
    }
}
