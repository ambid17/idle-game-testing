using System.Linq;
using Buildings;
using Events;
using Tutorial;
using UnityEngine;

namespace Processing
{
    // Keeps the Processing Center building hidden and non-interactable until the player unlocks
    // their first processing recipe, then routes through
    // Tutorial.TutorialManager.TryShow(TutorialId.ProcessingReveal) for the persisted once-only
    // guarantee, and hands off to Buildings.BuildingRevealCinematicPlayer for the shared reveal
    // cinematic (player frozen, camera pans in, materializes through a portal, bottom-screen text,
    // click to continue) when that ShowTutorialEvent comes back around. Mirrors
    // Economy.MuseumRevealController's shape - each owns only its own unlock trigger and building
    // reference; the description text lives in GameManager.TutorialDatabase like every other
    // tutorial. Scene-placed singleton for the same reason MuseumRevealController is - it needs an
    // Inspector-wired reference to the Processing Center building.
    public class ProcessingCenterRevealController : Singleton<ProcessingCenterRevealController>
    {
        [SerializeField] private GameObject processingCenterBuilding;

        protected override void Initialize()
        {
            base.Initialize();
            if (processingCenterBuilding == null)
            {
                Debug.LogError("ProcessingCenterRevealController.processingCenterBuilding is not assigned.");
            }
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnLoadCompleted()
        {
            bool alreadyUnlocked = ProcessingManager.Instance.HasAnyRecipeUnlocked();
            // Self-heals saves from before TutorialManager tracked this moment, so the building
            // doesn't stay stuck hidden for players who already unlocked it.
            if (alreadyUnlocked) TutorialManager.Instance.TryShow(TutorialId.ProcessingReveal);
            processingCenterBuilding.SetActive(TutorialManager.Instance.HasShown(TutorialId.ProcessingReveal));
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            if (TutorialManager.Instance.HasShown(TutorialId.ProcessingReveal)) return;

            bool unlocksARecipe = GameManager.ProcessingRecipeDatabase.Recipes.Any(r => r.RequiredUpgrade == evt.Definition);
            if (!unlocksARecipe) return;

            TutorialManager.Instance.TryShow(TutorialId.ProcessingReveal);
        }

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.Entry.Id != TutorialId.ProcessingReveal) return;
            BuildingRevealCinematicPlayer.Instance.Reveal(processingCenterBuilding, evt.Entry.Body);
        }
    }
}
