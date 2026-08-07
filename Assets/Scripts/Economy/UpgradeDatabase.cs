using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    [CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Economy/Upgrade Database")]
    public class UpgradeDatabase : ScriptableObject
    {
        public List<UpgradeDefinition> Upgrades = new();

        private Dictionary<UpgradeEffect, UpgradeDefinition> upgradesByEffect;

        // Assumes at most one definition per UpgradeEffect, true for the current Mining/Economy
        // set - if a branch ever needs two upgrades sharing an effect, key this off Id instead.
        public UpgradeDefinition Find(UpgradeEffect effect)
        {
            if (upgradesByEffect == null) BuildLookup();
            upgradesByEffect.TryGetValue(effect, out var def);
            return def;
        }

        private void BuildLookup()
        {
            upgradesByEffect = new Dictionary<UpgradeEffect, UpgradeDefinition>();
            foreach (var def in Upgrades)
            {
                if (def != null) upgradesByEffect[def.Effect] = def;
            }
        }
    }
}
