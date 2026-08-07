using UnityEngine;

namespace Interaction
{
    // Proximity prompt + E-to-interact per GameDesignDoc "Mechanics": approaching a building
    // pops up interaction text, pressing E interacts.
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [SerializeField] private float interactionRadius = 2f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private InteractionPromptUI promptUI;

        private IInteractable current;

        private void Update()
        {
            var found = FindNearestInteractable();

            if (found != current)
            {
                current = found;
                if (current != null) promptUI.Show(current.PromptText);
                else promptUI.Hide();
            }

            if (current != null && Input.GetKeyDown(KeyCode.E))
            {
                current.Interact();
            }
        }

        private IInteractable FindNearestInteractable()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactableLayer);
            IInteractable nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<IInteractable>(out var interactable)) continue;

                float distSq = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = interactable;
                }
            }

            return nearest;
        }
    }
}
