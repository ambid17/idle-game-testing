using System.Collections.Generic;
using Automation;
using Economy;
using Events;
using UnityEngine;

namespace UI
{
    // Control Center "miner automaton dashboard" tab: earnings graph only. Automaton upgrades are
    // purchased from MarketUI's Automation tab (UpgradeDatabase is shared, so purchases made there
    // apply here too) - no duplicate purchase UI in the Control Center.
    public class MinerDashboardUI : MonoBehaviour
    {
        [SerializeField] private LineGraphUI earningsGraph;

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<OreDepositedByAutomationEvent>(OnOreDeposited);
            RefreshGraph();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<OreDepositedByAutomationEvent>(OnOreDeposited);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => RefreshGraph(); // AutomatonCount changing shifts which series indices are shown

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
