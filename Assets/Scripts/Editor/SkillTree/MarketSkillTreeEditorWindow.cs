using System;
using System.Collections.Generic;
using System.Linq;
using Economy;
using UI.SkillTree;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EditorTools.SkillTree
{
    // Bakes the Market's skill tree (nodes + connectors) into the scene at edit time, using the
    // same SkillTreeLayout radial algorithm SkillTreePanelUI used to compute positions at
    // runtime. Once baked, SkillTreePanelUI.RefreshAll finds the pre-placed SkillTreeNodeUI
    // children under Content and only rebinds their view models on refresh instead of destroying
    // and re-instantiating everything on every purchase/dollar-changed event.
    public class MarketSkillTreeEditorWindow : EditorWindow
    {
        [SerializeField] private SkillTreePanelUI targetPanel;
        [SerializeField] private UpgradeDatabase database;

        [MenuItem("Tools/Skill Tree/Build Market Tree")]
        private static void Open() => GetWindow<MarketSkillTreeEditorWindow>("Market Skill Tree Builder");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Bakes SkillTreeNodeUI/SkillTreeConnectorUI GameObjects into the target panel's " +
                "Content, positioned via the same radial layout used at runtime. Any existing " +
                "baked nodes/connectors under Content are destroyed and rebuilt.",
                MessageType.Info);

            targetPanel = (SkillTreePanelUI)EditorGUILayout.ObjectField("Target Panel", targetPanel, typeof(SkillTreePanelUI), true);
            database = (UpgradeDatabase)EditorGUILayout.ObjectField("Upgrade Database", database, typeof(UpgradeDatabase), false);

            using (new EditorGUI.DisabledScope(targetPanel == null || database == null))
            {
                if (GUILayout.Button("Build / Rebuild Nodes"))
                {
                    BuildTree(targetPanel, database, confirmOverwrite: true);
                }
            }
        }

        // Does the actual bake; a static entry point so it can run both from this window's
        // button and from a one-off editor script/console call. Set confirmOverwrite to false to
        // skip the confirmation dialog (e.g. when driving this non-interactively).
        public static void BuildTree(SkillTreePanelUI targetPanel, UpgradeDatabase database, bool confirmOverwrite = true)
        {
            if (targetPanel == null || database == null)
            {
                Debug.LogError("MarketSkillTreeEditorWindow: targetPanel and database are required.");
                return;
            }

            if (targetPanel.Content == null || targetPanel.NodePrefab == null || targetPanel.ConnectorPrefab == null)
            {
                Debug.LogError("MarketSkillTreeEditorWindow: target panel is missing Content/NodePrefab/ConnectorPrefab - assign them on the SkillTreePanelUI first.");
                return;
            }

            var content = targetPanel.Content;
            int existingCount = content.GetComponentsInChildren<SkillTreeNodeUI>(true).Length
                + content.GetComponentsInChildren<SkillTreeConnectorUI>(true).Length;
            if (existingCount > 0 && confirmOverwrite && !EditorUtility.DisplayDialog(
                    "Rebuild Skill Tree",
                    $"This will destroy {existingCount} existing baked node/connector object(s) under '{content.name}' and rebuild them. Continue?",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            var definitions = database.Upgrades.Where(d => d != null).ToList();
            if (definitions.Count == 0)
            {
                Debug.LogError("MarketSkillTreeEditorWindow: Upgrade Database has no upgrades assigned.");
                return;
            }

            var layoutNodes = new List<ISkillTreeLayoutNode>(definitions.Count);
            var layoutNodeByDefinition = new Dictionary<UpgradeDefinition, UpgradeLayoutNode>();
            foreach (var def in definitions)
            {
                var node = new UpgradeLayoutNode(def);
                layoutNodeByDefinition[def] = node;
                layoutNodes.Add(node);
            }
            foreach (var def in definitions)
            {
                if (def.Prerequisite != null && layoutNodeByDefinition.TryGetValue(def.Prerequisite as UpgradeDefinition, out var prereqNode))
                {
                    layoutNodeByDefinition[def].Prerequisite = prereqNode;
                }
            }

            int branchCount = Enum.GetValues(typeof(UpgradeBranch)).Length;
            var positions = SkillTreeLayout.Compute(layoutNodes, targetPanel.LayoutConfig, branchCount);

            ClearExisting(content);

            var nodeUIByDefinition = new Dictionary<UpgradeDefinition, SkillTreeNodeUI>();
            foreach (var def in definitions)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(targetPanel.NodePrefab.gameObject, content);
                Undo.RegisterCreatedObjectUndo(instance, "Build Market Skill Tree Node");
                instance.name = $"SkillTreeNode_{def.DisplayName}";

                if (positions.TryGetValue(layoutNodeByDefinition[def], out var pos))
                {
                    instance.GetComponent<RectTransform>().anchoredPosition = pos;
                }

                var nodeUI = instance.GetComponent<SkillTreeNodeUI>();
                var serializedNode = new SerializedObject(nodeUI);
                serializedNode.FindProperty("upgradeDefinition").objectReferenceValue = def;
                serializedNode.ApplyModifiedProperties();

                nodeUIByDefinition[def] = nodeUI;
            }

            int connectorCount = 0;
            foreach (var def in definitions)
            {
                if (def.Prerequisite == null || !nodeUIByDefinition.ContainsKey(def.Prerequisite as UpgradeDefinition)) continue;
                if (!positions.TryGetValue(layoutNodeByDefinition[def], out var childPos)) continue;
                if (!positions.TryGetValue(layoutNodeByDefinition[def.Prerequisite as UpgradeDefinition], out var parentPos)) continue;

                var connectorInstance = (GameObject)PrefabUtility.InstantiatePrefab(targetPanel.ConnectorPrefab.gameObject, content);
                Undo.RegisterCreatedObjectUndo(connectorInstance, "Build Market Skill Tree Connector");
                connectorInstance.name = $"SkillTreeConnector_{def.Prerequisite.DisplayName}_to_{def.DisplayName}";
                connectorInstance.transform.SetAsFirstSibling();
                connectorInstance.GetComponent<SkillTreeConnectorUI>().SetEndpoints(parentPos, childPos);
                connectorCount++;
            }

            EditorUtility.SetDirty(content);
            EditorSceneManager.MarkSceneDirty(content.gameObject.scene);

            Debug.Log($"MarketSkillTreeEditorWindow: baked {nodeUIByDefinition.Count} node(s) and {connectorCount} connector(s) under '{content.name}'.");
        }

        private static void ClearExisting(RectTransform content)
        {
            var toDestroy = new List<GameObject>();
            foreach (var node in content.GetComponentsInChildren<SkillTreeNodeUI>(true)) toDestroy.Add(node.gameObject);
            foreach (var connector in content.GetComponentsInChildren<SkillTreeConnectorUI>(true)) toDestroy.Add(connector.gameObject);
            foreach (var go in toDestroy) Undo.DestroyObjectImmediate(go);
        }

        // Wraps an UpgradeDefinition just enough to feed the shared SkillTreeLayout algorithm -
        // mirrors what MarketSkillTreeSource.BuildViewModels does at runtime, but reads only
        // static asset data (Branch/Prerequisite) so it works in edit mode without a running
        // UpgradeManager.
        private class UpgradeLayoutNode : ISkillTreeLayoutNode
        {
            private readonly UpgradeDefinition definition;
            public UpgradeLayoutNode(UpgradeDefinition definition) => this.definition = definition;
            public int BranchIndex => (int)definition.Branch;
            public ISkillTreeLayoutNode Prerequisite { get; set; }
        }
    }
}
