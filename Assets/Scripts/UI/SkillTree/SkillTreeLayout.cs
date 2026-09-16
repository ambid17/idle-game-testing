using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.SkillTree
{
    // Computes a polar (converted to Cartesian) position for every node in a skill tree: each
    // Branch gets a fixed angular sector, and within that sector the tree is laid out as a
    // nested radial tree - every node's own angular wedge is subdivided evenly among its
    // children, so a node's whole subtree always stays inside the wedge its parent claimed.
    // Pure/static so it's usable without any MonoBehaviour and easy to sanity-check independent
    // of prefab/scene setup.
    public static class SkillTreeLayout
    {
        public static Dictionary<ISkillTreeLayoutNode, Vector2> Compute(
            IReadOnlyList<ISkillTreeLayoutNode> nodes,
            SkillTreeLayoutConfig config,
            int branchCount)
        {
            var positions = new Dictionary<ISkillTreeLayoutNode, Vector2>();
            if (nodes == null || nodes.Count == 0 || branchCount <= 0) return positions;

            var depthCache = new Dictionary<ISkillTreeLayoutNode, int>();
            var rootCache = new Dictionary<ISkillTreeLayoutNode, ISkillTreeLayoutNode>();
            foreach (var node in nodes) ResolveChain(node, depthCache, rootCache);

            // Children keyed by parent, built in input order so sibling order (and therefore
            // which side of the wedge each child fans to) is deterministic. A node only counts
            // as another's child if that parent is itself part of this layout pass - an
            // out-of-set Prerequisite is treated the same as no Prerequisite (see fallback below).
            var nodeSet = new HashSet<ISkillTreeLayoutNode>(nodes);
            var childrenOf = new Dictionary<ISkillTreeLayoutNode, List<ISkillTreeLayoutNode>>();
            foreach (var node in nodes)
            {
                if (node.Prerequisite == null || !nodeSet.Contains(node.Prerequisite)) continue;
                if (!childrenOf.TryGetValue(node.Prerequisite, out var list))
                {
                    list = new List<ISkillTreeLayoutNode>();
                    childrenOf[node.Prerequisite] = list;
                }
                list.Add(node);
            }

            // Roots (no in-set Prerequisite) get a fixed slot within their branch's sector, in
            // first-appearance order; PlaceSubtree then recurses each root's own wedge down
            // through its descendants, so forks deeper in a chain - e.g. Movement's
            // FuelInventory splitting into MoveSpeed/FuelEfficiency and each of those splitting
            // again into FlightSpeed/FallDamageReduction - branch away from each other instead
            // of drifting across the whole sector and crossing a sibling branch's connectors.
            var branchRoots = new Dictionary<int, List<ISkillTreeLayoutNode>>();
            foreach (var node in nodes)
            {
                if (node.Prerequisite != null && nodeSet.Contains(node.Prerequisite)) continue;
                if (!branchRoots.TryGetValue(node.BranchIndex, out var list))
                {
                    list = new List<ISkillTreeLayoutNode>();
                    branchRoots[node.BranchIndex] = list;
                }
                list.Add(node);
            }

            float sectorWidth = 360f / branchCount;
            foreach (var pair in branchRoots)
            {
                var rootsInBranch = pair.Value;
                float sectorCenter = config.startAngleDegrees + pair.Key * sectorWidth;
                float usableWidth = Mathf.Max(0f, sectorWidth - 2f * config.sectorPaddingDegrees);
                float slotWidth = usableWidth / rootsInBranch.Count;

                for (int i = 0; i < rootsInBranch.Count; i++)
                {
                    float slotCenter = rootsInBranch.Count == 1
                        ? sectorCenter
                        : sectorCenter - usableWidth / 2f + (i + 0.5f) * slotWidth;
                    PlaceSubtree(rootsInBranch[i], slotCenter, slotWidth, depthCache, childrenOf, config, positions);
                }
            }

            // Fallback for any node PlaceSubtree never reached - e.g. a cyclic Prerequisite
            // reference (see ResolveChain) - so it still ends up positioned instead of silently
            // missing from the tree.
            foreach (var node in nodes)
            {
                if (positions.ContainsKey(node)) continue;
                float sectorCenter = config.startAngleDegrees + node.BranchIndex * sectorWidth;
                float usableWidth = Mathf.Max(0f, sectorWidth - 2f * config.sectorPaddingDegrees);
                PlaceSubtree(node, sectorCenter, usableWidth, depthCache, childrenOf, config, positions);
            }

            return positions;
        }

        // Places `node` at its assigned angle/radius, then splits node's own wedge evenly among
        // its children so each child (and everything under it) stays nested inside that wedge.
        // Guards re-entry so a cycle that slips past ResolveChain's own guard (or the fallback
        // pass re-walking a node PlaceSubtree already reached) can't recurse forever.
        private static void PlaceSubtree(
            ISkillTreeLayoutNode node,
            float angleDegrees,
            float wedgeWidth,
            Dictionary<ISkillTreeLayoutNode, int> depthCache,
            Dictionary<ISkillTreeLayoutNode, List<ISkillTreeLayoutNode>> childrenOf,
            SkillTreeLayoutConfig config,
            Dictionary<ISkillTreeLayoutNode, Vector2> positions)
        {
            if (positions.ContainsKey(node)) return;

            float radius = (depthCache[node] + 1) * config.depthSpacing;
            float radians = angleDegrees * Mathf.Deg2Rad;
            positions[node] = new Vector2(radius * Mathf.Cos(radians), radius * Mathf.Sin(radians));

            if (!childrenOf.TryGetValue(node, out var kids) || kids.Count == 0) return;

            float childWidth = wedgeWidth / kids.Count;
            for (int i = 0; i < kids.Count; i++)
            {
                float childAngle = kids.Count == 1
                    ? angleDegrees
                    : angleDegrees - wedgeWidth / 2f + (i + 0.5f) * childWidth;
                PlaceSubtree(kids[i], childAngle, childWidth, depthCache, childrenOf, config, positions);
            }
        }

        // Walks a node's Prerequisite chain toward its root, memoizing both depth-from-root and
        // the root itself for every node visited along the way (so a later node sharing part of
        // the same chain resolves in O(1)). Guards against a cyclic Prerequisite reference (not
        // expected from normal single-parent authoring) by breaking the walk and treating the
        // point of the cycle as a root, rather than looping forever.
        private static void ResolveChain(
            ISkillTreeLayoutNode node,
            Dictionary<ISkillTreeLayoutNode, int> depthCache,
            Dictionary<ISkillTreeLayoutNode, ISkillTreeLayoutNode> rootCache)
        {
            if (node == null || depthCache.ContainsKey(node)) return;

            var chain = new List<ISkillTreeLayoutNode>();
            var current = node;
            while (current != null && !depthCache.ContainsKey(current))
            {
                if (chain.Contains(current))
                {
                    Debug.LogError("SkillTreeLayout: cyclic Prerequisite reference detected; breaking the cycle by treating this node as a root.");
                    current = null;
                    break;
                }
                chain.Add(current);
                current = current.Prerequisite;
            }

            int baseDepth = current != null ? depthCache[current] + 1 : 0;
            var root = current != null ? rootCache[current] : chain[chain.Count - 1];

            for (int i = 0; i < chain.Count; i++)
            {
                depthCache[chain[i]] = baseDepth + (chain.Count - 1 - i);
                rootCache[chain[i]] = root;
            }
        }
    }

    [Serializable]
    public class SkillTreeLayoutConfig
    {
        public float depthSpacing = 400f;
        public float sectorPaddingDegrees = 8f;
        public float startAngleDegrees = -90f;
    }
}
