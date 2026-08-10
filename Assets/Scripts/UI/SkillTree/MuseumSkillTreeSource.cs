using System;
using System.Collections.Generic;
using Economy;
using Events;

namespace UI.SkillTree
{
    // ISkillTreeSource for the Museum's permanent (Prestige Point-purchased) perk tree. Mirrors
    // MarketSkillTreeSource exactly, reading from GameManager.PrestigeUpgradeDatabase/
    // PrestigeUpgradeManager instead.
    public class MuseumSkillTreeSource : ISkillTreeSource
    {
        private PrestigeUpgradeDatabase database => GameManager.PrestigeUpgradeDatabase;
        private PrestigeUpgradeManager manager => PrestigeUpgradeManager.Instance;

        public int BranchCount => Enum.GetValues(typeof(PrestigeUpgradeBranch)).Length;

        public IReadOnlyList<SkillTreeNodeViewModel> BuildViewModels()
        {
            var viewModels = new List<SkillTreeNodeViewModel>();
            var viewModelsByDefinition = new Dictionary<PrestigeUpgradeDefinition, SkillTreeNodeViewModel>();

            foreach (var def in database.Upgrades)
            {
                if (def == null) continue;

                var vm = new SkillTreeNodeViewModel
                {
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    Icon = def.Icon,
                    BranchIndex = (int)def.Branch,
                    Level = manager.GetLevel(def),
                    MaxLevel = def.MaxLevel,
                    IsUnlocked = manager.IsUnlocked(def),
                    IsMaxed = manager.IsMaxed(def),
                    CanPurchase = manager.CanPurchase(def),
                    Source = def,
                };
                vm.CostLabel = vm.IsMaxed ? "MAXED" : $"{manager.GetNextCost(def):0.##} pts";

                viewModels.Add(vm);
                viewModelsByDefinition[def] = vm;
            }

            foreach (var vm in viewModels)
            {
                var def = (PrestigeUpgradeDefinition)vm.Source;
                if (def.Prerequisite != null && viewModelsByDefinition.TryGetValue(def.Prerequisite, out var prereqVm))
                {
                    vm.Prerequisite = prereqVm;
                }
            }

            return viewModels;
        }

        public void RequestPurchase(SkillTreeNodeViewModel node) =>
            GameManager.EventService.Dispatch(new PrestigePurchaseRequestedEvent((PrestigeUpgradeDefinition)node.Source));
    }
}
