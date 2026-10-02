using Events;
using Interaction;
using UnityEngine;

namespace Player
{
    // Free, automatic refuel + repair while the player is inside the Depot's interaction zone -
    // replaces the old paid Control Center "Supplies" tab. Uses the same Depot trigger that drives
    // the interaction prompt (PlayerInteractionDetector sits on this GameObject too), so the
    // "zone" is exactly where the Depot prompt shows up. Tops off every frame while inside, since
    // idle drain would otherwise tick the tank back down while the player is selling.
    public class PlayerDepotResupply : MonoBehaviour
    {
        [SerializeField] private LayerMask interactableLayer;

        // Below this, a top-off isn't worth a toast (e.g. re-entering after a second of idle drain).
        private const float NotifyThreshold = 1f;

        private PlayerController playerController;
        private PlayerHealth playerHealth;
        private int depotOverlapCount;

        private void Start()
        {
            playerController = GetComponent<PlayerController>();
            playerHealth = GetComponent<PlayerHealth>();
            if (playerController == null) Debug.LogError("PlayerDepotResupply needs a PlayerController on the same GameObject.");
            if (playerHealth == null) Debug.LogError("PlayerDepotResupply needs a PlayerHealth on the same GameObject.");
        }

        private void Update()
        {
            if (depotOverlapCount > 0) Resupply(false);
        }

        private void Resupply(bool notify)
        {
            if (playerHealth.IsDead) return;

            float fuelAdded = playerController.FuelMissing;
            float hpAdded = Mathf.Max(0f, playerHealth.MaxHp - playerHealth.CurrentHp);
            if (fuelAdded > 0f) playerController.AddFuel(fuelAdded);
            if (hpAdded > 0f) playerHealth.AddHp(hpAdded);

            if (!notify) return;

            bool refueled = fuelAdded >= NotifyThreshold;
            bool repaired = hpAdded >= NotifyThreshold;
            string message = refueled && repaired ? "Refueled & repaired at the Depot"
                : refueled ? "Refueled at the Depot"
                : repaired ? "Repaired at the Depot"
                : null;
            if (message == null) return;

            GameManager.EventService.Dispatch(new NotificationEvent(message, NotificationUrgency.Queued));
            GameManager.EventService.Dispatch(new DepotResupplyEvent(transform, refueled, repaired));
        }

        private bool IsDepot(Collider2D collision)
        {
            if (!interactableLayer.Contains(collision.gameObject.layer)) return false;
            var interactable = collision.GetComponent<BuildingInteractable>();
            return interactable != null && interactable.InteractableType == InteractableType.Building_Depot;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!IsDepot(collision)) return;
            depotOverlapCount++;
            if (depotOverlapCount != 1) return;

            GameManager.EventService.Dispatch(new PlayerAtDepotChangedEvent(true));
            Resupply(true);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!IsDepot(collision)) return;
            if (depotOverlapCount == 0) return;

            depotOverlapCount--;
            if (depotOverlapCount == 0) GameManager.EventService.Dispatch(new PlayerAtDepotChangedEvent(false));
        }
    }
}
