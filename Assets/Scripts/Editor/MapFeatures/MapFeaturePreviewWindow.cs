using System.Collections.Generic;
using System.Text;
using MapGeneration;
using RunModifiers;
using UnityEditor;
using UnityEngine;

namespace MapGenerationEditor
{
    // Renders a generated layer as a minimap-coloured grid, with an optional run modifier and/or
    // extra MapFeatureDefinition applied on top - for designing StructureDefinitions and tuning
    // features/modifiers without entering Play Mode. Cells that differ from the un-modified
    // layer can be lightened so a feature's footprint stands out, every stamped structure is
    // outlined, and a legend names each colour.
    //
    // Assigning a Structure also opens a paint editor for its layout: pick a brush, left-drag to
    // paint, right-click to pick up the symbol under the cursor. The structure is force-stamped
    // onto the preview (ignoring its feature's layer range and chance) so edits show immediately.
    public class MapFeaturePreviewWindow : EditorWindow
    {
        private const int EditCellPixels = 22;
        private const float LegendItemWidth = 170f;
        private const char KeepSymbol = '.';

        private static readonly Color OpenColor = new(0.05f, 0.05f, 0.07f);
        private static readonly Color KeepColor = new(0.27f, 0.27f, 0.27f);
        private static readonly Color OreThisLayerColor = new(0.85f, 0.55f, 0.2f);
        private static readonly Color OreNextLayerColor = new(0.95f, 0.8f, 0.3f);
        private static readonly Color StructureOutlineColor = Color.white;
        private static readonly Color EditedOutlineColor = Color.yellow;

        private static readonly char[] BuiltInSymbols = { KeepSymbol, '_', 'o', 'O', 'A', 'P' };

        private LayerConfigProvider provider;
        private BlockTypeDatabase blockTypes;
        private RunModifierDefinition modifier;
        private MapFeatureDefinition extraFeature;
        private StructureDefinition structure;
        private int seed = 12345;
        private int layer = 3;
        private int gridWidth = 30;
        private int targetLayer = 3;
        private BlockTypeId oreA = BlockTypeId.IronOre;
        private BlockTypeId oreB = BlockTypeId.Coal;
        private bool highlightChanges = true;
        private bool outlineStructures = true;
        private int cellPixels = 12;

        private Texture2D texture;
        private Vector2 scroll;
        private string summary = "";
        private readonly List<(Color color, string name, int count)> legend = new();
        private readonly List<(StructureDefinition structure, RectInt rect)> stamped = new();

        // Stamps `structure` on every previewed layer; never saved.
        private StructureStampFeature forcedStamp;
        private char brush = '#';
        private string newSymbol = "#";
        private BlockType newBlock;
        private bool needsRegenerate;

        [MenuItem("Tools/Map Generation/Feature Preview")]
        private static void Open() => GetWindow<MapFeaturePreviewWindow>("Map Feature Preview");

        private void OnEnable()
        {
            provider ??= FindAsset<LayerConfigProvider>();
            blockTypes ??= FindAsset<BlockTypeDatabase>();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (forcedStamp != null) DestroyImmediate(forcedStamp);
            if (texture != null) DestroyImmediate(texture);
        }

        private void OnUndoRedo()
        {
            needsRegenerate = true;
            Repaint();
        }

        private static T FindAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).FullName}");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUI.BeginChangeCheck();
            provider = (LayerConfigProvider)EditorGUILayout.ObjectField("Layer Provider", provider, typeof(LayerConfigProvider), false);
            blockTypes = (BlockTypeDatabase)EditorGUILayout.ObjectField("Block Types", blockTypes, typeof(BlockTypeDatabase), false);
            modifier = (RunModifierDefinition)EditorGUILayout.ObjectField("Run Modifier", modifier, typeof(RunModifierDefinition), false);
            extraFeature = (MapFeatureDefinition)EditorGUILayout.ObjectField("Extra Feature", extraFeature, typeof(MapFeatureDefinition), false);
            structure = (StructureDefinition)EditorGUILayout.ObjectField(new GUIContent("Structure", "Edit this structure's layout and force-stamp it onto the preview."), structure, typeof(StructureDefinition), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.IntField("Seed", seed);
                if (GUILayout.Button("Random", GUILayout.Width(70))) seed = Random.Range(int.MinValue, int.MaxValue);
            }
            layer = Mathf.Max(0, EditorGUILayout.IntField("Layer", layer));
            gridWidth = Mathf.Max(5, EditorGUILayout.IntField("Grid Width", gridWidth));
            if (modifier != null)
            {
                if (modifier.UsesTargetLayer) targetLayer = EditorGUILayout.IntField("Target Layer", targetLayer);
                if (modifier.UsesOreA) oreA = (BlockTypeId)EditorGUILayout.EnumPopup("Ore A", oreA);
                if (modifier.UsesOreB) oreB = (BlockTypeId)EditorGUILayout.EnumPopup("Ore B", oreB);
            }
            highlightChanges = EditorGUILayout.Toggle("Lighten Changes", highlightChanges);
            outlineStructures = EditorGUILayout.Toggle("Outline Structures", outlineStructures);
            cellPixels = EditorGUILayout.IntSlider("Cell Size", cellPixels, 4, 24);
            bool changed = EditorGUI.EndChangeCheck();

            if (structure != null && blockTypes != null) DrawStructureEditor();

            if (GUILayout.Button("Generate") || changed || needsRegenerate || texture == null) Regenerate();

            EditorGUILayout.HelpBox(summary, MessageType.None);
            if (texture != null)
            {
                DrawLegend();
                DrawPreview();
            }

            EditorGUILayout.EndScrollView();
        }

        // ---- Layer preview ----

        private void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(texture.width * cellPixels, texture.height * cellPixels, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            if (!outlineStructures) return;

            foreach (var (stampedStructure, cells) in stamped)
            {
                var outline = new Rect(rect.x + cells.xMin * cellPixels, rect.y + cells.yMin * cellPixels, cells.width * cellPixels, cells.height * cellPixels);
                DrawOutline(outline, stampedStructure == structure ? EditedOutlineColor : StructureOutlineColor);
            }
        }

        // 1px frame just inside rect.
        private static void DrawOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private void DrawLegend()
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / LegendItemWidth));
            int total = legend.Count + (outlineStructures ? 2 : 0);

            for (int i = 0; i < total; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(total, i + perRow); j++)
                    {
                        if (j < legend.Count) DrawLegendItem(legend[j].color, $"{legend[j].name} ({legend[j].count})", filled: true);
                        else if (j == legend.Count) DrawLegendItem(StructureOutlineColor, "Structure", filled: false);
                        else DrawLegendItem(EditedOutlineColor, "Edited structure", filled: false);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void DrawLegendItem(Color color, string label, bool filled)
        {
            var swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.Height(14f));
            swatch.y += 2f;
            if (filled) EditorGUI.DrawRect(swatch, color);
            else
            {
                EditorGUI.DrawRect(swatch, OpenColor);
                DrawOutline(swatch, color);
            }
            GUILayout.Label(label, GUILayout.Width(LegendItemWidth - 20f));
        }

        private void Regenerate()
        {
            needsRegenerate = false;
            if (provider == null || blockTypes == null)
            {
                summary = "Assign a LayerConfigProvider and BlockTypeDatabase.";
                return;
            }

            var config = provider.GetConfig(layer);
            var next = provider.GetConfig(layer + 1);
            if (config == null)
            {
                summary = "No LayerConfig for that layer.";
                return;
            }

            var state = new RunModifierState
            {
                ModifierId = modifier != null ? modifier.Id : null,
                OreA = oreA,
                OreB = oreB,
                TargetLayer = targetLayer,
            };
            var tweaks = modifier != null ? RunModifierResolver.BuildTweaks(modifier, state, layer) : new LayerGenerationTweaks();
            if (extraFeature != null) tweaks.Features.Add(extraFeature);
            if (structure != null) tweaks.Features.Add(GetForcedStamp());

            var chunk = ChunkGenerator.Generate(seed, layer, gridWidth, config, config.LayerHeight, 1f, 0f, 0f, next, tweaks);
            var baseline = ChunkGenerator.Generate(seed, layer, gridWidth, config, config.LayerHeight, 1f, 0f, 0f, next);

            stamped.Clear();
            stamped.AddRange(chunk.StampedStructures);

            if (texture != null) DestroyImmediate(texture);
            texture = new Texture2D(chunk.Width, chunk.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

            int changedCells = 0;
            var entries = new Dictionary<string, (Color color, int count)>();
            for (int y = 0; y < chunk.Height; y++)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    var cell = chunk.Cells[chunk.Index(x, y)];
                    var before = baseline.Cells[baseline.Index(x, y)];
                    bool differs = cell.BlockTypeId != before.BlockTypeId || cell.Mined != before.Mined;
                    if (differs) changedCells++;

                    var block = blockTypes.Get(cell.BlockTypeId);
                    Color baseColor = cell.Mined ? OpenColor : block != null ? block.MinimapColor : Color.magenta;
                    Color color = highlightChanges && differs ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;

                    // Texture rows run bottom-up; chunk rows run top-down.
                    texture.SetPixel(x, chunk.Height - 1 - y, color);

                    string key = cell.Mined ? "(open)" : block != null ? block.DisplayName : $"id {cell.BlockTypeId}";
                    entries.TryGetValue(key, out var entry);
                    entries[key] = (baseColor, entry.count + 1);
                }
            }
            texture.Apply();

            legend.Clear();
            foreach (var kvp in entries) legend.Add((kvp.Value.color, kvp.Key, kvp.Value.count));
            legend.Sort((a, b) => b.count.CompareTo(a.count));

            summary = $"{chunk.Width}x{chunk.Height}, {changedCells} cells differ from the un-modified layer, {stamped.Count} structure(s) stamped.";
            if (structure != null && !stamped.Exists(s => s.structure == structure)) summary += $" No room was found for {structure.name} - try another seed or a wider grid.";
        }

        // A hidden feature that stamps `structure` once on any layer, every time.
        private StructureStampFeature GetForcedStamp()
        {
            if (forcedStamp == null)
            {
                forcedStamp = CreateInstance<StructureStampFeature>();
                forcedStamp.hideFlags = HideFlags.HideAndDontSave;
            }

            var so = new SerializedObject(forcedStamp);
            so.FindProperty("id").stringValue = "feature_preview_forced_stamp";
            so.FindProperty("structure").objectReferenceValue = structure;
            so.FindProperty("placement.MinLayer").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return forcedStamp;
        }

        // ---- Structure editor ----

        private void DrawStructureEditor()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Edit {structure.name}", EditorStyles.boldLabel);

            var grid = structure.Grid;
            int w = grid.GetLength(0), h = grid.GetLength(1);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{w} x {h}", GUILayout.Width(60));
                DrawResizeButtons("Top", grid, 0, 1, 0, 0);
                DrawResizeButtons("Bottom", grid, 0, 0, 0, 1);
                DrawResizeButtons("Left", grid, 1, 0, 0, 0);
                DrawResizeButtons("Right", grid, 0, 0, 1, 0);
                GUILayout.FlexibleSpace();
            }

            DrawBrushes();
            DrawEditGrid(grid);
            EditorGUILayout.Space();
        }

        // "- Side +" : removes or adds one row/column on that side.
        private void DrawResizeButtons(string side, char[,] grid, int left, int top, int right, int bottom)
        {
            if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(20))) WriteLayout(Resized(grid, -left, -top, -right, -bottom), $"Shrink {structure.name}");
            GUILayout.Label(side, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(42));
            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(20))) WriteLayout(Resized(grid, left, top, right, bottom), $"Grow {structure.name}");
            GUILayout.Space(8);
        }

        private static char[,] Resized(char[,] grid, int left, int top, int right, int bottom)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            int newW = w + left + right, newH = h + top + bottom;
            if (newW < 1 || newH < 1) return grid;

            var resized = new char[newW, newH];
            for (int y = 0; y < newH; y++)
            {
                for (int x = 0; x < newW; x++)
                {
                    int sx = x - left, sy = y - top;
                    resized[x, y] = sx >= 0 && sx < w && sy >= 0 && sy < h ? grid[sx, sy] : KeepSymbol;
                }
            }
            return resized;
        }

        // The built-in symbols plus the structure's palette - doubles as the editor's legend.
        private void DrawBrushes()
        {
            var symbols = new List<char>(BuiltInSymbols);
            var so = new SerializedObject(structure);
            var palette = so.FindProperty("palette");
            for (int i = 0; i < palette.arraySize; i++)
            {
                char symbol = (char)palette.GetArrayElementAtIndex(i).FindPropertyRelative("Symbol").intValue;
                if (!symbols.Contains(symbol)) symbols.Add(symbol);
            }

            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / LegendItemWidth));
            for (int i = 0; i < symbols.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(symbols.Count, i + perRow); j++) DrawBrush(symbols[j]);
                    GUILayout.FlexibleSpace();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("New block brush", GUILayout.Width(100));
                newSymbol = GUILayout.TextField(newSymbol, 1, GUILayout.Width(24));
                newBlock = (BlockType)EditorGUILayout.ObjectField(newBlock, typeof(BlockType), false, GUILayout.Width(180));
                using (new EditorGUI.DisabledScope(newBlock == null || newSymbol.Length != 1 || symbols.Contains(newSymbol[0])))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(50)))
                    {
                        palette.arraySize++;
                        var entry = palette.GetArrayElementAtIndex(palette.arraySize - 1);
                        entry.FindPropertyRelative("Symbol").intValue = newSymbol[0];
                        entry.FindPropertyRelative("Action").intValue = (int)StructureCellAction.Block;
                        entry.FindPropertyRelative("Block").objectReferenceValue = newBlock;
                        so.ApplyModifiedProperties();
                        brush = newSymbol[0];
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawBrush(char symbol)
        {
            var row = GUILayoutUtility.GetRect(LegendItemWidth, 18f, GUILayout.Width(LegendItemWidth));
            if (symbol == brush) EditorGUI.DrawRect(row, new Color(0.24f, 0.48f, 0.9f, 0.5f));
            DrawSymbolCell(new Rect(row.x + 2f, row.y + 1f, 16f, 16f), symbol);
            GUI.Label(new Rect(row.x + 22f, row.y, row.width - 22f, row.height), DescribeSymbol(symbol));

            if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
            {
                brush = symbol;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawEditGrid(char[,] grid)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            var rect = GUILayoutUtility.GetRect(w * EditCellPixels + 2, h * EditCellPixels + 2, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            var cells = new Rect(rect.x + 1f, rect.y + 1f, w * EditCellPixels, h * EditCellPixels);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(cells, Color.black);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        DrawSymbolCell(new Rect(cells.x + x * EditCellPixels, cells.y + y * EditCellPixels, EditCellPixels - 1, EditCellPixels - 1), grid[x, y]);
                    }
                }
                DrawOutline(rect, EditedOutlineColor);
            }

            var evt = Event.current;
            bool painting = evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag;
            if (!painting || !cells.Contains(evt.mousePosition)) return;

            int cx = Mathf.Clamp(Mathf.FloorToInt((evt.mousePosition.x - cells.x) / EditCellPixels), 0, w - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt((evt.mousePosition.y - cells.y) / EditCellPixels), 0, h - 1);
            if (evt.button == 1) brush = grid[cx, cy];
            else if (evt.button == 0 && grid[cx, cy] != brush)
            {
                var edited = (char[,])grid.Clone();
                edited[cx, cy] = brush;
                WriteLayout(edited, $"Paint {structure.name}");
            }
            evt.Use();
            Repaint();
        }

        private void DrawSymbolCell(Rect rect, char symbol)
        {
            Color color = SymbolColor(symbol);
            EditorGUI.DrawRect(rect, color);

            var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = color.grayscale > 0.5f ? Color.black : Color.white;
            GUI.Label(rect, symbol.ToString(), style);
        }

        private Color SymbolColor(char symbol)
        {
            if (!structure.TryResolve(symbol, out var action, out var block)) return Color.magenta;
            switch (action)
            {
                case StructureCellAction.Carve: return OpenColor;
                case StructureCellAction.Block: return block != null ? block.MinimapColor : Color.magenta;
                case StructureCellAction.OreThisLayer: return OreThisLayerColor;
                case StructureCellAction.OreNextLayer: return OreNextLayerColor;
                case StructureCellAction.Artifact: return BlockColor(BlockTypeId.Artifact);
                case StructureCellAction.PowerUp: return BlockColor(BlockTypeId.TreasureChest);
                default: return KeepColor;
            }
        }

        private Color BlockColor(BlockTypeId id)
        {
            var block = blockTypes.Get((byte)id);
            return block != null ? block.MinimapColor : Color.magenta;
        }

        private string DescribeSymbol(char symbol)
        {
            if (!structure.TryResolve(symbol, out var action, out var block)) return "undefined";
            switch (action)
            {
                case StructureCellAction.Keep: return "Keep generated";
                case StructureCellAction.Carve: return "Open ground";
                case StructureCellAction.Block: return block != null ? block.DisplayName : "Block (unassigned)";
                case StructureCellAction.OreThisLayer: return "Ore (this layer)";
                case StructureCellAction.OreNextLayer: return "Ore (next layer)";
                case StructureCellAction.PowerUp: return "Power-up";
                default: return action.ToString();
            }
        }

        // Saves grid as the structure's layout text (top row first) and refreshes the preview.
        private void WriteLayout(char[,] grid, string undoName)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            var text = new StringBuilder();
            for (int y = 0; y < h; y++)
            {
                if (y > 0) text.Append('\n');
                for (int x = 0; x < w; x++) text.Append(grid[x, y]);
            }

            Undo.SetCurrentGroupName(undoName);
            var so = new SerializedObject(structure);
            so.FindProperty("layout").stringValue = text.ToString();
            so.ApplyModifiedProperties();
            needsRegenerate = true;
        }
    }
}
