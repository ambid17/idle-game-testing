using System.Collections.Generic;
using Events;
using Player;
using Settings;
using UnityEngine;

namespace Interaction
{
    // Generic proximity prompt + E-to-interact per GameDesignDoc "Mechanics": approaching any
    // IInteractable (buildings, chests, ...) pops up its prompt text, pressing E interacts.
    // Multiple IInteractables can overlap the trigger at once (e.g. a death-drop chest spawned
    // next to a building); the detector tracks all of them but only ever prompts/interacts with
    // whichever one is currently closest to the player, so a single E press can't fire two
    // interactions and the prompt always reflects the object the player is actually closest to.
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private InteractionPromptUI promptUI;

        private readonly List<IInteractable> nearby = new List<IInteractable>();
        private IInteractable current;

        private void Update()
        {
            // Entries can be destroyed MonoBehaviours (e.g. a looted-empty Chest) without becoming
            // a C# null through the interface reference - check Unity's own null first.
            nearby.RemoveAll(interactable => interactable is Object obj && obj == null);

            var closest = GetClosest();
            if (closest != current)
            {
                current = closest;
                if (current != null)
                {
                    promptUI.Show(current.InteractableType);
                }
                else
                {
                    promptUI.Hide();
                }
            }

            var keybinds = GameManager.KeybindService;
            // On a gamepad the interact buttons double as UI Submit (A) etc., so while a panel is
            // open they belong to the panel's focused button rather than re-interacting with the
            // building (e.g. Depot's Secondary = Deposit All). A press that just closed a panel
            // through its UI is likewise not also an interact.
            bool gamepadInPanel = InputBlocker.IsBlocked && keybinds.CurrentScheme == InputScheme.Gamepad;

            if (current != null && !gamepadInPanel && !InputBlocker.WasUnblockedThisFrame)
            {
                var interactionType = InteractionType.None;

                if (keybinds.WasPressedThisFrame(GameAction.InteractPrimary))
                {
                    interactionType = InteractionType.Primary;
                }
                if (keybinds.WasPressedThisFrame(GameAction.InteractSecondary))
                {
                    interactionType = InteractionType.Secondary;
                }
                if (keybinds.WasPressedThisFrame(GameAction.InteractTertiary))
                {
                    interactionType = InteractionType.Tertiary;
                }

                if (interactionType != InteractionType.None)
                {
                    GameManager.EventService.Dispatch(new PlayerInteractedEvent(current.InteractableType, interactionType));
                }
            }
        }

        private IInteractable GetClosest()
        {
            IInteractable closest = null;
            var closestSqrDistance = float.MaxValue;
            var position = transform.position;

            foreach (var interactable in nearby)
            {
                if (interactable is not Component component) continue;

                var sqrDistance = (component.transform.position - position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = interactable;
                }
            }

            return closest;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!interactableLayer.Contains(collision.gameObject.layer))
            {
                return;
            }
            var other = collision.GetComponent<IInteractable>();
            if (other == null) return;

            if (!nearby.Contains(other))
            {
                nearby.Add(other);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!interactableLayer.Contains(collision.gameObject.layer))
            {
                return;
            }
            var other = collision.GetComponent<IInteractable>();
            if (other == null) return;

            nearby.Remove(other);
        }
    }
}
