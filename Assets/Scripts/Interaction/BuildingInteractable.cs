using System;
using UnityEngine;
using UnityEngine.Events;

namespace Interaction
{
    // Building-specific UIs (depot, market, museum, ...) subscribe to onInteract (Inspector-wired)
    // or the Interacted C# event (code-wired, e.g. so a UI script can find its building by Type
    // without any scene wiring) rather than requiring changes here.
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

        public event Action Interacted;

        public void Interact()
        {
            onInteract?.Invoke();
            Interacted?.Invoke();
        }
    }
}
