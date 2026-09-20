using Economy;
using Events;
using System;
using System.Collections.Generic;
using static UnityEditor.Profiling.HierarchyFrameDataView;

namespace UI.SkillTree
{
    // ISkillTreeSource for the Market's regular (Dollar-purchased) upgrade tree. Reads from
    // GameManager.UpgradeDatabase/UpgradeManager - never reimplements unlock/cost/purchase logic.
    public class MarketSkillTreeSource : ISkillTreeSource
    {
        private UpgradeDatabase database => GameManager.UpgradeDatabase;
        private UpgradeManager manager => UpgradeManager.Instance;

        public int BranchCount => Enum.GetValues(typeof(UpgradeBranch)).Length;
        public SkillTreeType SkillTreeType { get { return SkillTreeType.Upgrades; } }

        public IReadOnlyList<SkillTreeNodeViewModel> BuildViewModels()
        {
            var viewModels = new List<SkillTreeNodeViewModel>();
            var viewModelsByDefinition = new Dictionary<UpgradeDefinition, SkillTreeNodeViewModel>();

            foreach (var def in database.Upgrades)
            {
                var vm = new SkillTreeNodeViewModel
                {
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    Icon = def.Icon,
                    CurrencyIcon = manager.CurrencyIcon,
                    BranchIndex = (int)def.Branch,
                    Level = manager.GetLevelIncludingPrestige(def),
                    MaxLevel = def.MaxLevel,
                    IsUnlocked = manager.IsUnlocked(def),
                    IsMaxed = manager.IsMaxed(def),
                    CanPurchase = manager.CanPurchase(def),
                    UpgradeDefinition = def,
                };
                vm.CostLabel = vm.IsMaxed ? "MAXED" : $"{manager.GetNextCost(def):0}";

                viewModels.Add(vm);
                viewModelsByDefinition[def] = vm;
            }

            LinkPrerequisites(viewModels, viewModelsByDefinition);


            return viewModels;
        }

        private void LinkPrerequisites(List<SkillTreeNodeViewModel> viewModels, Dictionary<UpgradeDefinition, SkillTreeNodeViewModel> viewModelsByDefinition)
        {
            foreach (var vm in viewModels)
            {
                var def = (UpgradeDefinition)vm.UpgradeDefinition;
                if (def.Prerequisite != null && viewModelsByDefinition.TryGetValue(def.Prerequisite as UpgradeDefinition, out var prereqVm))
                {
                    vm.Prerequisite = prereqVm;
                }
            }
        }

        public void RequestPurchase(UpgradeDefinitionBase definition) =>
            GameManager.EventService.Dispatch(new PurchaseRequestedEvent((UpgradeDefinition)definition));

        public SkillTreeNodeDetails GetDetails(UpgradeDefinitionBase definition)
        {
            var def = (UpgradeDefinition)definition;
            return new SkillTreeNodeDetails
            {
                DisplayName = def.DisplayName,
                Description = def.Description,
                Level = manager.GetLevelIncludingPrestige(def),
                MaxLevel = def.MaxLevel,
                CostLabel = manager.IsMaxed(def) ? "MAXED" : $"{manager.GetNextCost(def):0}",
                CanPurchase = manager.CanPurchase(def),
                PurchaseBlockedReason = manager.GetPurchaseBlockedReason(def)
            };
        }
    }
}
