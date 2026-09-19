using Buildings;
using Events;
using Persistence;
using UnityEngine;

namespace Economy
{
    // Keeps the Museum building hidden and non-interactable until the player finds their first
    // Artifact, then hands off to Buildings.BuildingRevealCinematicPlayer for the shared reveal
    // cinematic (player frozen, camera pans in, materializes through a portal, bottom-screen text,
    // click to continue). Mirrors Processing.ProcessingCenterRevealController's shape - each owns
    // only its own unlock trigger, building reference, and description text. Scene-placed singleton
    // for the same reason ProcessingCenterRevealController is - it needs an Inspector-wired
    // reference to the Museum building.
    public class MuseumRevealController : Singleton<MuseumRevealController>
    {
        [SerializeField] private GameObject museumBuilding;

        [SerializeField, TextArea]
        private string museumDescription =
            "The Museum is where you turn in Artifacts for Prestige Points, spent on permanent perks.";

        private bool hasFoundFirstArtifact;

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
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(OnArtifactCountChanged);
        }

        private void OnLoadCompleted()
        {
            hasFoundFirstArtifact = Wallet.Instance.ArtifactCount > 0 || PrestigePoints.Instance.Points > 0;
            museumBuilding.SetActive(hasFoundFirstArtifact);
        }

        private void OnArtifactCountChanged()
        {
            if (hasFoundFirstArtifact || !SaveService.Instance.HasLoadedData) return;
            if (Wallet.Instance.ArtifactCount <= 0) return;

            hasFoundFirstArtifact = true;
            BuildingRevealCinematicPlayer.Instance.Reveal(museumBuilding, museumDescription);
        }
    }
}
