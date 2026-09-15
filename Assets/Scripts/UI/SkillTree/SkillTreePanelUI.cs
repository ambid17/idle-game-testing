using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.Profiling.HierarchyFrameDataView;

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

        public void RefreshAll()
        {
            if (source == null || content == null || nodePrefab == null) return;

            // Preserved across the rebuild below so a purchase made from the open modal rebinds
            // it to the matching freshly-built view model instead of leaving it on a stale one.
            object previousModalSource = detailModal != null ? detailModal.CurrentSource : null;

            var viewModels = source.BuildViewModels();
            var layoutNodes = new List<ISkillTreeLayoutNode>(viewModels.Count);
            foreach (var vm in viewModels) layoutNodes.Add(vm);
            var positions = SkillTreeLayout.Compute(layoutNodes, layoutConfig, source.BranchCount);

            ClearInstances();
            AddNodes(viewModels, positions);
            AddConnectors(viewModels, positions);
            RebuildDetailModal(viewModels, previousModalSource);
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
