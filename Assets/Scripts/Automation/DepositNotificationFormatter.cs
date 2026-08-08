using System.Collections.Generic;
using MapGeneration;

namespace Automation
{
    // Turns an OreDepositedByAutomationEvent's raw (entity name, ore dictionary) payload into
    // display text, e.g. "Automaton #2 deposited 5 Iron, 2 Gold" - shared so NotificationQueueUI
    // doesn't duplicate this formatting logic.
    public static class DepositNotificationFormatter
    {
        public static string Format(string entityDisplayName, IReadOnlyDictionary<BlockTypeId, int> deposited, BlockTypeDatabase blockTypeDatabase)
        {
            if (deposited == null || blockTypeDatabase == null) return entityDisplayName;

            var parts = new List<string>();
            foreach (var kvp in deposited)
            {
                if (kvp.Value <= 0) continue;

                var blockType = blockTypeDatabase.Get((byte)kvp.Key);
                string name = blockType != null && !string.IsNullOrEmpty(blockType.DisplayName) ? blockType.DisplayName : kvp.Key.ToString();
                parts.Add($"{kvp.Value} {name}");
            }

            return parts.Count == 0 ? $"{entityDisplayName} returned empty-handed" : $"{entityDisplayName} deposited {string.Join(", ", parts)}";
        }
    }
}
