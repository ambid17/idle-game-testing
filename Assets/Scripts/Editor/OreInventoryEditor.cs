using System.Linq;
using Economy;
using UnityEditor;
using UnityEngine;

// Read-only Play Mode view of an OreInventory's contents (its counts live in a private
// Dictionary, which Unity's Inspector can't show) - works on any owner: player, automaton, drone.
[CustomEditor(typeof(OreInventory))]
public class OreInventoryEditor : Editor
{
    private bool showEmpty;

    // Contents change every frame while something is mining/draining.
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Contents are shown in Play Mode.", MessageType.Info);
            return;
        }

        var inventory = (OreInventory)target;
        var database = GameManager.BlockTypeDatabase;

        float currentWeight = inventory.CurrentWeight;
        float maxWeight = inventory.MaxWeight;
        var barRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        EditorGUI.ProgressBar(barRect, maxWeight > 0f ? Mathf.Clamp01(currentWeight / maxWeight) : 0f, $"Weight {currentWeight:0.##} / {maxWeight:0.##}");

        showEmpty = EditorGUILayout.Toggle("Show Empty", showEmpty);

        var rows = inventory.OreCounts
            .Where(kvp => showEmpty || kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        if (rows.Count == 0)
        {
            EditorGUILayout.LabelField("Empty", EditorStyles.miniLabel);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Ore", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Count", EditorStyles.boldLabel, GUILayout.Width(60f));
            EditorGUILayout.LabelField("Weight", EditorStyles.boldLabel, GUILayout.Width(60f));
        }

        foreach (var kvp in rows)
        {
            var blockType = database.Get((byte)kvp.Key);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(blockType != null ? blockType.DisplayName : kvp.Key.ToString());
                EditorGUILayout.LabelField(kvp.Value.ToString(), GUILayout.Width(60f));
                EditorGUILayout.LabelField(blockType != null ? (blockType.Weight * kvp.Value).ToString("0.##") : "-", GUILayout.Width(60f));
            }
        }
    }
}
