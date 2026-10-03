using Atmosphere;
using Events;
using Interaction;
using MapGeneration;
using UI;
using UnityEngine;

namespace Story
{
    // A Keystone in one of the Seal Chambers, or the last Seal in the Vault (GameDesignDoc
    // "# Story & Endgame: The Seals"). The room itself is an ordinary StructureStampFeature on this
    // chamber's layer, regenerated with every Dig; this object finds where it was stamped and
    // stands on its floor. Examining it reads the room's mural once, then offers the choice:
    // take the Keystone or leave it, or - at the Vault - Release or Reseal. What it shows comes from
    // GameManager.StoryManager, so a taken Keystone stays gone in every later Dig, and once the
    // Seal is mended neither the Keystones nor the last Seal appear in their rooms again.
    //
    // Scene layout: this component sits on the trigger collider (Interactable layer) that
    // PlayerInteractionDetector picks up; the stone's sprite is a child so it can bob while the
    // trigger stays still.
    public class SealChamber : MonoBehaviour, IInteractable
    {
        [Tooltip("The layer this chamber's structure is stamped on.")]
        [SerializeField] private int layerIndex;
        [Tooltip("The room to stand in - the same StructureDefinition the layer's StructureStampFeature stamps.")]
        [SerializeField] private StructureDefinition structure;
        [Tooltip("The Vault holds the last Seal and the ending choice, instead of a Keystone.")]
        [SerializeField] private bool isVault;
        [Tooltip("Which Keystone this is, top chamber first. Ignored for the Vault.")]
        [SerializeField] private int keystoneIndex;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer stoneRenderer;
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobSpeed = 1.4f;
        [Tooltip("Degrees per second - the last Seal turns slowly.")]
        [SerializeField] private float spinSpeed;
        [Tooltip("Drawn above fog, so a player digging nearby spots the chamber first.")]
        [SerializeField] private Color glowColor = new(0.78f, 0.49f, 1f, 0.55f);
        [SerializeField] private float glowDiameter = 3f;
        [SerializeField] private float glowPulseAmount = 0.12f;

        private static MapGenerationService map => GameManager.MapGenerationService;
        private static StoryManager story => GameManager.StoryManager;

        private Collider2D interactTrigger;
        private SpriteRenderer glow;
        private Vector3 stoneRestPosition;
        private MineWorld placedForWorld;
        private int placedForSeed;
        private bool hasRoom;
        private string pendingConversation;
        private bool broken;

        public InteractableType InteractableType => InteractableType.SealChamber;

        private int ChamberIndex => isVault ? story.VaultChamberIndex : keystoneIndex;
        private string ConversationId => $"Story.Chamber.{ChamberIndex}";

        // Where the stone floats - the endings play their effects on the last Seal here.
        public Vector3 StonePosition => transform.position + stoneRestPosition;

        // A Keystone is there until taken, the last Seal until it's broken - and after Reseal every
        // room stands empty.
        private bool IsStonePresent => !broken && story.Ending == StoryEnding.None && (isVault || !story.IsKeystoneTaken(keystoneIndex));

        private void Awake()
        {
            if (structure == null) Debug.LogError($"SealChamber '{name}': structure is not assigned.");
            if (stoneRenderer == null) Debug.LogError($"SealChamber '{name}': stoneRenderer is not assigned.");

            interactTrigger = GetComponent<Collider2D>();
            if (interactTrigger == null) Debug.LogError($"SealChamber '{name}' needs a trigger Collider2D on the same GameObject.");
        }

        private void Start()
        {
            stoneRestPosition = stoneRenderer.transform.localPosition;
            glow = GlowSprites.CreateGlow(transform, glowColor, glowDiameter);
            glow.transform.localPosition = stoneRestPosition;
            Refresh();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Add<DialogFinishedEvent>(OnDialogFinished);
            GameManager.EventService.Add<StoryProgressChangedEvent>(Refresh);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Remove<DialogFinishedEvent>(OnDialogFinished);
            GameManager.EventService.Remove<StoryProgressChangedEvent>(Refresh);
        }

        // The world is swapped wholesale on save restore and regenerated on prestige, so (like
        // Critters.CritterShopController) re-place whenever the world or its seed changes.
        private void Update()
        {
            var world = map.World;
            if (world != placedForWorld || world.Seed != placedForSeed)
            {
                placedForWorld = world;
                placedForSeed = world.Seed;
                Place();
            }

            if (!stoneRenderer.enabled) return;

            float t = Time.time;
            stoneRenderer.transform.localPosition = stoneRestPosition + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
            stoneRenderer.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            glow.transform.localScale = Vector3.one * (glowDiameter * (1f + Mathf.Sin(t * bobSpeed * 1.7f) * glowPulseAmount));
        }

        private void Place()
        {
            hasRoom = false;
            foreach (var (stamped, rect) in placedForWorld.GetOrGenerateChunk(layerIndex).StampedStructures)
            {
                if (stamped != structure) continue;

                // The middle of the room's bottom open row - the row below that is its floor.
                transform.position = map.CellToWorldCenter(layerIndex, rect.xMin + rect.width / 2, rect.yMax - 2);
                hasRoom = true;
                break;
            }

            if (!hasRoom) Debug.LogWarning($"SealChamber '{name}': no {structure.name} was stamped on layer {layerIndex} for seed {placedForSeed} - hidden for this Dig.");
            Refresh();
        }

        private void Refresh()
        {
            // StoryProgressChangedEvent can arrive (save restore) before Start has built the glow.
            if (glow == null) return;

            bool present = hasRoom && IsStonePresent;
            stoneRenderer.enabled = present;
            glow.enabled = present;
            // Disabling the trigger fires OnTriggerExit2D, so the detector drops the prompt.
            interactTrigger.enabled = present;
        }

        // Story.ReleaseCinematic: the last Seal is gone for the rest of this scene's life.
        public void Break()
        {
            broken = true;
            Refresh();
        }

        private void OnPlayerInteracted(PlayerInteractedEvent e)
        {
            if (!ReferenceEquals(e.Target, this) || e.InteractionType != InteractionType.Primary) return;
            // Interact also advances dialog - a press mid-conversation or with the choice up isn't a new visit.
            if (ModalTracker.IsAnyModalOpen || story.IsEndingPlaying) return;

            if (story.HasExamined(ChamberIndex))
            {
                ShowChoice();
                return;
            }

            story.MarkExamined(ChamberIndex);
            pendingConversation = ConversationId;
            GameManager.EventService.Dispatch(new DialogRequestedEvent(pendingConversation, story.BuildChamberLines(ChamberIndex)));
        }

        private void OnDialogFinished(DialogFinishedEvent e)
        {
            if (e.ConversationId != pendingConversation) return;

            pendingConversation = null;
            ShowChoice();
        }

        private void ShowChoice()
        {
            var content = story.Content;
            if (isVault)
            {
                int cost = story.ResealCost;
                GameManager.EventService.Dispatch(new StoryChoiceRequestedEvent(
                    content.VaultTitle,
                    string.Format(content.VaultBody, cost, Economy.Wallet.Instance.ArtifactCount),
                    content.VaultLeaveLabel,
                    content.VaultReleaseLabel, () => story.ChooseEnding(StoryEnding.Release),
                    string.Format(content.VaultResealLabel, cost), () => story.ChooseEnding(StoryEnding.Reseal),
                    story.CanAffordReseal));
                return;
            }

            int reward = story.KeystoneReward(keystoneIndex);
            GameManager.EventService.Dispatch(new StoryChoiceRequestedEvent(
                content.KeystoneTitle,
                string.Format(content.KeystoneWarning, reward, story.SealsHolding, story.SealsHolding - 1, story.ResealCost, story.ResealCostIfTaken(keystoneIndex)),
                content.KeystoneLeaveLabel,
                string.Format(content.KeystoneTakeLabel, reward), () => story.TakeKeystone(keystoneIndex)));
        }
    }
}
