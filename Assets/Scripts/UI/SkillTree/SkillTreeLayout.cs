using System.Collections.Generic;
using UnityEngine;

namespace UI.SkillTree
{
    // Computes a polar (converted to Cartesian) position for every node in a skill tree: each
    // Branch gets a fixed angular sector, and within a sector a node's radius is its
    // Prerequisite-chain depth from that chain's root. Pure/static so it's usable without any
    // MonoBehaviour and easy to sanity-check independent of prefab/scene setup.
    public static class SkillTreeLayout
    {
        public static Dictionary<ISkillTreeLayoutNode, Vector2> Compute(
            IReadOnlyList<ISkillTreeLayoutNode> nodes,
            int branchCount,
            float depthSpacing = 220f,
            float sectorPaddingDegrees = 8f,
            float startAngleDegrees = -90f)
        {
            var positions = new Dictionary<ISkillTreeLayoutNode, Vector2>();
            if (nodes == null || nodes.Count == 0 || branchCount <= 0) return positions;

            var depthCache = new Dictionary<ISkillTreeLayoutNode, int>();
            var rootCache = new Dictionary<ISkillTreeLayoutNode, ISkillTreeLayoutNode>();
            foreach (var node in nodes) ResolveChain(node, depthCache, rootCache);

            // Group siblings so they can be spread evenly across their sector: nodes sharing a
            // branch/depth/root-chain sit at the same radius and fan out side by side instead of
            // stacking on top of each other. Scoping by root (not just branch+depth) keeps a
            // branch's independent chains - e.g. Automation's separate Automaton/FuelDrone/
            // StorageDrone roots - as visually distinct sub-fans rather than merging them.
            var groups = new Dictionary<(int branch, int depth, ISkillTreeLayoutNode root), List<ISkillTreeLayoutNode>>();
            foreach (var node in nodes)
            {
                var key = (node.BranchIndex, depthCache[node], rootCache[node]);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<ISkillTreeLayoutNode>();
                    groups[key] = list;
                }
                list.Add(node);
            }

            float sectorWidth = 360f / branchCount;
            foreach (var pair in groups)
            {
                var key = pair.Key;
                var siblings = pair.Value;
                float sectorCenter = startAngleDegrees + key.branch * sectorWidth;
                float usableWidth = Mathf.Max(0f, sectorWidth - 2f * sectorPaddingDegrees);
                float radius = (key.depth + 1) * depthSpacing;

                for (int i = 0; i < siblings.Count; i++)
                {
                    float angleDegrees = siblings.Count == 1
                        ? sectorCenter
                        : sectorCenter - usableWidth / 2f + (i + 0.5f) * usableWidth / siblings.Count;

                    float radians = angleDegrees * Mathf.Deg2Rad;
                    positions[siblings[i]] = new Vector2(radius * Mathf.Cos(radians), radius * Mathf.Sin(radians));
                }
            }

            return positions;
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
}
