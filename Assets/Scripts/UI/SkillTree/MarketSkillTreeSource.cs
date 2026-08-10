using System;
using System.Collections.Generic;
using Economy;
using Events;

namespace UI.SkillTree
{
    // ISkillTreeSource for the Market's regular (Dollar-purchased) upgrade tree. Reads from
    // GameManager.UpgradeDatabase/UpgradeManager - never reimplements unlock/cost/purchase logic.
    public class MarketSkillTreeSource : ISkillTreeSource
    {
        private UpgradeDatabase database => GameManager.UpgradeDatabase;
        private UpgradeManager manager => UpgradeManager.Instance;

        public int BranchCount => Enum.GetValues(typeof(UpgradeBranch)).Length;

        public IReadOnlyList<SkillTreeNodeViewModel> BuildViewModels()
        {
            var viewModels = new List<SkillTreeNodeViewModel>();
            var viewModelsByDefinition = new Dictionary<UpgradeDefinition, SkillTreeNodeViewModel>();

            foreach (var def in database.Upgrades)
            {
                if (def == null) continue;

                var vm = new SkillTreeNodeViewModel
                {
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    Icon = def.Icon,
                    BranchIndex = (int)def.Branch,
                    Level = manager.GetLevelIncludingPrestige(def),
                    MaxLevel = def.MaxLevel,
                    IsUnlocked = manager.IsUnlocked(def),
                    IsMaxed = manager.IsMaxed(def),
                    CanPurchase = manager.CanPurchase(def),
                    Source = def,
                };
                vm.CostLabel = vm.IsMaxed ? "MAXED" : $"${manager.GetNextCost(def):0.##}";

                viewModels.Add(vm);
                viewModelsByDefinition[def] = vm;
            }

            foreach (var vm in viewModels)
            {
                var def = (UpgradeDefinition)vm.Source;
                if (def.Prerequisite != null && viewModelsByDefinition.TryGetValue(def.Prerequisite, out var prereqVm))
                {
                    vm.Prerequisite = prereqVm;
                }
            }

            return viewModels;
        }

        public void RequestPurchase(SkillTreeNodeViewModel node) =>
            GameManager.EventService.Dispatch(new PurchaseRequestedEvent((UpgradeDefinition)node.Source));
    }
}
