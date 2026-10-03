using System.Collections;
using Economy;
using Effects;
using Events;
using Interaction;
using Player;
using UI;
using UnityEngine;

namespace Critters
{
    // The Museum's Survival_CritterShopPortal perk: a portal standing beside the Critter Shop that
    // takes the player to the Depot - the same trip as Depot Recall (PlayerPortalTravel), with no
    // cooldown. A child of the shop building, so it moves with it and is hidden on seeds without a
    // shop. Stays closed (art hidden, trigger off) until the perk is owned, and opens with a pop the
    // moment it is.
    //
    // Scene layout: this object carries a trigger on the Interactable layer and a
    // BuildingInteractable (CritterShopPortal); portalRenderer is a child that spins.
    public class CritterShopPortal : MonoBehaviour
    {
        [SerializeField] private PlayerPortalTravel playerTravel;
        [SerializeField] private SpriteRenderer portalRenderer;
        [Tooltip("The trigger PlayerInteractionDetector picks up - enabled with the portal.")]
        [SerializeField] private Collider2D portalTrigger;
        [SerializeField] private float portalSpinSpeed = -200f;
        [SerializeField, Min(0.01f)] private float portalOpenSeconds = 0.5f;

        private Vector3 portalBaseScale;
        private bool portalOpen;

        private void Awake()
        {
            if (playerTravel == null) Debug.LogError("CritterShopPortal.playerTravel is not assigned.");
            if (portalRenderer == null) Debug.LogError("CritterShopPortal.portalRenderer is not assigned.");
            if (portalTrigger == null) Debug.LogError("CritterShopPortal.portalTrigger is not assigned.");

            portalBaseScale = portalRenderer.transform.localScale;
            portalRenderer.gameObject.SetActive(false);
            portalTrigger.enabled = false;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);
            // The shop is re-placed (and this re-enabled) on every new world - pop open again there.
            portalOpen = false;
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);
        }

        // Pulled every frame rather than on purchase events, so a save restore, a prestige applying
        // queued levels and the shop being re-placed are all covered.
        private void Update()
        {
            SetPortalOpen(PrestigeUpgradeManager.Instance.Survival_CritterShopPortalUnlocked);
            if (portalOpen) portalRenderer.transform.Rotate(0f, 0f, portalSpinSpeed * Time.deltaTime);
        }

        private void SetPortalOpen(bool open)
        {
            if (open == portalOpen) return;

            portalOpen = open;
            portalTrigger.enabled = open;
            portalRenderer.gameObject.SetActive(open);
            if (open) StartCoroutine(OpenPortal());
        }

        private IEnumerator OpenPortal()
        {
            for (float t = 0f; t < portalOpenSeconds; t += Time.deltaTime)
            {
                portalRenderer.transform.localScale = portalBaseScale * Mathf.Max(0f, Easing.OutBack(t / portalOpenSeconds));
                yield return null;
            }
            portalRenderer.transform.localScale = portalBaseScale;
        }

        private void OnPlayerInteracted(PlayerInteractedEvent e)
        {
            if (e.InteractableType != InteractableType.CritterShopPortal || e.InteractionType != InteractionType.Primary) return;
            // Interact also advances dialog - a press mid-conversation isn't a trip.
            if (ModalTracker.IsAnyModalOpen) return;

            playerTravel.TryTravelToDepot();
        }
    }
}
