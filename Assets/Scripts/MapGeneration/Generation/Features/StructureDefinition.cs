using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    public enum StructureCellAction
    {
        // Leave whatever the generator rolled.
        Keep = 0,
        // Pre-carved open ground.
        Carve = 1,
        // Place the palette entry's Block.
        Block = 2,
        // An ore rolled from this layer's table.
        OreThisLayer = 3,
        // An ore rolled from the next layer's table.
        OreNextLayer = 4,
        Artifact = 5,
        // A power-up rolled from this layer's PowerUpTable (Treasure Chest if it has none).
        PowerUp = 6,
    }

    [Serializable]
    public class StructurePaletteEntry
    {
        public char Symbol = '#';
        public StructureCellAction Action = StructureCellAction.Block;
        [Tooltip("Only used by the Block action.")]
        public BlockType Block;
    }

    // A hand-authored layout, stamped onto a layer by StructureStampFeature. The shape is a text
    // grid (one line per row, top row first) so layouts are quick to write by hand and diff
    // readably in git. Built-in symbols need no palette entry:
    //   '.' or ' '  keep        '_'  carve open      'o'  ore (this layer)   'O'  ore (next layer)
    //   'A'  artifact           'P'  power-up
    // Any other symbol (e.g. '#' for a wall block) must be defined in the palette, which can also
    // override the built-ins.
    [CreateAssetMenu(fileName = "Structure", menuName = "Map Generation/Structure")]
    public class StructureDefinition : ScriptableObject
    {
        [TextArea(4, 24)]
        [SerializeField] private string layout = "";
        [SerializeField] private List<StructurePaletteEntry> palette = new();

        [Header("Stamping")]
        [Tooltip("May be mirrored left-right.")]
        [SerializeField] private bool allowMirror = true;
        [Tooltip("May be rotated in 90 degree steps.")]
        [SerializeField] private bool allowRotation;
        [Tooltip("Hazards within this many cells of the footprint are swapped for Dirt. -1 = leave them.")]
        [SerializeField] private int clearHazardMargin = 1;
        [Tooltip("Claim the whole bounding box (true) or only the non-Keep cells (false).")]
        [SerializeField] private bool claimFootprint = true;

        public bool AllowMirror => allowMirror;
        public bool AllowRotation => allowRotation;
        public int ClearHazardMargin => clearHazardMargin;
        public bool ClaimFootprint => claimFootprint;

        [NonSerialized] private char[,] parsed;
        [NonSerialized] private string parsedFrom;

        // [x, y] with y = 0 the top row, padded with '.' to a rectangle.
        public char[,] Grid
        {
            get
            {
                if (parsed == null || parsedFrom != layout) Parse();
                return parsed;
            }
        }

        private void Parse()
        {
            parsedFrom = layout;
            var lines = (layout ?? "").Replace("\r", "").Split('\n');
            // Trim blank leading/trailing lines so authors can pad the TextArea freely.
            int first = 0, last = lines.Length - 1;
            while (first <= last && lines[first].Trim().Length == 0) first++;
            while (last >= first && lines[last].Trim().Length == 0) last--;

            int height = Mathf.Max(0, last - first + 1);
            int width = 0;
            for (int i = first; i <= last; i++) width = Mathf.Max(width, lines[i].Length);

            parsed = new char[width, height];
            for (int y = 0; y < height; y++)
            {
                var line = lines[first + y];
                for (int x = 0; x < width; x++) parsed[x, y] = x < line.Length ? line[x] : '.';
            }
        }

        public bool TryResolve(char symbol, out StructureCellAction action, out BlockType block)
        {
            block = null;
            foreach (var entry in palette)
            {
                if (entry.Symbol != symbol) continue;
                action = entry.Action;
                block = entry.Block;
                return true;
            }

            switch (symbol)
            {
                case '.': case ' ': action = StructureCellAction.Keep; return true;
                case '_': action = StructureCellAction.Carve; return true;
                case 'o': action = StructureCellAction.OreThisLayer; return true;
                case 'O': action = StructureCellAction.OreNextLayer; return true;
                case 'A': action = StructureCellAction.Artifact; return true;
                case 'P': action = StructureCellAction.PowerUp; return true;
                default: action = StructureCellAction.Keep; return false;
            }
        }

        private void OnValidate()
        {
            parsed = null;
            var grid = Grid;
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                for (int x = 0; x < grid.GetLength(0); x++)
                {
                    if (!TryResolve(grid[x, y], out var action, out var block))
                    {
                        Debug.LogError($"StructureDefinition '{name}': symbol '{grid[x, y]}' at ({x},{y}) has no palette entry.");
                        return;
                    }
                    if (action == StructureCellAction.Block && block == null)
                    {
                        Debug.LogError($"StructureDefinition '{name}': palette symbol '{grid[x, y]}' uses the Block action but has no Block assigned.");
                        return;
                    }
                }
            }
        }
    }
}
