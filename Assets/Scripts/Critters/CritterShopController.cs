using System.Collections.Generic;
using Atmosphere;
using Events;
using Interaction;
using MapGeneration;
using UI;
using UnityEngine;

namespace Critters
{
    // Runs the Critter Shop: keeps the shop building standing on the floor of this seed's shop
    // cave (ChunkGenerator.CarveShopCave - one per run, somewhere in layers 1-3), plays the
    // shopkeeper's intro the first time the player walks in, opens CritterShopUI, and turns the
    // player's jar in with the shopkeeper reacting to each new species and hat. Also sends the
    // per-catch notifications, including a one-time hint about where the shop is. Scene-placed
    // singleton (child of GameManager) since it needs Inspector references to the building and panel.
    public class CritterShopController : Singleton<CritterShopController>
    {
        private const string IntroConversation = "CritterShop.Intro";
        private const string ChatConversation = "CritterShop.Chat";
        private const string TurnInConversation = "CritterShop.TurnIn";

        [Tooltip("Scene object with the shop's sprite, a trigger on the Interactable layer and a BuildingInteractable (Building_CritterShop). Its pivot should be bottom-center - it's placed on the cave floor.")]
        [SerializeField] private GameObject shopBuilding;
        [SerializeField] private CritterShopUI shopUI;
        [SerializeField] private ShopkeeperDialog dialog;
        [Tooltip("Lantern glow over the shop, drawn above fog so a player exploring nearby spots it first.")]
        [SerializeField] private Color lanternGlowColor = new(1f, 0.75f, 0.35f, 0.55f);
        [SerializeField] private float lanternGlowSize = 4f;
        [SerializeField] private Vector2 lanternGlowOffset = new(0f, 1.5f);

        private static MapGenerationService map => GameManager.MapGenerationService;

        private MineWorld placedForWorld;
        private int placedForSeed;
        private string activeConversation;
        private bool openShopAfterConversation;

        public ShopkeeperDialog Dialog => dialog;
        public bool HasShop { get; private set; }
        public int ShopLayerIndex { get; private set; }
        public Vector3 ShopPosition { get; private set; }

        protected override void Initialize()
        {
            base.Initialize();
            if (shopBuilding == null) Debug.LogError("CritterShopController.shopBuilding is not assigned.");
            if (shopUI == null) Debug.LogError("CritterShopController.shopUI is not assigned.");
            if (dialog == null) Debug.LogError("CritterShopController.dialog is not assigned.");

            GlowSprites.CreateGlow(shopBuilding.transform, lanternGlowColor, lanternGlowSize).transform.localPosition = lanternGlowOffset;
            shopBuilding.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Add<DialogFinishedEvent>(OnDialogFinished);
            GameManager.EventService.Add<CritterCaughtEvent>(OnCritterCaught);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);
            GameManager.EventService.Remove<DialogFinishedEvent>(OnDialogFinished);
            GameManager.EventService.Remove<CritterCaughtEvent>(OnCritterCaught);
        }

        // The world is swapped wholesale on save restore and regenerated on prestige, so rather
        // than chase every event that can do that, re-place whenever the world or its seed changes.
        private void Update()
        {
            var world = map.World;
            if (world == placedForWorld && world.Seed == placedForSeed) return;

            placedForWorld = world;
            placedForSeed = world.Seed;
            PlaceShop();
        }

        private void PlaceShop()
        {
            ShopLayerIndex = ChunkGenerator.GetShopLayerIndex(placedForSeed);
            var cave = placedForWorld.GetOrGenerateChunk(ShopLayerIndex).ShopCave;
            HasShop = cave.HasValue;
            if (!HasShop)
            {
                shopBuilding.SetActive(false);
                return;
            }

            // Bottom-center of the cave: the top edge of its GrassyDirt floor row.
            var rect = cave.Value;
            var bottomRowCenter = map.CellToWorldCenter(ShopLayerIndex, rect.xMin, rect.yMax - 1);
            float centerX = (rect.xMin + rect.width * 0.5f) * map.CellSize;
            ShopPosition = new Vector3(centerX, bottomRowCenter.y - map.CellSize * 0.5f, 0f);

            shopBuilding.transform.position = ShopPosition;
            shopBuilding.SetActive(true);
        }

        private void OnPlayerInteracted(PlayerInteractedEvent evt)
        {
            if (evt.InteractableType != InteractableType.Building_CritterShop || evt.InteractionType != InteractionType.Primary) return;
            // Interact also advances dialog - a press mid-conversation or with the panel up isn't a new visit.
            if (activeConversation != null || shopUI.IsOpen) return;

            if (!CritterCollection.Instance.HasMetShopkeeper)
            {
                CritterCollection.Instance.MarkShopkeeperMet();
                StartConversation(IntroConversation, BuildLines(dialog.IntroLines), openShopAfter: true);
                return;
            }

            shopUI.Open();
        }

        // CritterShopUI's Talk button.
        public void Chat() => StartConversation(ChatConversation, BuildLines(ShopkeeperDialog.PickRandom(dialog.Chatter)), openShopAfter: false);

        // CritterShopUI's Turn In button.
        public void TurnIn()
        {
            var collection = CritterCollection.Instance;
            if (collection.Jar.Count == 0)
            {
                StartConversation(TurnInConversation, BuildLines(ShopkeeperDialog.PickRandom(dialog.EmptyJarLines)), openShopAfter: false);
                return;
            }

            var result = collection.TurnInJar();
            var lines = BuildLines(string.Format(ShopkeeperDialog.PickRandom(dialog.TurnInLines), result.Count, result.Dollars.ToString("0")));

            foreach (var species in result.NewSpecies)
            {
                if (!string.IsNullOrEmpty(species.ShopkeeperQuip)) lines.AddRange(BuildLines(species.ShopkeeperQuip));
            }

            bool completedCollection = collection.SpeciesCollected >= GameManager.CritterDatabase.SpeciesCount;
            foreach (var hat in result.NewHats)
            {
                // The finale below hands over the last hat itself.
                if (completedCollection && hat.UnlockAtSpeciesCount >= GameManager.CritterDatabase.SpeciesCount) continue;
                lines.AddRange(BuildLines(string.Format(ShopkeeperDialog.PickRandom(dialog.HatUnlockedLines), hat.DisplayName)));
            }

            if (completedCollection && result.NewSpecies.Count > 0) lines.AddRange(BuildLines(dialog.AllFoundLines));

            StartConversation(TurnInConversation, lines, openShopAfter: false);
        }

        private void StartConversation(string conversationId, List<DialogLine> lines, bool openShopAfter)
        {
            activeConversation = conversationId;
            openShopAfterConversation = openShopAfter;
            GameManager.EventService.Dispatch(new DialogRequestedEvent(conversationId, lines));
        }

        private void OnDialogFinished(DialogFinishedEvent evt)
        {
            if (evt.ConversationId != activeConversation) return;

            activeConversation = null;
            if (openShopAfterConversation) shopUI.Open();
        }

        private List<DialogLine> BuildLines(params string[] entries)
        {
            var lines = new List<DialogLine>();
            if (entries == null) return lines;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                foreach (var page in entry.Split(ShopkeeperDialog.PageSeparator))
                {
                    var text = page.Trim();
                    if (text.Length > 0) lines.Add(new DialogLine(dialog.SpeakerName, dialog.Portrait, text));
                }
            }
            return lines;
        }

        private void OnCritterCaught(CritterCaughtEvent evt)
        {
            var collection = CritterCollection.Instance;
            GameManager.EventService.Dispatch(new NotificationEvent(
                $"Caught a {evt.Critter.DisplayName}! ({collection.Jar.Count} in jar)", NotificationUrgency.Queued, evt.Critter.Sprite));

            // First catch ever, shop not found yet: point the player at it.
            bool firstEverCatch = collection.Jar.Count == 1 && collection.SpeciesCollected == 0;
            if (firstEverCatch && !collection.HasMetShopkeeper && HasShop)
            {
                GameManager.EventService.Dispatch(new NotificationEvent(
                    $"Rumor has it an odd critter collector lives in a cave about {Mathf.Abs(ShopPosition.y):0}m down...", NotificationUrgency.TimeSensitive));
            }
        }
    }
}
