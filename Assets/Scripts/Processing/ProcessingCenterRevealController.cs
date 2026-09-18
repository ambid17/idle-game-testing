using System.Collections;
using System.Linq;
using Events;
using Player;
using Unity.Cinemachine;
using UnityEngine;

namespace Processing
{
    // Keeps the Processing Center building hidden and non-interactable until the player unlocks
    // their first processing recipe, then plays a one-time cinematic (player frozen, camera pans
    // to the building, it drops from the sky and lands) the first time the Market is closed after
    // that unlock. Scene-placed singleton (mirrors Tutorial.TutorialManager) since it needs
    // Inspector-wired references to the building and the follow camera.
    public class ProcessingCenterRevealController : Singleton<ProcessingCenterRevealController>
    {
        [SerializeField] private GameObject processingCenterBuilding;
        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private float dropHeight = 20f;
        [SerializeField] private float flyDuration = 1.5f;
        [SerializeField] private float cameraPanInSeconds = 1f;
        [SerializeField] private float landingPauseSeconds = 0.5f;
        [SerializeField] private float cameraPanOutSeconds = 1f;
        [SerializeField] private AnimationCurve flyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private bool hasUnlockedFirstRecipe;
        private bool pendingReveal;

        protected override void Initialize()
        {
            base.Initialize();
            if (processingCenterBuilding == null)
            {
                Debug.LogError("ProcessingCenterRevealController.processingCenterBuilding is not assigned.");
            }
            if (followCamera == null)
            {
                Debug.LogError("ProcessingCenterRevealController.followCamera is not assigned.");
            }
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<MarketPanelClosedEvent>(OnMarketClosed);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<MarketPanelClosedEvent>(OnMarketClosed);
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
            pendingReveal = true;
        }

        private void OnMarketClosed()
        {
            if (!pendingReveal) return;
            pendingReveal = false;
            StartCoroutine(RevealSequence());
        }

        private IEnumerator RevealSequence()
        {
            InputBlocker.SetBlocked(true);

            var originalTarget = followCamera.Target;
            var buildingTarget = originalTarget;
            buildingTarget.TrackingTarget = processingCenterBuilding.transform;
            followCamera.Target = buildingTarget;

            yield return new WaitForSeconds(cameraPanInSeconds);

            Vector3 restPosition = processingCenterBuilding.transform.position;
            Vector3 startPosition = restPosition + Vector3.up * dropHeight;
            processingCenterBuilding.transform.position = startPosition;
            processingCenterBuilding.SetActive(true);

            float elapsed = 0f;
            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = flyCurve.Evaluate(Mathf.Clamp01(elapsed / flyDuration));
                processingCenterBuilding.transform.position = Vector3.LerpUnclamped(startPosition, restPosition, t);
                yield return null;
            }
            processingCenterBuilding.transform.position = restPosition;

            yield return new WaitForSeconds(landingPauseSeconds);

            followCamera.Target = originalTarget;

            yield return new WaitForSeconds(cameraPanOutSeconds);

            InputBlocker.SetBlocked(false);
        }
    }
}
