using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    [CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Economy/Upgrade Database")]
    public class UpgradeDatabase : ScriptableObject
    {
        public List<UpgradeDefinition> Upgrades = new();

        private Dictionary<UpgradeEffect, UpgradeDefinition> upgradesByEffect;
        private Dictionary<string, UpgradeDefinition> upgradesById;

        // Assumes at most one definition per UpgradeEffect, true for the current Mining/Economy
        // set - if a branch ever needs two upgrades sharing an effect, key this off Id instead.
        public UpgradeDefinition Find(UpgradeEffect effect)
        {
            if (upgradesByEffect == null) BuildLookup();
            upgradesByEffect.TryGetValue(effect, out var def);
            return def;
        }

        // Used by UpgradeManager.SetLevel to restore SaveService's Id-keyed save data onto the
        // matching definition.
        public UpgradeDefinition Find(string id)
        {
            if (upgradesById == null) BuildLookup();
            upgradesById.TryGetValue(id, out var def);
            return def;
        }

        private void BuildLookup()
        {
            upgradesByEffect = new Dictionary<UpgradeEffect, UpgradeDefinition>();
            upgradesById = new Dictionary<string, UpgradeDefinition>();
            foreach (var def in Upgrades)
            {
                if (def == null) continue;
                upgradesByEffect[def.Effect] = def;
                upgradesById[def.Id] = def;
            }
        }
    }
}
