using System.Linq;
using Automation;
using UnityEditor;
using UnityEngine;

namespace EditorTools.Automation
{
    // Adds a live inventory readout on top of the default Inspector (which already shows the
    // serialized state/path/target fields) so a MiningAutomaton's behavior can be observed in
    // Play Mode without temporary Debug.Log calls. Scene-view path/target visualization lives in
    // MiningAutomaton.OnDrawGizmosSelected.
    [CustomEditor(typeof(MiningAutomaton))]
    public class MiningAutomatonEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var automaton = (MiningAutomaton)target;

            DrawDefaultInspector();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live state and inventory.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live Inspection", EditorStyles.boldLabel);

            var inventory = automaton.Inventory;
            if (inventory == null)
            {
                EditorGUILayout.HelpBox("OreInventory not yet initialized.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Weight", $"{inventory.CurrentWeight:0.0} / {inventory.MaxWeight:0.0}{(inventory.IsFull ? "  (FULL)" : "")}");

            float currentFuel = automaton.FuelMax - automaton.FuelMissing;
            EditorGUILayout.LabelField("Fuel", $"{currentFuel:0.0} / {automaton.FuelMax:0.0}{(currentFuel <= 0f ? "  (STALLED)" : "")}");

            var blockTypeDatabase = GameManager.BlockTypeDatabase;
            foreach (var kvp in inventory.OreCounts.Where(kvp => kvp.Value > 0))
            {
                var blockType = blockTypeDatabase.Get((byte)kvp.Key);
                string label = blockType != null && !string.IsNullOrEmpty(blockType.DisplayName) ? blockType.DisplayName : kvp.Key.ToString();
                EditorGUILayout.LabelField(label, kvp.Value.ToString());
            }

            // Keeps the panel refreshing every frame while selected in Play Mode - Unity doesn't
            // auto-repaint custom inspectors when plain (non-serialized-property) fields change.
            Repaint();
        }
    }
}
