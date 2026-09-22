using System.Collections.Generic;
using Economy;
using Events;
using MapGeneration;

namespace Automation
{
    // Shared by MiningAutomaton and StorageDrone so every Depot-bound deposit consistently
    // updates IdleEarningsTracker and notifies listeners - StorageDrone used to skip the tracker
    // call, silently undercounting ore it drained from other carriers before they could deposit
    // it themselves.
    public static class AutomationDepositService
    {
        public static void Deposit(string entityDisplayName, IReadOnlyDictionary<BlockTypeId, int> withdrawn)
        {
            if (withdrawn == null || withdrawn.Count == 0) return;

            Depot.Instance.Deposit(withdrawn);

            foreach (var kvp in withdrawn)
            {
                if (kvp.Value > 0) IdleEarningsTracker.Instance.RecordOreDeposited(kvp.Key, kvp.Value);
            }

            GameManager.EventService.Dispatch(new OreDepositedByAutomationEvent(entityDisplayName, withdrawn));

            string message = DepositNotificationFormatter.Format(entityDisplayName, withdrawn, GameManager.BlockTypeDatabase);
            GameManager.EventService.Dispatch(new NotificationEvent(message, NotificationUrgency.Queued));
        }
    }
}
