using System.Collections;
using Player;
using Unity.Cinemachine;
using UI.Panels;
using UnityEngine;

namespace Buildings
{
    // Shared "materialize through a portal" reveal cinematic: player frozen, camera pans to the
    // building, a portal effect plays while the building's sprite fades in, then bottom-screen
    // text explains the building before the player clicks to continue and the camera pans back.
    // Used by both Economy.MuseumRevealController and Processing.ProcessingCenterRevealController -
    // each of those only owns its own unlock trigger, its building reference, and its description
    // text; this is the one place the animation/timings/text-panel/InputBlocker plumbing lives.
    // Scene-placed singleton (mirrors Tutorial.TutorialManager) since it needs Inspector-wired
    // references to the shared camera, portal effect, and cinematic text panel.
    public class BuildingRevealCinematicPlayer : Singleton<BuildingRevealCinematicPlayer>
    {
        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private ParticleSystem portalEffect;
        [SerializeField] private BuildingRevealTextUI cinematicText;

        [SerializeField] private float cameraPanInSeconds = 1f;
        [SerializeField] private float portalWarmupSeconds = 0.75f;
        [SerializeField] private float materializeDuration = 1.5f;
        [SerializeField] private float portalCooldownSeconds = 0.5f;
        [SerializeField] private float cameraPanOutSeconds = 1f;
        [SerializeField] private float textPromptDelaySeconds = 3f;
        [SerializeField] private AnimationCurve materializeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        protected override void Initialize()
        {
            base.Initialize();
            if (followCamera == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.followCamera is not assigned.");
            }
            if (portalEffect == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.portalEffect is not assigned.");
            }
            if (cinematicText == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.cinematicText is not assigned.");
            }
        }

        // Triggers are responsible for their own hidden-until-unlocked bookkeeping (SetActive(false)
        // by default, flipped true on load if already unlocked) - this just plays the cinematic that
        // reveals an already-hidden building.
        public void Reveal(GameObject building, string description)
        {
            var buildingSprite = building.GetComponent<SpriteRenderer>();
            if (buildingSprite == null)
            {
                Debug.LogError($"BuildingRevealCinematicPlayer.Reveal: '{building.name}' has no SpriteRenderer.");
                return;
            }

            StartCoroutine(RevealSequence(building, buildingSprite, description));
        }

        private IEnumerator RevealSequence(GameObject building, SpriteRenderer buildingSprite, string description)
        {
            // Waits out whatever panel/modal is open (e.g. the Market panel a recipe was just bought
            // in, or the FirstArtifact world tutorial popup) since every panel/modal already sets
            // InputBlocker while open - see Player.InputBlocker's own comment.
            yield return new WaitUntil(() => !InputBlocker.IsBlocked);

            InputBlocker.SetBlocked(true);

            var originalTarget = followCamera.Target;
            var buildingTarget = originalTarget;
            buildingTarget.TrackingTarget = building.transform;
            followCamera.Target = buildingTarget;

            yield return new WaitForSeconds(cameraPanInSeconds);

            portalEffect.transform.position = building.transform.position;
            portalEffect.Play();

            yield return new WaitForSeconds(portalWarmupSeconds);

            building.SetActive(true);
            SetAlpha(buildingSprite, 0f);

            float elapsed = 0f;
            while (elapsed < materializeDuration)
            {
                elapsed += Time.deltaTime;
                float t = materializeCurve.Evaluate(Mathf.Clamp01(elapsed / materializeDuration));
                SetAlpha(buildingSprite, t);
                yield return null;
            }
            SetAlpha(buildingSprite, 1f);

            yield return new WaitForSeconds(portalCooldownSeconds);

            portalEffect.Stop();

            bool textDismissed = false;
            cinematicText.Show(description, textPromptDelaySeconds, () => textDismissed = true);
            yield return new WaitUntil(() => textDismissed);

            followCamera.Target = originalTarget;

            yield return new WaitForSeconds(cameraPanOutSeconds);

            InputBlocker.SetBlocked(false);
        }

        private static void SetAlpha(SpriteRenderer sprite, float alpha)
        {
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }
    }
}
