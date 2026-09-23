using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    [CreateAssetMenu(fileName = "PrestigeUpgradeDatabase", menuName = "Economy/Prestige Upgrade Database")]
    public class PrestigeUpgradeDatabase : ScriptableObject
    {
        public List<PrestigeUpgradeDefinition> Upgrades = new();

        private Dictionary<PrestigeUpgradeEffect, PrestigeUpgradeDefinition> upgradesByEffect;
        private Dictionary<string, PrestigeUpgradeDefinition> upgradesById;

        // Assumes at most one definition per PrestigeUpgradeEffect, matching UpgradeDatabase.Find's
        // assumption for the Market tree.
        public PrestigeUpgradeDefinition Find(PrestigeUpgradeEffect effect)
        {
            if (upgradesByEffect == null) BuildLookup();
            upgradesByEffect.TryGetValue(effect, out var def);
            return def;
        }

        // Used by PrestigeUpgradeManager.SetLevel to restore SaveService's Id-keyed save data onto
        // the matching definition, matching UpgradeDatabase.Find(string).
        public PrestigeUpgradeDefinition Find(string id)
        {
            if (upgradesById == null) BuildLookup();
            upgradesById.TryGetValue(id, out var def);
            return def;
        }

        private void BuildLookup()
        {
            upgradesByEffect = new Dictionary<PrestigeUpgradeEffect, PrestigeUpgradeDefinition>();
            upgradesById = new Dictionary<string, PrestigeUpgradeDefinition>();
            foreach (var def in Upgrades)
            {
                if (def == null) continue;
                upgradesByEffect[def.Effect] = def;
                upgradesById[def.DisplayName] = def;
            }
        }

        public void Validate()
        {
            if (Upgrades == null)
            {
                Debug.LogError("PrestigeUpgradeDatabase is not assigned in GameManager.");
                return;
            }
            // C# silently allows two enum members to share a value - which would make two
            // definitions indistinguishable once serialized.
            if (System.Enum.GetValues(typeof(PrestigeUpgradeEffect)).Length != new HashSet<int>((int[])System.Enum.GetValues(typeof(PrestigeUpgradeEffect))).Count)
            {
                Debug.LogError("PrestigeUpgradeEffect has two members sharing the same explicit value.");
            }
            foreach (var upgrade in Upgrades)
            {
                if (upgrade == null)
                {
                    Debug.LogError("PrestigeUpgradeDatabase contains a null PrestigeUpgradeDefinition.");
                    continue;
                }
                if (string.IsNullOrEmpty(upgrade.DisplayName))
                {
                    Debug.LogError($"PrestigeUpgradeDefinition '{upgrade.name}' has an invalid display name.");
                }
                if (upgrade.Icon == null)
                {
                    Debug.LogError($"PrestigeUpgradeDefinition '{upgrade.name}' has no icon assigned.");
                }
                if (upgrade.MaxLevel <= 0)
                {
                    Debug.LogError($"PrestigeUpgradeDefinition '{upgrade.name}' has an invalid MaxLevel.");
                }
                if (upgrade.BaseCost <= 0)
                {
                    Debug.LogError($"PrestigeUpgradeDefinition '{upgrade.name}' has an invalid BaseCost.");
                }
                if (upgrade.Prerequisite == upgrade)
                {
                    Debug.LogError($"PrestigeUpgradeDefinition '{upgrade.name}' lists itself as its own Prerequisite.");
                }
                // Asset filenames are named after their effect, so a mismatch means the serialized
                // Effect int points at the wrong (or a removed) PrestigeUpgradeEffect member.
                if (upgrade.name != upgrade.Effect.ToString())
                {
                    Debug.LogError($"PrestigeUpgradeDefinition '{upgrade.name}' has Effect '{upgrade.Effect}' - asset name and Effect must match.");
                }
            }
        }
    }
}
