using System.Collections.Generic;
using Automation;
using Economy;
using Events;
using UnityEngine;

namespace UI
{
    // Control Center "miner automaton dashboard" tab: automaton upgrade nodes, reusing
    // UpgradeNodeUI/PurchaseRequestedEvent exactly as MarketUI does. MarketUI's OnEnable listener
    // for PurchaseRequestedEvent is already global (not filtered to the Market building), so it
    // processes purchases raised from here too - this script deliberately does not add its own
    // duplicate listener, which would double-call UpgradeManager.TryPurchase per click.
    public class MinerDashboardUI : MonoBehaviour
    {
        private static readonly UpgradeEffect[] Effects =
        {
            UpgradeEffect.AutomatonCount,
            UpgradeEffect.AutomatonMiningSpeed,
            UpgradeEffect.AutomatonMoveSpeed,
            UpgradeEffect.AutomatonMiningRadius,
            UpgradeEffect.AutomatonInventoryCapacity
        };

        [SerializeField] private Transform nodeContainer;
        [SerializeField] private UpgradeNodeUI nodePrefab;
        [SerializeField] private LineGraphUI earningsGraph;

        private readonly List<UpgradeNodeUI> nodes = new();
        private UpgradeDatabase upgradeDatabase => GameManager.UpgradeDatabase;

        private void Start() => BuildNodes();

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<DollarsChangedEvent>(RefreshNodes);
            GameManager.EventService.Add<OreDepositedByAutomationEvent>(OnOreDeposited);
            RefreshNodes();
            RefreshGraph();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<DollarsChangedEvent>(RefreshNodes);
            GameManager.EventService.Remove<OreDepositedByAutomationEvent>(OnOreDeposited);
        }

        private void BuildNodes()
        {
            if (nodePrefab == null || nodeContainer == null)
            {
                Debug.LogError("MinerDashboardUI: Missing nodePrefab or nodeContainer.");
                return;
            }

            foreach (var effect in Effects)
            {
                var def = upgradeDatabase.Find(effect);
                if (def == null) continue;

                var node = Instantiate(nodePrefab, nodeContainer);
                node.Bind(def);
                nodes.Add(node);
            }
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            RefreshNodes();
            RefreshGraph(); // AutomatonCount changing shifts which series indices are shown
        }

        private void RefreshNodes()
        {
            foreach (var node in nodes) node.Refresh(UpgradeManager.Instance);
        }

        private void OnOreDeposited(OreDepositedByAutomationEvent evt) => RefreshGraph();

        private void RefreshGraph()
        {
            if (earningsGraph == null) return;

            int automatonCount = UpgradeManager.Instance.AutomatonCount;
            var seriesList = new List<IReadOnlyList<float>>();
            for (int i = 1; i <= automatonCount; i++)
            {
                seriesList.Add(AutomatonEarningsTracker.Instance.RecentDollarsPerMinute(i));
            }
            earningsGraph.SetSeries(seriesList);
        }
    }
}
