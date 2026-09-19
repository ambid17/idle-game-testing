using System.Collections.Generic;
using UnityEngine;

namespace UI.SkillTree
{
    // Reusable pannable/zoomable node-graph renderer, hosted by both MarketUI (Market upgrades)
    // and MuseumUI (prestige perks) via a small ISkillTreeSource adapter - this class never
    // references UpgradeDefinition/PrestigeUpgradeDefinition directly.
    public class SkillTreePanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private SkillTreePanZoomUI panZoom;
        [SerializeField] private SkillTreeNodeUI nodePrefab;
        [SerializeField] private SkillTreeConnectorUI connectorPrefab;
        [SerializeField] private SkillTreeDetailModalUI detailModal;
        [SerializeField] private SkillTreeLayoutConfig layoutConfig;

        private ISkillTreeSource source;
        private readonly List<SkillTreeNodeUI> nodes = new();
        private readonly List<SkillTreeConnectorUI> connectors = new();

        // Read by the skill tree editor tool so it can bake nodes/connectors using this panel's
        // own prefabs/layout config instead of duplicating them.
        public RectTransform Content => content;
        public SkillTreeNodeUI NodePrefab => nodePrefab;
        public SkillTreeConnectorUI ConnectorPrefab => connectorPrefab;
        public SkillTreeLayoutConfig LayoutConfig => layoutConfig;

        public void Initialize(ISkillTreeSource source)
        {
            this.source = source;
            if (detailModal != null) detailModal.Initialize(source);
        }

        // Called by the owning panel (MarketUI/MuseumUI) whenever the tree view becomes visible -
        // resets any leftover pan/zoom from last time and rebuilds against current state.
        public void Open()
        {
            if (panZoom != null) panZoom.ResetView();
            RefreshAll();
        }

        // Called by the owning panel (MarketUI/MuseumUI) when it closes, so a still-open detail
        // modal doesn't leak its ModalTracker registration or reappear pre-opened next time.
        public void Close() => detailModal?.Close();

        public void RefreshAll()
        {
            if (source == null || content == null) return;

            // Preserved across the rebuild below so a purchase made from the open modal rebinds
            // it to the matching freshly-built view model instead of leaving it on a stale one.
            object previousModalSource = detailModal != null ? detailModal.CurrentSource : null;

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
                var positions = SkillTreeLayout.Compute(layoutNodes, layoutConfig, source.BranchCount);

                ClearInstances();
                AddNodes(viewModels, positions);
                AddConnectors(viewModels, positions);
            }

            RebuildDetailModal(viewModels, previousModalSource);
        }

        private void BindPreplacedNodes(SkillTreeNodeUI[] preplacedNodes, IReadOnlyList<SkillTreeNodeViewModel> viewModels)
        {
            foreach (var nodeUI in preplacedNodes)
            {
                SkillTreeNodeViewModel match = null;
                foreach (var vm in viewModels)
                {
                    if (!ReferenceEquals(vm.Source, nodeUI.BoundAsset)) continue;
                    match = vm;
                    break;
                }

                if (match == null)
                {
                    Debug.LogError($"SkillTreePanelUI: pre-placed node '{nodeUI.name}' has no matching upgrade definition - was it removed from the database or never bound by the editor tool?");
                    continue;
                }

                nodeUI.Bind(match, OnNodeClicked);
            }
        }

        private void AddNodes(IReadOnlyList<SkillTreeNodeViewModel> viewModels, Dictionary<ISkillTreeLayoutNode, Vector2> positions)
        {
            foreach (var vm in viewModels)
            {
                var nodeUI = Instantiate(nodePrefab, content);
                nodeUI.Bind(vm, OnNodeClicked);
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

        private void RebuildDetailModal(IReadOnlyList<SkillTreeNodeViewModel> viewModels, object previousModalSource)
        {
            if(detailModal == null || previousModalSource == null) return;
            foreach (var vm in viewModels)
            {
                if (!ReferenceEquals(vm.Source, previousModalSource)) continue;
                detailModal.Show(vm);
                break;
            }
        }

        private void OnNodeClicked(SkillTreeNodeViewModel vm) => detailModal?.Show(vm);

        private void ClearInstances()
        {
            foreach (var node in nodes) if (node != null) Destroy(node.gameObject);
            nodes.Clear();
            foreach (var connector in connectors) if (connector != null) Destroy(connector.gameObject);
            connectors.Clear();
        }
    }
}
