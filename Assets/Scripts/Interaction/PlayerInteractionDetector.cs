using Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interaction
{
    // Generic proximity prompt + E-to-interact per GameDesignDoc "Mechanics": approaching any
    // IInteractable (buildings, chests, ...) pops up its prompt text, pressing E interacts. Only
    // one IInteractable is ever tracked at a time, so a single E press can't fire two interactions
    // when the player is near more than one interactable at once.
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private InteractionPromptUI promptUI;

        private IInteractable current;
        Keyboard keyboard => Keyboard.current;

        private void Update()
        {
            // current can be a destroyed MonoBehaviour (e.g. a looted-empty Chest) without becoming
            // a C# null through the interface reference - check Unity's own null first.
            if (current is Object obj && obj == null)
            {
                current = null;
                promptUI.Hide();
                return;
            }

            if (current != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                current.Interact();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!interactableLayer.Contains(collision.gameObject.layer))
            {
                return;
            }
            var other = collision.GetComponent<IInteractable>();
            if (other == null) return;

            current = other;
            promptUI.Show(current.PromptText);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!interactableLayer.Contains(collision.gameObject.layer))
            {
                return;
            }
            if (collision.GetComponent<IInteractable>() != current) return;

            current = null;
            promptUI.Hide();
        }
    }
}
