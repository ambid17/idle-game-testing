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
        public SkillTreeType SkillTreeType { get { return SkillTreeType.PrestigeUpgrades; } }

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
                    CurrencyIcon = manager.CurrencyIcon,
                    BranchIndex = (int)def.Branch,
                    Level = manager.GetLevel(def),
                    MaxLevel = def.MaxLevel,
                    IsUnlocked = manager.IsUnlocked(def),
                    IsMaxed = manager.IsMaxed(def),
                    CanPurchase = manager.CanPurchase(def),
                    UpgradeDefinition = def,
                };
                vm.CostLabel = vm.IsMaxed ? "MAXED" : $"{manager.GetNextCost(def):0.##} pts";

                viewModels.Add(vm);
                viewModelsByDefinition[def] = vm;
            }

            foreach (var vm in viewModels)
            {
                var def = (PrestigeUpgradeDefinition)vm.UpgradeDefinition;
                if (def.Prerequisite != null && viewModelsByDefinition.TryGetValue(def.Prerequisite as PrestigeUpgradeDefinition, out var prereqVm))
                {
                    vm.Prerequisite = prereqVm;
                }
            }

            return viewModels;
        }

        public void RequestPurchase(UpgradeDefinitionBase definition) =>
            GameManager.EventService.Dispatch(new PrestigePurchaseRequestedEvent((PrestigeUpgradeDefinition)definition));

        public SkillTreeNodeDetails GetDetails(UpgradeDefinitionBase definition)
        {
            var def = (PrestigeUpgradeDefinition)definition;
            return new SkillTreeNodeDetails
            {
                DisplayName = def.DisplayName,
                Description = def.Description,
                Level = manager.GetLevel(def),
                MaxLevel = def.MaxLevel,
                CostLabel = manager.IsMaxed(def) ? "MAXED" : $"{manager.GetNextCost(def):0.##} pts",
                CanPurchase = manager.CanPurchase(def),
                PurchaseBlockedReason = manager.GetPurchaseBlockedReason(def)
            };
        }
    }
}
