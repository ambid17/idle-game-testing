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
    // from GameManager.TutorialDatabase.
    //
    // Both UI.Panels.TutorialModalUI (screen overlay) and UI.Panels.WorldTutorialPopupUI (world
    // popup) listen for the same ShowTutorialEvent and each decide from WorldPosition whether it's
    // theirs to show - see ShowTutorialEvent's own comment in Events.cs.
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
            GameManager.EventService.Add<ArtifactCountChangedEvent>(OnArtifactCountChanged);
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(OnArtifactCountChanged);
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnLoadCompleted()
        {
            TryShow(TutorialId.CoreGoal);
        }

        private void OnArtifactCountChanged()
        {
            // ensure no artifacts have ever been gained
            if (Wallet.Instance.ArtifactCount <= 0 && PrestigePoints.Instance.Points <= 0 || !SaveService.Instance.HasLoadedData) return;

            Vector3? anchor = playerController != null ? playerController.transform.position + new Vector3(0, 2, 0) : null;
            TryShow(TutorialId.FirstArtifact, anchor);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
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

        private void TryShow(TutorialId id, Vector3? worldPosition = null)
        {
            if (shownTutorials.Contains(id)) return;

            var database = GameManager.TutorialDatabase;
            if (database == null || !database.TryGet(id, out var entry))
            {
                Debug.LogError($"TutorialManager: no TutorialEntry found for {id}.");
                return;
            }

            shownTutorials.Add(id);
            GameManager.EventService.Dispatch(new ShowTutorialEvent(entry, worldPosition));
        }

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
