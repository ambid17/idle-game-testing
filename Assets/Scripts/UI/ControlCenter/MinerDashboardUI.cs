using System.Collections.Generic;
using Automation;
using Events;
using MapGeneration;
using UnityEngine;

namespace UI
{
    // Control Center "miner automaton dashboard" tab: shows a live ore/min table sourced from
    // IdleEarningsTracker (fed by both Mining Automatons and Storage Drones via
    // AutomationDepositService) - deliberately source-agnostic, it doesn't care which entity
    // deposited the ore, only how much of each type is coming in. Automaton upgrades are
    // purchased from MarketUI's Automation tab (UpgradeDatabase is shared, so purchases made there
    // apply here too) - no duplicate purchase UI in the Control Center.
    public class MinerDashboardUI : MonoBehaviour
    {
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;

        private readonly Dictionary<BlockTypeId, OreRowUI> rows = new();
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start() => BuildRows();

        private void OnEnable()
        {
            CheckNullRefs();
            GameManager.EventService.Add<OreDepositedByAutomationEvent>(OnOreDeposited);
            Refresh();
        }

        private void CheckNullRefs()
        {
            if (rowContainer == null) Debug.LogError($"{nameof(MinerDashboardUI)} is missing its rowContainer reference.");
            if (rowPrefab == null) Debug.LogError($"{nameof(MinerDashboardUI)} is missing its rowPrefab reference.");
        }

        private void OnDisable() => GameManager.EventService.Remove<OreDepositedByAutomationEvent>(OnOreDeposited);

        private void BuildRows()
        {
            foreach (var blockType in blockTypeDatabase.BlockTypes)
            {
                if (blockType.Category != BlockCategory.Ore) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                string displayName = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
                row.Bind(blockType);
                row.gameObject.name = $"Row_{blockType.name}";
                rows[blockType.Id] = row;
            }
        }

        private void OnOreDeposited(OreDepositedByAutomationEvent evt) => Refresh();

        private void Refresh()
        {
            var averages = IdleEarningsTracker.Instance.AveragePerMinute;
            foreach (var kvp in rows)
            {
                averages.TryGetValue(kvp.Key, out var rate);
                kvp.Value.SetRate(rate);
            }
        }
    }
}
