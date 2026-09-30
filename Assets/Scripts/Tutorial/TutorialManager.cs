using System.Collections.Generic;
using Economy;
using Events;
using Interaction;
using Persistence;
using Player;
using UnityEngine;

namespace Tutorial
{
    // Drives the tutorial popup system: listens for the event-driven triggers (load, first open of
    // each building UI, first revive after a death, first automation purchases, first prestige,
    // first critter catch) and polls for the player first reaching layer 1, and - the first time only, tracked by TutorialId and persisted
    // via RestoreFromSaveData/ShownTutorials - dispatches ShowTutorialEvent with that tutorial's copy
    // from GameManager.TutorialDatabase. TryShow is public so any system with its own display
    // mechanism (e.g. Economy.MuseumRevealController/Processing.ProcessingCenterRevealController's
    // building-reveal cinematic) can still get the same persisted once-only guarantee.
    //
    // UI.Panels.TutorialModalUI (screen overlay), UI.Panels.WorldTutorialPopupUI (world popup), and
    // the building-reveal controllers all listen for the same ShowTutorialEvent and each decide from
    // TutorialEntry.DisplayType whether it's theirs to show - see ShowTutorialEvent's own comment in
    // Events.cs.
    public class TutorialManager : Singleton<TutorialManager>
    {
        [SerializeField] private PlayerController playerController;

        private readonly HashSet<TutorialId> shownTutorials = new();
        public IReadOnlyCollection<TutorialId> ShownTutorials => shownTutorials;

        // PlayerRevivedEvent also fires on prestige and from dev tools - only a revive that
        // follows an actual death should explain the lost-ore chest.
        private bool diedSinceLastRevive;

        protected override void Initialize()
        {
            base.Initialize();
            if (playerController == null)
            {
                Debug.LogError("TutorialManager.playerController is not assigned.");
            }
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Add<PlayerDiedEvent>(OnPlayerDied);
            GameManager.EventService.Add<PlayerRevivedEvent>(OnPlayerRevived);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Add<CritterCaughtEvent>(OnCritterCaught);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<PlayerDiedEvent>(OnPlayerDied);
            GameManager.EventService.Remove<PlayerRevivedEvent>(OnPlayerRevived);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Remove<CritterCaughtEvent>(OnCritterCaught);
        }

        // Hazards start on layer 1 (layer 0's HazardTable is empty), so the first time the player
        // gets there is when richer ore, harder dirt and hazards all need explaining.
        private void Update()
        {
            // Wait for the save to restore both the player's position and shownTutorials.
            if (!SaveService.Instance.HasLoadedData || HasShown(TutorialId.DeeperLayers)) return;

            var map = GameManager.MapGenerationService;
            int layerIndex = GameManager.LayerConfigProvider.GetLayerIndexAtWorldY(playerController.transform.position.y, map.CellSize);
            if (layerIndex >= 1) TryShow(TutorialId.DeeperLayers);
        }

        private void OnLoadCompleted()
        {
            TryShow(TutorialId.CoreGoal);
        }

        private void OnPlayerDied(PlayerDiedEvent evt) => diedSinceLastRevive = true;

        private void OnPlayerRevived()
        {
            if (!diedSinceLastRevive) return;
            diedSinceLastRevive = false;
            TryShow(TutorialId.DeathAndChests);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            if (evt.NewLevel != 1) return;

            switch (evt.Definition.Effect)
            {
                case UpgradeEffect.Automation_AutomatonCount: TryShow(TutorialId.Automatons); break;
                case UpgradeEffect.Automation_StorageDroneCount: TryShow(TutorialId.StorageDrones); break;
                case UpgradeEffect.Automation_FuelDroneCount: TryShow(TutorialId.FuelDrones); break;
            }
        }

        private void OnPrestigeCompleted(PrestigeCompletedEvent evt) => TryShow(TutorialId.NewRun);

        private void OnCritterCaught(CritterCaughtEvent evt) => TryShow(TutorialId.Critters, evt.Position);

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            if(evt.InteractionType != InteractionType.Primary) return;
            // attempt to show a tutorial for the building type interacted with
            var id = ToTutorialId(evt.InteractableType);
            if (id.HasValue) TryShow(id.Value);
        }

        private static TutorialId? ToTutorialId(InteractableType type)
        {
            switch (type)
            {
                case InteractableType.Building_Depot: return TutorialId.Building_Depot;
                case InteractableType.Building_Market: return TutorialId.Building_Market;
                case InteractableType.Building_Museum: return TutorialId.Building_Museum;
                case InteractableType.Building_Processing: return TutorialId.Building_Processing;
                case InteractableType.Building_ControlCenter: return TutorialId.Building_ControlCenter;
                default: return null;
            }
        }

        public void TryShow(TutorialId id, Vector3? worldPosition = null)
        {
            if (HasShown(id)) return;

            var database = GameManager.TutorialDatabase;
            if (database == null || !database.TryGet(id, out var entry))
            {
                Debug.LogError($"TutorialManager: no TutorialEntry found for {id}.");
                return;
            }

            shownTutorials.Add(id);
            GameManager.EventService.Dispatch(new ShowTutorialEvent(entry, worldPosition));
        }

        public bool HasShown(TutorialId id) => shownTutorials.Contains(id);

        // Dev-tool support (UI.DevPanelTimeTutorialTab): lets a tutorial that already fired once be
        // replayed. No production code path needs this - TryShow's once-only guarantee is otherwise
        // permanent for the session.
        public void ResetShown(TutorialId id) => shownTutorials.Remove(id);

        public void RestoreFromSaveData(IEnumerable<TutorialId> savedShownTutorials)
        {
            shownTutorials.Clear();
            if (savedShownTutorials == null) return;

            foreach (var id in savedShownTutorials)
            {
                shownTutorials.Add(id);
            }
        }
    }
}
