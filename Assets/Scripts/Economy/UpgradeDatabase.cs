using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    [CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Economy/Upgrade Database")]
    public class UpgradeDatabase : ScriptableObject
    {
        public List<UpgradeDefinition> Upgrades = new();

        private Dictionary<UpgradeEffect, UpgradeDefinition> byEffect;

        // Assumes at most one definition per UpgradeEffect, true for the current Mining/Economy
        // set - if a branch ever needs two upgrades sharing an effect, key this off Id instead.
        public UpgradeDefinition Find(UpgradeEffect effect)
        {
            if (byEffect == null) BuildLookup();
            byEffect.TryGetValue(effect, out var def);
            return def;
        }

        private void BuildLookup()
        {
            byEffect = new Dictionary<UpgradeEffect, UpgradeDefinition>();
            foreach (var def in Upgrades)
            {
                if (def != null) byEffect[def.Effect] = def;
            }
        }

        private void OnEnable() => byEffect = null;
    }
}
