using UnityEngine;
using UnityEngine.Events;

namespace Interaction
{
    // Generic building stub per GameDesignDoc "Mechanics": approaching shows a prompt, E
    // interacts. Building-specific UIs (depot, market, museum, ...) subscribe to onInteract
    // rather than requiring changes here.
    public class BuildingInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "Press E to interact";
        [SerializeField] private UnityEvent onInteract;

        public string PromptText => promptText;

        public void Interact() => onInteract?.Invoke();
    }
}
