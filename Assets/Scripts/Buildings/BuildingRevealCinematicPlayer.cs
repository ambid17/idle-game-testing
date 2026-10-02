using System;
using System.Collections;
using Audio;
using Effects;
using Player;
using Unity.Cinemachine;
using UI.Panels;
using UnityEngine;

namespace Buildings
{
    // Shared "delivered through a portal" reveal cinematic: player frozen, camera pans to the
    // building site, a portal (the same art as Player.PlayerPortalTravel's) tears open in the sky
    // above it, the building tumbles out, drops and plops onto the ground in a dust cloud, the
    // portal closes, then bottom-screen text explains the building before the player clicks to
    // continue and the camera pans back.
    // Used by Economy.MuseumRevealController, Processing.ProcessingCenterRevealController and
    // Automation.ControlCenterRevealController -
    // each of those only owns its own unlock trigger, its building reference, and its description
    // text; this is the one place the animation/timings/text-panel/InputBlocker plumbing lives.
    // Scene-placed singleton (mirrors Tutorial.TutorialManager) since it needs Inspector-wired
    // references to the shared camera, portal art, and cinematic text panel.
    public class BuildingRevealCinematicPlayer : Singleton<BuildingRevealCinematicPlayer>
    {
        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private BuildingRevealTextUI cinematicText;

        [Header("Portal")]
        [SerializeField] private Sprite portalSprite;
        [Tooltip("Portal diameter in world units at full size - wide enough for a building to fit through.")]
        [SerializeField] private float portalDiameter = 4f;
        [Tooltip("How far above the building's resting position the portal opens.")]
        [SerializeField] private float portalHeight = 3.2f;
        [Tooltip("Degrees per second; negative spins clockwise, matching the art's spiral.")]
        [SerializeField] private float portalSpinSpeed = -240f;
        [SerializeField] private Color portalGlowColor = new(0.75f, 0.3f, 1f, 0.55f);
        [Tooltip("Tint the building leaves the portal with, fading to its own colours as it emerges.")]
        [SerializeField] private Color emergeTint = new(0.8f, 0.55f, 1f, 1f);

        [SerializeField, Min(0)] private int landingDustPuffs = 26;
        [Tooltip("Sparkles per second drawn into the portal while it is open.")]
        [SerializeField, Min(0f)] private float portalSparkleRate = 28f;

        [Header("Timing")]
        [SerializeField] private float cameraPanInSeconds = 0.75f;
        [SerializeField] private float portalOpenSeconds = 0.5f;
        [Tooltip("Beat with the portal open and crackling before the building comes through.")]
        [SerializeField] private float portalChargeSeconds = 0.7f;
        [SerializeField] private float emergeSeconds = 0.55f;
        [Tooltip("The building hangs in front of the portal for a moment before it drops.")]
        [SerializeField] private float hangSeconds = 0.18f;
        [SerializeField] private float dropSeconds = 0.38f;
        [SerializeField] private float settleSeconds = 0.6f;
        [SerializeField] private float portalCloseSeconds = 0.3f;
        [SerializeField] private float afterLandingSeconds = 0.4f;
        [SerializeField] private float cameraPanOutSeconds = 1f;
        [SerializeField] private float textPromptDelaySeconds = 2f;

        [Header("Landing")]
        [Tooltip("Degrees the building turns as it tumbles out of the portal.")]
        [SerializeField] private float emergeSpinDegrees = 200f;
        [Tooltip("How much the building flattens on impact (0.25 = 25% shorter, and wider to match).")]
        [SerializeField, Range(0f, 0.6f)] private float landingSquash = 0.26f;
        [Tooltip("The art's lowest pixels sit below the visible ground line by this much (world units) - dust is raised by it.")]
        [SerializeField] private float groundLineOffset = 0f;
        [SerializeField, Min(0f)] private float landingShakeForce = 0.55f;
        [SerializeField, Min(0f)] private float landingShakeSeconds = 0.4f;
        [Tooltip("How far above the building the camera aims while the portal is open, so both fit on screen.")]
        [SerializeField] private float cameraLift = 1.4f;

        private CinematicPortal portal;
        private Transform cameraAnchor;

        protected override void Initialize()
        {
            base.Initialize();
            if (followCamera == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.followCamera is not assigned.");
            }
            if (cinematicText == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.cinematicText is not assigned.");
            }
            if (portalSprite == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.portalSprite is not assigned.");
            }
        }

        private void Start()
        {
            // The camera follows this instead of the building, which is busy falling out of the sky.
            cameraAnchor = new GameObject("Building Reveal Camera Anchor").transform;
            cameraAnchor.SetParent(transform, false);

            portal = CinematicPortal.Create(transform, "Building Reveal Portal", portalSprite, portalDiameter, portalSpinSpeed, portalGlowColor, portalSparkleRate);
        }

        // Triggers are responsible for their own hidden-until-unlocked bookkeeping (SetActive(false)
        // by default, flipped true on load if already unlocked) - this just plays the cinematic that
        // reveals an already-hidden building.
        // afterMaterialized: optional extra beat played once the building has landed and before
        // the text appears (e.g. the Control Center opening its doors).
        public void Reveal(GameObject building, string description, Func<IEnumerator> afterMaterialized = null)
        {
            var buildingSprite = building.GetComponent<SpriteRenderer>();
            if (buildingSprite == null)
            {
                Debug.LogError($"BuildingRevealCinematicPlayer.Reveal: '{building.name}' has no SpriteRenderer.");
                return;
            }

            StartCoroutine(RevealSequence(building, buildingSprite, description, afterMaterialized));
        }

        private IEnumerator RevealSequence(GameObject building, SpriteRenderer buildingSprite, string description, Func<IEnumerator> afterMaterialized)
        {
            // Waits out whatever panel/modal is open (e.g. the Market panel a recipe was just bought
            // in, or the FirstArtifact world tutorial popup) since every panel/modal already sets
            // InputBlocker while open - see Player.InputBlocker's own comment. Tutorial modals don't
            // block input, so also wait out any open modal (e.g. the Automatons tutorial that fires
            // alongside the Control Center reveal) rather than playing the cinematic underneath it.
            yield return new WaitUntil(() => !InputBlocker.IsBlocked && !UI.ModalTracker.IsAnyModalOpen);

            InputBlocker.SetBlocked(true);

            var buildingTransform = building.transform;
            Vector3 restPosition = buildingTransform.position;
            Vector3 restScale = buildingTransform.localScale;
            Quaternion restRotation = buildingTransform.rotation;
            Color restColor = buildingSprite.color;
            Vector3 portalPosition = restPosition + Vector3.up * portalHeight;

            // The sprite's tight mesh gives where the art actually ends, since the texture has
            // transparent padding around the building. Pivot-relative, in world units.
            Bounds art = BuildingDrop.ArtBounds(buildingSprite.sprite, restScale);
            float bottomOffset = art.min.y;
            Vector3 groundCenter = restPosition + new Vector3(art.center.x, bottomOffset + groundLineOffset, 0f);

            portal.Renderer.sharedMaterial = buildingSprite.sharedMaterial;
            portal.Renderer.sortingLayerID = buildingSprite.sortingLayerID;
            portal.Renderer.sortingOrder = buildingSprite.sortingOrder - 1;

            var originalTarget = followCamera.Target;
            var anchorTarget = originalTarget;
            anchorTarget.TrackingTarget = cameraAnchor;
            cameraAnchor.position = restPosition + Vector3.up * cameraLift;
            followCamera.Target = anchorTarget;

            yield return new WaitForSeconds(cameraPanInSeconds);

            GameManager.AudioService.Play(SoundId.BuildingPortal);
            yield return portal.Animate(portalPosition, 0f, 1f, portalOpenSeconds, Easing.OutBack);
            yield return new WaitForSeconds(portalChargeSeconds);

            // Tumbles out of the portal's centre, growing from nothing.
            buildingTransform.position = portalPosition;
            buildingTransform.localScale = Vector3.zero;
            buildingSprite.color = emergeTint * restColor;
            building.SetActive(true);
            GameManager.WorldEffects.SparkleBurst(portalPosition, 24, portalDiameter * 0.15f, 5f);

            for (float t = 0f; t < emergeSeconds; t += Time.deltaTime)
            {
                float k = t / emergeSeconds;
                buildingTransform.localScale = restScale * Mathf.Max(0f, Easing.OutBack(k));
                buildingTransform.rotation = restRotation * Quaternion.Euler(0f, 0f, emergeSpinDegrees * (1f - Easing.OutCubic(k)));
                buildingSprite.color = Color.Lerp(emergeTint, Color.white, k) * restColor;
                yield return null;
            }
            buildingTransform.localScale = restScale;
            buildingTransform.rotation = restRotation;
            buildingSprite.color = restColor;

            yield return new WaitForSeconds(hangSeconds);

            yield return BuildingDrop.Fall(buildingTransform, portalPosition, restPosition, restScale, dropSeconds);

            GameManager.AudioService.Play(SoundId.BuildingLand);
            GameManager.CameraShake.Shake(Vector2.down * landingShakeForce, landingShakeSeconds);
            GameManager.WorldEffects.DustRing(groundCenter, art.extents.x, landingDustPuffs);
            StartCoroutine(portal.Animate(portalPosition, 1f, 0f, portalCloseSeconds, Easing.InQuad));
            GameManager.WorldEffects.SparkleBurst(portalPosition, 16, portalDiameter * 0.3f, 3f);

            yield return BuildingDrop.Settle(buildingTransform, restPosition, restScale, bottomOffset, landingSquash, settleSeconds);

            // Bring the building back to the middle of the screen now the portal is gone.
            cameraAnchor.position = restPosition;
            yield return new WaitForSeconds(afterLandingSeconds);

            if (afterMaterialized != null) yield return afterMaterialized();

            bool textDismissed = false;
            cinematicText.Show(description, textPromptDelaySeconds, () => textDismissed = true);
            yield return new WaitUntil(() => textDismissed);

            followCamera.Target = originalTarget;

            yield return new WaitForSeconds(cameraPanOutSeconds);

            InputBlocker.SetBlocked(false);
        }
    }
}
