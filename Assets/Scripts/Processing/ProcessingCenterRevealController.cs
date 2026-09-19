using System.Linq;
using Buildings;
using Events;
using UnityEngine;

namespace Processing
{
    // Keeps the Processing Center building hidden and non-interactable until the player unlocks
    // their first processing recipe, then hands off to Buildings.BuildingRevealCinematicPlayer for
    // the shared reveal cinematic (player frozen, camera pans in, materializes through a portal,
    // bottom-screen text, click to continue). Mirrors Economy.MuseumRevealController's shape - each
    // owns only its own unlock trigger, building reference, and description text; the cinematic
    // player itself already waits out whatever panel the unlock happened in (e.g. the Market panel
    // an upgrade was just bought in) before it starts, so no panel-close event is needed here.
    // Scene-placed singleton for the same reason MuseumRevealController is - it needs an
    // Inspector-wired reference to the Processing Center building.
    public class ProcessingCenterRevealController : Singleton<ProcessingCenterRevealController>
    {
        [SerializeField] private GameObject processingCenterBuilding;

        [SerializeField, TextArea]
        private string processingCenterDescription =
            "The Processing Center turns raw ore into refined goods you can sell for more at the Market.";

        private bool hasUnlockedFirstRecipe;

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
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        private void OnLoadCompleted()
        {
            hasUnlockedFirstRecipe = ProcessingManager.Instance.HasAnyRecipeUnlocked();
            processingCenterBuilding.SetActive(hasUnlockedFirstRecipe);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            if (hasUnlockedFirstRecipe) return;

            bool unlocksARecipe = GameManager.ProcessingRecipeDatabase.Recipes.Any(r => r.RequiredUpgrade == evt.Definition);
            if (!unlocksARecipe) return;

            hasUnlockedFirstRecipe = true;
            BuildingRevealCinematicPlayer.Instance.Reveal(processingCenterBuilding, processingCenterDescription);
        }
    }
}
