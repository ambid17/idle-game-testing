using Atmosphere;
using Audio;
using Events;
using Interaction;
using UnityEngine;

namespace Economy
{
    // Hidden secret: a relic floating on a cloud platform 200m above the surface. Flying up to it
    // and pressing Interact banks a flat artifact reward straight into the Wallet (AddArtifacts, so
    // it skips the per-pickup value multiplier like other flat grants). One-time per save - the
    // collected flag goes through SaveService (GameSaveData.SkyArtifactCollected) and survives
    // prestige, so it can't be farmed every run.
    //
    // Scene layout: SkyIsland root with a Cloud child (one-way PlatformEffector2D on Ground, so the
    // player flies up through it and lands) and this SkyArtifact child, whose trigger collider on
    // the Interactable layer is what PlayerInteractionDetector picks up (same flow as Chest). The
    // bobbing Relic sprite is a child of this one so the trigger itself stays still.
    public class SkyArtifact : MonoBehaviour, IInteractable
    {
        [SerializeField] private int artifactReward = 10;
        [SerializeField] private SpriteRenderer relicRenderer;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobSpeed = 1.6f;
        [SerializeField] private Color glowColor = new(0.55f, 0.9f, 1f, 0.6f);
        [SerializeField] private float glowDiameter = 2.4f;
        [SerializeField] private float glowPulseAmount = 0.12f;

        public InteractableType InteractableType => InteractableType.SkyArtifact;
        public bool IsCollected { get; private set; }

        private Collider2D interactTrigger;
        private SpriteRenderer glow;
        private Vector3 relicRestPosition;

        private void Start()
        {
            if (relicRenderer == null)
            {
                Debug.LogError("SkyArtifact.relicRenderer is not assigned.");
                return;
            }

            interactTrigger = GetComponent<Collider2D>();
            if (interactTrigger == null) Debug.LogError("SkyArtifact needs a trigger Collider2D on the same GameObject.");

            relicRestPosition = relicRenderer.transform.localPosition;
            glow = GlowSprites.CreateGlow(relicRenderer.transform, glowColor, glowDiameter / relicRenderer.transform.lossyScale.x);

            // Restore may have run before Start (SaveService loads early).
            ApplyCollectedVisuals();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);
        }

        private void Update()
        {
            if (IsCollected) return;

            float t = Time.time;
            relicRenderer.transform.localPosition = relicRestPosition + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
            float pulse = 1f + Mathf.Sin(t * bobSpeed * 1.7f) * glowPulseAmount;
            glow.transform.localScale = Vector3.one * (glowDiameter / relicRenderer.transform.lossyScale.x * pulse);
        }

        private void OnPlayerInteracted(PlayerInteractedEvent e)
        {
            if (e.InteractableType == InteractableType.SkyArtifact && e.InteractionType == InteractionType.Primary)
            {
                Collect();
            }
        }

        private void Collect()
        {
            if (IsCollected) return;

            IsCollected = true;
            Wallet.Instance.AddArtifacts(artifactReward);
            GameManager.AudioService.Play(SoundId.ArtifactFound);
            GameManager.EventService.Dispatch(new NotificationEvent(
                $"Sky relic recovered! +{artifactReward} artifacts",
                NotificationUrgency.TimeSensitive,
                relicRenderer.sprite));
            ApplyCollectedVisuals();
        }

        // SaveService.ApplyLoadedData - the cloud stays either way, only the relic disappears.
        public void RestoreCollected(bool collected)
        {
            IsCollected = collected;
            ApplyCollectedVisuals();
        }

        private void ApplyCollectedVisuals()
        {
            if (relicRenderer != null) relicRenderer.gameObject.SetActive(!IsCollected);
            // Disabling the trigger fires OnTriggerExit2D, so the detector drops the prompt.
            if (interactTrigger != null) interactTrigger.enabled = !IsCollected;
        }
    }
}
