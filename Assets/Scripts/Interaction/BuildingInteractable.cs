using UnityEngine;
using UnityEngine.Events;

namespace Interaction
{
    // Building-specific UIs (depot, market, museum, ...) subscribe to onInteract
    // rather than requiring changes here.
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

        public void Interact() => onInteract?.Invoke();
    }
}
