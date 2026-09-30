using System.Collections.Generic;
using MapGeneration;
using RunModifiers;
using UnityEditor;
using UnityEngine;

namespace MapGenerationEditor
{
    // Renders a generated layer as a minimap-coloured grid, with an optional run modifier and/or
    // extra MapFeatureDefinition applied on top - for designing StructureDefinitions and tuning
    // features/modifiers without entering Play Mode. Cells that differ from the un-modified
    // layer can be outlined so a feature's footprint stands out.
    public class MapFeaturePreviewWindow : EditorWindow
    {
        private LayerConfigProvider provider;
        private BlockTypeDatabase blockTypes;
        private RunModifierDefinition modifier;
        private MapFeatureDefinition extraFeature;
        private int seed = 12345;
        private int layer = 3;
        private int gridWidth = 30;
        private int targetLayer = 3;
        private BlockTypeId oreA = BlockTypeId.IronOre;
        private BlockTypeId oreB = BlockTypeId.Coal;
        private bool highlightChanges = true;
        private int cellPixels = 12;

        private Texture2D texture;
        private Vector2 scroll;
        private string summary = "";

        [MenuItem("Tools/Map Generation/Feature Preview")]
        private static void Open() => GetWindow<MapFeaturePreviewWindow>("Map Feature Preview");

        private void OnEnable()
        {
            provider ??= FindAsset<LayerConfigProvider>();
            blockTypes ??= FindAsset<BlockTypeDatabase>();
        }

        private static T FindAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).FullName}");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            provider = (LayerConfigProvider)EditorGUILayout.ObjectField("Layer Provider", provider, typeof(LayerConfigProvider), false);
            blockTypes = (BlockTypeDatabase)EditorGUILayout.ObjectField("Block Types", blockTypes, typeof(BlockTypeDatabase), false);
            modifier = (RunModifierDefinition)EditorGUILayout.ObjectField("Run Modifier", modifier, typeof(RunModifierDefinition), false);
            extraFeature = (MapFeatureDefinition)EditorGUILayout.ObjectField("Extra Feature", extraFeature, typeof(MapFeatureDefinition), false);

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
            highlightChanges = EditorGUILayout.Toggle("Outline Changes", highlightChanges);
            cellPixels = EditorGUILayout.IntSlider("Cell Size", cellPixels, 4, 24);
            bool changed = EditorGUI.EndChangeCheck();

            if (GUILayout.Button("Generate") || changed || texture == null) Regenerate();

            EditorGUILayout.HelpBox(summary, MessageType.None);
            if (texture == null) return;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var rect = GUILayoutUtility.GetRect(texture.width * cellPixels, texture.height * cellPixels, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            EditorGUILayout.EndScrollView();
        }

        private void Regenerate()
        {
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

            var chunk = ChunkGenerator.Generate(seed, layer, gridWidth, config, config.LayerHeight, 1f, 0f, 0f, next, tweaks);
            var baseline = ChunkGenerator.Generate(seed, layer, gridWidth, config, config.LayerHeight, 1f, 0f, 0f, next);

            if (texture != null) DestroyImmediate(texture);
            texture = new Texture2D(chunk.Width, chunk.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

            int changedCells = 0;
            var counts = new Dictionary<string, int>();
            for (int y = 0; y < chunk.Height; y++)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    var cell = chunk.Cells[chunk.Index(x, y)];
                    var before = baseline.Cells[baseline.Index(x, y)];
                    bool differs = cell.BlockTypeId != before.BlockTypeId || cell.Mined != before.Mined;
                    if (differs) changedCells++;

                    var block = blockTypes.Get(cell.BlockTypeId);
                    Color color = cell.Mined ? new Color(0.05f, 0.05f, 0.07f) : block != null ? block.MinimapColor : Color.magenta;
                    if (highlightChanges && differs) color = Color.Lerp(color, Color.white, 0.35f);

                    // Texture rows run bottom-up; chunk rows run top-down.
                    texture.SetPixel(x, chunk.Height - 1 - y, color);

                    string key = cell.Mined ? "(open)" : block != null ? block.DisplayName : $"id {cell.BlockTypeId}";
                    counts.TryGetValue(key, out int count);
                    counts[key] = count + 1;
                }
            }
            texture.Apply();

            var lines = new List<string> { $"{chunk.Width}x{chunk.Height}, {changedCells} cells differ from the un-modified layer." };
            foreach (var kvp in counts) lines.Add($"{kvp.Key}: {kvp.Value}");
            summary = string.Join("   ", lines);
        }
    }
}
