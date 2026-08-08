using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    [CreateAssetMenu(fileName = "PrestigeUpgradeDatabase", menuName = "Economy/Prestige Upgrade Database")]
    public class PrestigeUpgradeDatabase : ScriptableObject
    {
        public List<PrestigeUpgradeDefinition> Upgrades = new();

        private Dictionary<PrestigeUpgradeEffect, PrestigeUpgradeDefinition> upgradesByEffect;

        // Assumes at most one definition per PrestigeUpgradeEffect, matching UpgradeDatabase.Find's
        // assumption for the Market tree.
        public PrestigeUpgradeDefinition Find(PrestigeUpgradeEffect effect)
        {
            if (upgradesByEffect == null) BuildLookup();
            upgradesByEffect.TryGetValue(effect, out var def);
            return def;
        }

        private void BuildLookup()
        {
            upgradesByEffect = new Dictionary<PrestigeUpgradeEffect, PrestigeUpgradeDefinition>();
            foreach (var def in Upgrades)
            {
                if (def != null) upgradesByEffect[def.Effect] = def;
            }
        }
    }
}
