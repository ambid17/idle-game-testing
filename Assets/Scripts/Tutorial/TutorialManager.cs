using System.Collections.Generic;
using Economy;
using Events;
using Interaction;
using Persistence;
using Player;
using UnityEngine;

namespace Tutorial
{
    // Drives the tutorial popup system: listens for the trigger events for first-Artifact-mined and
    // first-open-of-each-building-UI, and - the first time only, tracked by TutorialId and persisted
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
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnLoadCompleted()
        {
            TryShow(TutorialId.CoreGoal);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            // attempt to show a tutorial for the building type interacted with
            var id = ToTutorialId(evt.Type);
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
