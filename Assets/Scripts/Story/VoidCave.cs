using System.Collections;
using Atmosphere;
using Audio;
using Effects;
using Events;
using Interaction;
using Persistence;
using Player;
using UI;
using Unity.Cinemachine;
using UnityEngine;

namespace Story
{
    // The Reseal ending (GameDesignDoc "# Story & Endgame: The Seals"), and the place it happens in:
    // the Bound's cave in the void, heaped with the Makers' riches. Mending the last Seal has the
    // Bound pull the player through a portal into the cave, where he thanks them for letting him
    // rest. The player can then walk about, speak to him again, and take the portal home - after
    // which the cave is gone for good, and (StoryManager.Ending == Reseal) no Keystone or Seal
    // appears in the mine again.
    //
    // The cave is a real room far above the map, where every map system reads "open sky", so it
    // needs nothing from map generation. Quitting inside it is fine: the save has the player's
    // position in the cave, and the cave stays until StoryManager.VoidCaveLeft.
    //
    // Scene layout: this component stays enabled on the root and toggles the "Contents" child (art,
    // floor and wall colliders, the Bound, the portal). The portal and the Bound each carry a
    // trigger on the Interactable layer with a BuildingInteractable (VoidCavePortal / TheBound).
    public class VoidCave : MonoBehaviour
    {
        private const string SpeechConversation = "Story.VoidCave.Speech";
        private const string RepeatConversation = "Story.VoidCave.Repeat";
        private const string VoiceTag = "{voice}";

        [SerializeField] private GameObject contents;
        [Tooltip("Where the player is set down in the cave.")]
        [SerializeField] private Transform arrivalPoint;
        [Tooltip("Where the cave's portal sets the player down at home.")]
        [SerializeField] private Transform homePoint;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerPortalTravel playerTravel;
        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private SealChamber vault;

        [Header("The Bound")]
        [SerializeField] private SpriteRenderer boundRenderer;
        [SerializeField] private float boundBobAmplitude = 0.12f;
        [SerializeField] private float boundBobSpeed = 0.8f;
        [SerializeField] private Color boundGlowColor = new(0.75f, 0.3f, 1f, 0.4f);
        [Tooltip("World units.")]
        [SerializeField] private float boundGlowDiameter = 9f;

        [Header("Portal home")]
        [Tooltip("Spins; scaled up from nothing once the Bound has spoken.")]
        [SerializeField] private SpriteRenderer portalRenderer;
        [Tooltip("The trigger PlayerInteractionDetector picks up - enabled with the portal.")]
        [SerializeField] private Collider2D portalTrigger;
        [SerializeField] private float portalSpinSpeed = -200f;
        [SerializeField, Min(0.01f)] private float portalOpenSeconds = 0.5f;

        [Header("Riches")]
        [Tooltip("Glints appear inside this box (local to the cave root).")]
        [SerializeField] private Vector2 glintAreaCenter = new(0f, -2.4f);
        [SerializeField] private Vector2 glintAreaSize = new(20f, 2.6f);
        [SerializeField, Min(0f)] private float glintsPerSecond = 9f;
        [SerializeField] private Color glintColor = new(1f, 0.87f, 0.4f, 1f);

        [Header("Reseal sequence")]
        [SerializeField, Min(0f)] private float mendSeconds = 1.6f;
        [SerializeField] private Color voidColor = new(0.75f, 0.3f, 1f, 1f);
        [Tooltip("Farther than this from the cave, the player has left it (portal or not).")]
        [SerializeField, Min(1f)] private float leaveDistance = 40f;

        private static StoryManager story => GameManager.StoryManager;

        private SpriteRenderer boundGlow;
        private Vector3 boundRestPosition;
        private Vector3 portalBaseScale;
        private bool portalOpen;
        private bool isPlaying;
        private bool isLeaving;
        private bool speechRequested;

        private void Awake()
        {
            if (contents == null) Debug.LogError("VoidCave.contents is not assigned.");
            if (arrivalPoint == null) Debug.LogError("VoidCave.arrivalPoint is not assigned.");
            if (homePoint == null) Debug.LogError("VoidCave.homePoint is not assigned.");
            if (player == null) Debug.LogError("VoidCave.player is not assigned.");
            if (playerTravel == null) Debug.LogError("VoidCave.playerTravel is not assigned.");
            if (followCamera == null) Debug.LogError("VoidCave.followCamera is not assigned.");
            if (vault == null) Debug.LogError("VoidCave.vault is not assigned.");
            if (boundRenderer == null) Debug.LogError("VoidCave.boundRenderer is not assigned.");
            if (portalRenderer == null) Debug.LogError("VoidCave.portalRenderer is not assigned.");
            if (portalTrigger == null) Debug.LogError("VoidCave.portalTrigger is not assigned.");

            boundRestPosition = boundRenderer.transform.localPosition;
            portalBaseScale = portalRenderer.transform.localScale;
        }

        private void Start()
        {
            boundGlow = GlowSprites.CreateGlow(boundRenderer.transform, boundGlowColor, boundGlowDiameter / boundRenderer.transform.lossyScale.x);
            boundGlow.sortingOrder = boundRenderer.sortingOrder - 1;
            Refresh();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<StoryProgressChangedEvent>(Refresh);
            GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Add<DialogFinishedEvent>(OnDialogFinished);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<StoryProgressChangedEvent>(Refresh);
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Remove<DialogFinishedEvent>(OnDialogFinished);
        }

        private void Refresh()
        {
            contents.SetActive(story.IsInVoidCave);
            if (!story.IsInVoidCave) return;

            // A save made mid-speech: he says it again once the screen is free.
            if (!story.VoidCaveSpeechHeard && !isPlaying && !speechRequested)
            {
                speechRequested = true;
                story.QueueConversation(SpeechConversation, story.BuildLines(story.Content.VoidCaveSpeech));
            }
            SetPortalOpen(story.VoidCaveSpeechHeard);
        }

        private void Update()
        {
            if (!contents.activeSelf) return;

            float t = Time.time;
            boundRenderer.transform.localPosition = boundRestPosition + Vector3.up * (Mathf.Sin(t * boundBobSpeed) * boundBobAmplitude);
            boundGlow.transform.localScale = Vector3.one * (boundGlowDiameter / boundRenderer.transform.lossyScale.x * (1f + Mathf.Sin(t * 1.7f) * 0.06f));
            portalRenderer.transform.Rotate(0f, 0f, portalSpinSpeed * Time.deltaTime);
            EmitGlints(Time.deltaTime);

            // Out of the cave some other way (a respawn, a dev teleport): it closes behind them.
            if (!isPlaying && !isLeaving && !playerTravel.IsTraveling && SaveService.Instance.HasLoadedData
                && Vector2.Distance(player.transform.position, transform.position) > leaveDistance)
            {
                story.MarkVoidCaveLeft();
            }
        }

        private void EmitGlints(float deltaTime)
        {
            float expected = glintsPerSecond * deltaTime;
            int count = Mathf.FloorToInt(expected) + (Random.value < expected % 1f ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                var offset = glintAreaCenter + new Vector2(Random.Range(-0.5f, 0.5f) * glintAreaSize.x, Random.Range(-0.5f, 0.5f) * glintAreaSize.y);
                GameManager.WorldEffects.Sparkle(transform.position + (Vector3)offset, Vector3.up * Random.Range(0f, 0.25f), Random.Range(0.15f, 0.4f), Random.Range(0.4f, 0.9f), glintColor);
            }
        }

        // ---- Reseal ----

        // StoryManager.ChooseEnding, once the cost is known to be affordable.
        public void PlayReseal()
        {
            if (isPlaying)
            {
                Debug.LogError("VoidCave.PlayReseal: already playing.");
                return;
            }
            StartCoroutine(ResealSequence());
        }

        // The artifacts go back into the Seal, the Seal closes for good, and the Bound takes the
        // player to his cave.
        private IEnumerator ResealSequence()
        {
            isPlaying = true;
            story.SetEndingPlaying(true);
            GameManager.EventService.Dispatch<UICloseEvent>();
            InputBlocker.SetBlocked(true);

            Vector3 sealPosition = vault.StonePosition;
            GameManager.AudioService.Play(SoundId.RockRumble);
            GameManager.CameraShake.Shake(Vector2.up * story.TremorForce, mendSeconds);
            float streamTimer = 0f;
            for (float t = 0f; t < mendSeconds; t += Time.deltaTime)
            {
                // Motes stream from the player into the Seal.
                streamTimer -= Time.deltaTime;
                if (streamTimer <= 0f)
                {
                    streamTimer = 0.03f;
                    Vector3 from = player.transform.position + (Vector3)(Random.insideUnitCircle * 0.5f);
                    const float moteSeconds = 0.45f;
                    GameManager.WorldEffects.Sparkle(from, (sealPosition - from) / moteSeconds, Random.Range(0.2f, 0.4f), moteSeconds, Random.value < 0.5f ? glintColor : voidColor);
                }
                yield return null;
            }

            GameManager.AudioService.Play(SoundId.Prestige);
            GameManager.WorldEffects.SparkleBurst(sealPosition, 30, 0.4f, 6f, voidColor);
            // The Seal and every Keystone are gone from here on.
            story.CompleteReseal();
            player.AddFuel(player.FuelMissing);
            yield return new WaitForSeconds(0.7f);

            GameManager.AudioService.Play(SoundId.BuildingPortal);
            playerTravel.TryTravelTo(arrivalPoint.position, () =>
            {
                followCamera.PreviousStateIsValid = false;
                StartCoroutine(story.Flash(voidColor, 1f, 0f, 1.2f));
                // From here a reload puts the player in the cave.
                SaveService.Instance.Save();
            });
            while (playerTravel.IsTraveling) yield return null;

            InputBlocker.SetBlocked(false);
            story.SetEndingPlaying(false);
            isPlaying = false;
            speechRequested = true;
            GameManager.EventService.Dispatch(new DialogRequestedEvent(SpeechConversation, story.BuildLines(story.Content.VoidCaveSpeech)));
        }

        private void OnDialogFinished(DialogFinishedEvent e)
        {
            if (e.ConversationId != SpeechConversation || story.VoidCaveSpeechHeard || !story.IsInVoidCave) return;

            story.MarkVoidCaveSpeechHeard();
            SaveService.Instance.Save();
            SetPortalOpen(true);
        }

        // ---- In the cave ----

        private void SetPortalOpen(bool open)
        {
            portalTrigger.enabled = open;
            if (open == portalOpen && portalRenderer.gameObject.activeSelf == open) return;

            portalOpen = open;
            portalRenderer.gameObject.SetActive(open);
            if (!open) return;

            GameManager.AudioService.Play(SoundId.RespawnPortal);
            StartCoroutine(OpenPortal());
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
            if (e.InteractionType != InteractionType.Primary) return;
            // Interact also advances dialog - a press mid-conversation isn't a new one.
            if (ModalTracker.IsAnyModalOpen || isPlaying || isLeaving) return;

            if (e.InteractableType == InteractableType.TheBound)
            {
                string line = StoryContent.PickRandom(story.Content.VoidCaveRepeatLines);
                GameManager.EventService.Dispatch(new DialogRequestedEvent(RepeatConversation, story.BuildLines(new[] { VoiceTag + line })));
            }
            else if (e.InteractableType == InteractableType.VoidCavePortal)
            {
                StartCoroutine(GoHome());
            }
        }

        private IEnumerator GoHome()
        {
            isLeaving = true;
            portalTrigger.enabled = false;
            GameManager.AudioService.Play(SoundId.BuildingPortal);
            playerTravel.TryTravelTo(homePoint.position, () =>
            {
                followCamera.PreviousStateIsValid = false;
                StartCoroutine(story.Flash(voidColor, 1f, 0f, 1.2f));
            });
            while (playerTravel.IsTraveling) yield return null;

            isLeaving = false;
            story.MarkVoidCaveLeft();
        }
    }
}
