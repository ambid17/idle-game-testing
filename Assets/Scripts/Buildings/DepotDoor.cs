using Events;
using UnityEngine;

namespace Buildings
{
    // The Depot's roll-up garage door. The shutter is its own sprite (cut out of the building art
    // by Tools/Buildings/make_depot_door.py) clipped by a SpriteMask the shape of the doorway, so
    // sliding it up reads as rolling into the frame. Lives on the Depot building. Open while the
    // player is in the Depot's zone (PlayerAtDepotChangedEvent) and for as long as automatons /
    // storage drones on a Depot run keep calling NotifyApproach, plus a short linger so the door
    // is still up as they turn around and leave.
    public class DepotDoor : MonoBehaviour
    {
        [SerializeField] private Transform door;
        [Tooltip("How far the shutter slides up when open (local units) - the doorway's height.")]
        [SerializeField] private float openHeight = 0.2275f;
        [SerializeField] private float slideSeconds = 0.35f;
        [Tooltip("Automatons/drones heading for the Depot open the door once they're this close (world units).")]
        [SerializeField] private float approachRadius = 3f;
        [Tooltip("How long the door stays up after the last automaton/drone visit.")]
        [SerializeField] private float lingerSeconds = 0.6f;

        private Vector3 closedPosition;
        private bool playerAtDepot;
        private float holdOpenUntil;
        private float open;

        private void Awake()
        {
            if (door == null) Debug.LogError("DepotDoor.door is not assigned.");

            closedPosition = door.localPosition;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerAtDepotChangedEvent>(OnPlayerAtDepotChanged);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerAtDepotChangedEvent>(OnPlayerAtDepotChanged);
        }

        private void OnPlayerAtDepotChanged(PlayerAtDepotChangedEvent evt) => playerAtDepot = evt.AtDepot;

        // Called every frame by an automaton/drone that is flying to the Depot.
        public void NotifyApproach(Vector3 position)
        {
            if ((position - door.position).sqrMagnitude > approachRadius * approachRadius) return;
            holdOpenUntil = Time.time + lingerSeconds;
        }

        private void Update()
        {
            bool wantOpen = playerAtDepot || Time.time < holdOpenUntil;
            open = Mathf.MoveTowards(open, wantOpen ? 1f : 0f, Time.deltaTime / slideSeconds);
            door.localPosition = closedPosition + Vector3.up * (openHeight * Mathf.SmoothStep(0f, 1f, open));
        }
    }
}
