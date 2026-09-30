using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    // Weighted picks over LayerConfig tables. weightOf, when given, replaces entry.Weight (run
    // modifiers scale specific ores/hazards through it); null reads entry.Weight directly, so the
    // un-modified path is float-for-float identical to the original implementation.
    public static class WeightedTables
    {
        // fillerWeight is an implicit extra entry rolled ahead of the table's own entries; landing
        // on it returns null (the ore roll uses it for Dirt, see ChunkGenerator.DirtFillerWeight).
        // Rolled first so layers that used to author Dirt as their first entry keep the same
        // roll -> block mapping.
        public static BlockType PickWeighted(IReadOnlyList<WeightedBlockEntry> table, float roll01, float fillerWeight = 0f, Func<WeightedBlockEntry, float> weightOf = null)
        {
            float total = fillerWeight;
            for (int i = 0; i < table.Count; i++) total += WeightOf(table[i], weightOf);
            if (total <= 0f)
            {
                Debug.LogWarning("Weighted table has no weight, returning null");
                return null;
            }

            float target = roll01 * total;
            float cumulative = fillerWeight;
            if (fillerWeight > 0f && target <= cumulative) return null;
            for (int i = 0; i < table.Count; i++)
            {
                cumulative += WeightOf(table[i], weightOf);
                if (target <= cumulative) return table[i].BlockType;
            }

            return table.Count > 0 ? table[^1].BlockType : null;
        }

        // Same weighted roll as PickWeighted, restricted to Ore-category entries. Null if the table
        // has no ore.
        public static BlockType PickWeightedOre(IReadOnlyList<WeightedBlockEntry> table, float roll01, Func<WeightedBlockEntry, float> weightOf = null)
        {
            float total = 0f;
            for (int i = 0; i < table.Count; i++)
            {
                if (IsOreEntry(table[i])) total += WeightOf(table[i], weightOf);
            }
            if (total <= 0f) return null;

            float target = roll01 * total;
            float cumulative = 0f;
            BlockType last = null;
            for (int i = 0; i < table.Count; i++)
            {
                if (!IsOreEntry(table[i])) continue;
                cumulative += WeightOf(table[i], weightOf);
                last = table[i].BlockType;
                if (target <= cumulative) return last;
            }

            return last;
        }

        public static bool IsOreEntry(WeightedBlockEntry entry) => entry.BlockType != null && entry.BlockType.Category == BlockCategory.Ore;

        private static float WeightOf(WeightedBlockEntry entry, Func<WeightedBlockEntry, float> weightOf) =>
            weightOf != null ? weightOf(entry) : entry.Weight;
    }
}
