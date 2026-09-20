using Events;
using UnityEngine;
using UnityEngine.Events;

namespace Interaction
{
    // Building-specific UIs (depot, market, museum, ...) subscribe to onInteract (Inspector-wired)
    // or the BuildingInteractedEvent (code-wired via EventService, filtered by Type, so a UI
    // script can find its building without any scene wiring) rather than requiring changes here.

    public class BuildingInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private InteractableType interactableType;
        public InteractableType InteractableType { get { return interactableType; } }
    }
}
