using Events;
using UnityEngine;
using UnityEngine.Events;

namespace Interaction
{
    // Building-specific UIs (depot, market, museum, ...) subscribe to onInteract (Inspector-wired)
    // or the BuildingInteractedEvent (code-wired via EventService, filtered by Type, so a UI
    // script can find its building without any scene wiring) rather than requiring changes here.
    public enum InteractableType
    {
        Depot,
        Market,
        Museum,
        Processing
    }
    public class BuildingInteractable : MonoBehaviour
    {
        [SerializeField] private string promptText = "Press E to interact";
        [SerializeField] private UnityEvent onInteract;
        [SerializeField] private InteractableType interactableType;

        public string PromptText => promptText;
        public InteractableType Type => interactableType;

        public void Interact()
        {
            onInteract?.Invoke();
            GameManager.EventService.Dispatch(new BuildingInteractedEvent(interactableType));
        }
    }
}
