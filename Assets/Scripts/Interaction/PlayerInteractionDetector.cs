using Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interaction
{
    // Proximity prompt + E-to-interact per GameDesignDoc "Mechanics": approaching a building
    // pops up interaction text, pressing E interacts.
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private InteractionPromptUI promptUI;

        private BuildingInteractable current;
        Keyboard keyboard = Keyboard.current;

        private void Update()
        {
            if (current != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                GameManager.EventService.Dispatch(new BuildingInteractedEvent(current.Type));
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!interactableLayer.Contains(collision.gameObject.layer))
            {
                return;
            }
            var other = collision.GetComponent<BuildingInteractable>();
            current = other;
            promptUI.Show(current.PromptText);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!interactableLayer.Contains(collision.gameObject.layer))
            {
                return;
            }
            current = null;
            promptUI.Hide();
        }
    }
}
