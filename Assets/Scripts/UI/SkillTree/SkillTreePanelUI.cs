using System.Collections.Generic;
using System.Linq;
using Economy;
using Settings;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.SkillTree
{
    // Reusable pannable/zoomable node-graph renderer, hosted by both MarketUI (Market upgrades)
    // and MuseumUI (prestige perks) via a small ISkillTreeSource adapter - this class never
    // references UpgradeDefinition/PrestigeUpgradeDefinition directly, only their shared
    // UpgradeDefinitionBase.
    public class SkillTreePanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private SkillTreePanZoomUI panZoom;
        [SerializeField] private SkillTreeNodeUI nodePrefab;
        [SerializeField] private SkillTreeConnectorUI connectorPrefab;
        [SerializeField] private SkillTreeTooltipUI tooltip;
        [SerializeField] private SkillTreeLayoutConfig layoutConfig;

        private ISkillTreeSource source;
        private readonly List<SkillTreeNodeUI> nodes = new();
        private readonly List<SkillTreeConnectorUI> connectors = new();
        private SkillTreeNodeUI hoveredNode;

        // The upgrade the player last hovered/selected, kept for the rest of the game session so
        // the tree re-opens on it (view centered, and selected on a controller). Held as the
        // definition rather than the node, since dynamically built nodes are recreated on refresh.
        private UpgradeDefinitionBase lastFocusedUpgrade;
        private bool isOpen;
        // Set when the tree becomes visible (opened, or its tab switched back to); handled in
        // Update, once the nodes are active. Until then selections aren't remembered - they're
        // GamepadFocus picking its default node, not the player's choice.
        private bool pendingFocusRestore;

        // Read by the skill tree editor tool so it can bake nodes/connectors using this panel's
        // own prefabs/layout config instead of duplicating them.
        public RectTransform Content => content;
        public SkillTreeNodeUI NodePrefab => nodePrefab;
        public SkillTreeConnectorUI ConnectorPrefab => connectorPrefab;
        public SkillTreeLayoutConfig LayoutConfig => layoutConfig;

        public void Initialize(ISkillTreeSource source)
        {
            this.source = source;
            tooltip.Initialize(source);
        }

        private void Start()
        {
            CheckNullRefs();
        }

        private void CheckNullRefs()
        {
            if (content == null) Debug.LogError($"{nameof(SkillTreePanelUI)}.{nameof(content)} is not assigned in the inspector.");
            if (panZoom == null) Debug.LogError($"{nameof(SkillTreePanelUI)}.{nameof(panZoom)} is not assigned in the inspector.");
            if (nodePrefab == null) Debug.LogError($"{nameof(SkillTreePanelUI)}.{nameof(nodePrefab)} is not assigned in the inspector.");
            if (connectorPrefab == null) Debug.LogError($"{nameof(SkillTreePanelUI)}.{nameof(connectorPrefab)} is not assigned in the inspector.");
            if (tooltip == null) Debug.LogError($"{nameof(SkillTreePanelUI)}.{nameof(tooltip)} is not assigned in the inspector.");
            if (layoutConfig == null) Debug.LogError($"{nameof(SkillTreePanelUI)}.{nameof(layoutConfig)} is not assigned in the inspector.");
        }

        // Called by the owning panel (MarketUI/MuseumUI) whenever the tree view becomes visible -
        // resets any leftover pan/zoom from last time and rebuilds against current state.
        public void Open()
        {
            panZoom.ResetView();
            tooltip?.Hide();
            hoveredNode = null;
            RefreshAll();
            isOpen = true;
            pendingFocusRestore = true;
        }

        // Called by the owning panel (MarketUI/MuseumUI) when it closes, so a still-visible
        // tooltip doesn't linger on screen or reappear pre-shown next time.
        public void Close()
        {
            tooltip?.Hide();
            hoveredNode = null;
            isOpen = false;
            pendingFocusRestore = false;
        }

        // Covers the tree living in a tab (Museum): coming back to that tab restores the focus too.
        private void OnEnable()
        {
            if (isOpen) pendingFocusRestore = true;
        }

        private void Update()
        {
            if (!pendingFocusRestore) return;

            var node = FindNode(lastFocusedUpgrade != null ? lastFocusedUpgrade : source?.DefaultFocus);
            // Not visible yet (the Museum can open on its other tab) - wait until it is.
            if (node != null && !node.isActiveAndEnabled) return;

            pendingFocusRestore = false;
            if (node == null) return;

            if (GameManager.KeybindService.CurrentScheme == InputScheme.Gamepad && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(node.gameObject);
                // No select event fires if GamepadFocus had already landed on this node.
                OnNodeHoverEnter(node);
            }
            panZoom.CenterOn(node.GetComponent<RectTransform>(), instant: true);
        }

        private SkillTreeNodeUI FindNode(UpgradeDefinitionBase definition)
        {
            if (definition == null) return null;

            // Dynamically built nodes are tracked in the list (content may still hold last
            // refresh's destroyed ones this frame); a baked tree only exists as content's children.
            IEnumerable<SkillTreeNodeUI> candidates = nodes.Count > 0 ? nodes : content.GetComponentsInChildren<SkillTreeNodeUI>(true);
            return candidates.FirstOrDefault(node => node.UpgradeDefinition == definition);
        }

        public void RefreshAll()
        {
            if (source == null) return;

            var viewModels = source.BuildViewModels();

            // A skill tree built by the editor tool already has its SkillTreeNodeUI/
            // SkillTreeConnectorUI children baked into the scene at fixed positions - in that
            // case just rebind the existing nodes to fresh view models instead of destroying and
            // re-instantiating everything (which used to happen on every purchase/dollar-changed
            // refresh). Trees with no baked nodes yet (e.g. Museum) fall back to the original
            // dynamic build so they keep working unchanged.
            var preplacedNodes = content.GetComponentsInChildren<SkillTreeNodeUI>(true);
            if (preplacedNodes.Length > 0)
            {
                BindPreplacedNodes(preplacedNodes, viewModels);
            }
            else if (nodePrefab != null)
            {
                var layoutNodes = new List<ISkillTreeLayoutNode>(viewModels.Count);
                foreach (var vm in viewModels) layoutNodes.Add(vm);
                var positions = SkillTreeLayout.Compute(layoutNodes, layoutConfig);

                ClearInstances();
                AddNodes(viewModels, positions);
                AddConnectors(viewModels, positions);
            }

            // The visible tooltip (if any) holds the definition itself, not a view model, so it
            // just re-queries the source for fresh data - no need to look anything up here.
            tooltip.Refresh();
        }

        private void BindPreplacedNodes(SkillTreeNodeUI[] preplacedNodes, IReadOnlyList<SkillTreeNodeViewModel> viewModels)
        {
            foreach (var nodeUI in preplacedNodes)
            {
                SkillTreeNodeViewModel match = viewModels.FirstOrDefault(vm => vm.UpgradeDefinition.DisplayName == nodeUI.UpgradeDefinition.DisplayName);

                if (match == null)
                {
                    Debug.LogError($"SkillTreePanelUI: pre-placed node '{nodeUI.name}' has no matching upgrade definition - was it removed from the database or never bound by the editor tool?");
                    continue;
                }

                nodeUI.Bind(match, OnNodePurchaseClicked, OnNodeHoverEnter, OnNodeHoverExit, OnNodeSelected, panZoom.OnDrag);
            }
        }

        private void AddNodes(IReadOnlyList<SkillTreeNodeViewModel> viewModels, Dictionary<ISkillTreeLayoutNode, Vector2> positions)
        {
            foreach (var vm in viewModels)
            {
                var nodeUI = Instantiate(nodePrefab, content);
                nodeUI.Bind(vm, OnNodePurchaseClicked, OnNodeHoverEnter, OnNodeHoverExit, OnNodeSelected, panZoom.OnDrag);
                if (positions.TryGetValue(vm, out var position))
                {
                    nodeUI.GetComponent<RectTransform>().anchoredPosition = position;
                }
                nodes.Add(nodeUI);
            }
        }

        private void AddConnectors(IReadOnlyList<SkillTreeNodeViewModel> viewModels, Dictionary<ISkillTreeLayoutNode, Vector2> positions)
        {
            foreach (var vm in viewModels)
            {
                var prereqVm = vm.Prerequisite as SkillTreeNodeViewModel;
                if (prereqVm == null) continue;
                if (!positions.TryGetValue(vm, out var childPos)) continue;
                if (!positions.TryGetValue(prereqVm, out var parentPos)) continue;

                var connector = Instantiate(connectorPrefab, content);
                connector.transform.SetAsFirstSibling();
                connector.SetEndpoints(parentPos, childPos);
                connectors.Add(connector);
            }
        }

        private void OnNodePurchaseClicked(SkillTreeNodeViewModel vm) => source?.RequestPurchase(vm.UpgradeDefinition);

        private void OnNodeHoverEnter(SkillTreeNodeUI node)
        {
            hoveredNode = node;
            if (isOpen && !pendingFocusRestore) lastFocusedUpgrade = node.UpgradeDefinition;
            tooltip?.Show(node.UpgradeDefinition, node.GetComponent<RectTransform>());
        }

        private void OnNodeSelected(SkillTreeNodeUI node)
        {
            OnNodeHoverEnter(node);
            panZoom.CenterOn(node.GetComponent<RectTransform>());
        }

        private void OnNodeHoverExit(SkillTreeNodeUI node)
        {
            if (hoveredNode != node) return;
            hoveredNode = null;
            tooltip?.Hide();
        }

        private void ClearInstances()
        {
            foreach (var node in nodes) if (node != null) Destroy(node.gameObject);
            nodes.Clear();
            foreach (var connector in connectors) if (connector != null) Destroy(connector.gameObject);
            connectors.Clear();
        }
    }
}
