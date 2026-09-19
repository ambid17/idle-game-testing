using Buildings;
using Events;
using Persistence;
using Tutorial;
using UnityEngine;

namespace Economy
{
    // Keeps the Museum building hidden and non-interactable until the player finds their first
    // Artifact, then routes through Tutorial.TutorialManager.TryShow(TutorialId.MuseumReveal) for the
    // persisted once-only guarantee, and hands off to Buildings.BuildingRevealCinematicPlayer for the
    // shared reveal cinematic (player frozen, camera pans in, materializes through a portal,
    // bottom-screen text, click to continue) when that ShowTutorialEvent comes back around. Mirrors
    // Processing.ProcessingCenterRevealController's shape - each owns only its own unlock trigger and
    // building reference; the description text lives in GameManager.TutorialDatabase like every other
    // tutorial. Scene-placed singleton for the same reason ProcessingCenterRevealController is - it
    // needs an Inspector-wired reference to the Museum building.
    public class MuseumRevealController : Singleton<MuseumRevealController>
    {
        [SerializeField] private GameObject museumBuilding;

        protected override void Initialize()
        {
            base.Initialize();
            if (museumBuilding == null)
            {
                Debug.LogError("MuseumRevealController.museumBuilding is not assigned.");
            }
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Add<ArtifactCountChangedEvent>(OnArtifactCountChanged);
            GameManager.EventService.Add<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(OnArtifactCountChanged);
            GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnLoadCompleted()
        {
            bool alreadyUnlocked = Wallet.Instance.ArtifactCount > 0 || PrestigePoints.Instance.Points > 0;
            // Self-heals saves from before TutorialManager tracked this moment, so the building
            // doesn't stay stuck hidden for players who already unlocked it.
            if (alreadyUnlocked) TutorialManager.Instance.TryShow(TutorialId.MuseumReveal);
            museumBuilding.SetActive(TutorialManager.Instance.HasShown(TutorialId.MuseumReveal));
        }

        private void OnArtifactCountChanged()
        {
            if (!SaveService.Instance.HasLoadedData) return;
            if (Wallet.Instance.ArtifactCount <= 0 && PrestigePoints.Instance.Points <= 0) return;

            TutorialManager.Instance.TryShow(TutorialId.MuseumReveal);
        }

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.Entry.Id != TutorialId.MuseumReveal) return;
            BuildingRevealCinematicPlayer.Instance.Reveal(museumBuilding, evt.Entry.Body);
        }
    }
}
