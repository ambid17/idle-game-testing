using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    [CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Economy/Upgrade Database")]
    public class UpgradeDatabase : ScriptableObject
    {
        public List<UpgradeDefinition> Upgrades = new();

        private Dictionary<UpgradeEffect, UpgradeDefinition> upgradesByEffect;
        private Dictionary<string, UpgradeDefinition> upgradesByName;

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
            if (upgradesByName == null) BuildLookup();
            upgradesByName.TryGetValue(id, out var def);
            return def;
        }

        private void BuildLookup()
        {
            upgradesByEffect = new Dictionary<UpgradeEffect, UpgradeDefinition>();
            upgradesByName = new Dictionary<string, UpgradeDefinition>();
            foreach (var def in Upgrades)
            {
                if (def == null) continue;
                if (upgradesByEffect.ContainsKey(def.Effect))
                {
                    Debug.LogWarning($"Duplicate UpgradeDefinition effect found: {def.Effect} on {def.DisplayName}. Only one definition per effect is supported - the later entry wins.");
                }
                upgradesByEffect[def.Effect] = def;
                if(upgradesByName.ContainsKey(def.DisplayName))
                {
                    Debug.LogWarning($"Duplicate UpgradeDefinition name found: {def.DisplayName}. This may cause issues with saving/loading.");
                }
                upgradesByName[def.DisplayName] = def;
            }
        }

        public void Validate()
        {
            if (Upgrades == null)
            {
                Debug.LogError("UpgradeDatabase is not assigned in GameManager.");
                return;
            }
            // C# silently allows two enum members to share a value - which would make two
            // definitions indistinguishable once serialized.
            if (System.Enum.GetValues(typeof(UpgradeEffect)).Length != new HashSet<int>((int[])System.Enum.GetValues(typeof(UpgradeEffect))).Count)
            {
                Debug.LogError("UpgradeEffect has two members sharing the same explicit value.");
            }
            foreach (var upgrade in Upgrades)
            {
                if (upgrade == null)
                {
                    Debug.LogError("UpgradeDatabase contains a null UpgradeDefinition.");
                    continue;
                }
                if (string.IsNullOrEmpty(upgrade.DisplayName))
                {
                    Debug.LogError($"UpgradeDefinition '{upgrade.name}' has an invalid display name.");
                }
                if (upgrade.Icon == null)
                {
                    Debug.LogError($"UpgradeDefinition '{upgrade.name}' has no icon assigned.");
                }
                if (upgrade.MaxLevel <= 0)
                {
                    Debug.LogError($"UpgradeDefinition '{upgrade.name}' has an invalid MaxLevel.");
                }
                if (upgrade.BaseCost <= 0)
                {
                    Debug.LogError($"UpgradeDefinition '{upgrade.name}' has an invalid BaseCost.");
                }
                if (upgrade.Prerequisite == upgrade)
                {
                    Debug.LogError($"UpgradeDefinition '{upgrade.name}' lists itself as its own Prerequisite.");
                }
                // Asset filenames are named after their effect, so a mismatch means the serialized
                // Effect int points at the wrong (or a removed) UpgradeEffect member.
                if (upgrade.name != upgrade.Effect.ToString())
                {
                    Debug.LogError($"UpgradeDefinition '{upgrade.name}' has Effect '{upgrade.Effect}' - asset name and Effect must match.");
                }
            }
        }
    }
}
