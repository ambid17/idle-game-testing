using Automation;
using Events;
using MapGeneration;
using UnityEngine;

namespace UI
{
    // GameDesignDoc "notifications": spawns a toast for every automaton/storage-drone Depot
    // deposit. Per the resolved stacking decision, multiple toasts can be visible at once, each
    // with its own independent 3s auto-dismiss timer (NotificationItemUI) - no single-file
    // queueing/blocking between them.
    public class NotificationQueueUI : MonoBehaviour
    {
        [SerializeField] private Transform container;
        [SerializeField] private NotificationItemUI itemPrefab;

        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void OnEnable()
        {
            GameManager.EventService.Add<OreDepositedByAutomationEvent>(OnOreDeposited);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<OreDepositedByAutomationEvent>(OnOreDeposited);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void OnOreDeposited(OreDepositedByAutomationEvent evt)
        {
            if (itemPrefab == null || container == null)
            {
                Debug.LogError("NotificationQueueUI: Missing itemPrefab or container. Cannot show notification.");
                return;
            }

            string message = DepositNotificationFormatter.Format(evt.EntityDisplayName, evt.Deposited, blockTypeDatabase);
            Instantiate(itemPrefab, container).Bind(message);
        }

        private void OnPrestigeCompleted(PrestigeCompletedEvent evt)
        {
            if (itemPrefab == null || container == null)
            {
                Debug.LogError("NotificationQueueUI: Missing itemPrefab or container. Cannot show notification.");
                return;
            }

            Instantiate(itemPrefab, container).Bind("Prestige complete - the mine has regenerated.");
        }
    }
}
