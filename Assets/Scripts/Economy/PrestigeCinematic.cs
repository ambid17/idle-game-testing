using System;
using System.Collections;
using System.Collections.Generic;
using Audio;
using Effects;
using Events;
using Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Economy
{
    // The prestige "portal collapse": a giant portal opens in the sky, every building on the
    // surface and then the player stretch and spiral into it, the screen flashes white - the
    // actual reset (PrestigeManager.ExecutePrestige) happens under the flash - and the buildings
    // plop back down out of the portal onto the fresh world, one after another.
    // UI.RunModifierPickUI plays this instead of calling ExecutePrestige directly; the dev panel's
    // instant prestige still skips it. Scene-placed singleton since it needs Inspector-wired
    // references to the camera, the buildings and the player.
    public class PrestigeCinematic : Singleton<PrestigeCinematic>
    {
        [SerializeField] private CinemachineCamera followCamera;
        [Tooltip("The portal opens in the middle of what this camera is looking at.")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private PlayerController player;
        [Tooltip("Every surface building. Ones that are still hidden (not yet revealed) are skipped.")]
        [SerializeField] private SpriteRenderer[] buildings;

        [Header("Portal")]
        [SerializeField] private Sprite portalSprite;
        [SerializeField] private float portalDiameter = 5.5f;
        [Tooltip("How far above the middle of the screen the portal sits.")]
        [SerializeField] private float portalLift = 1.6f;
        [SerializeField] private float portalSpinSpeed = -300f;
        [SerializeField] private Color portalGlowColor = new(0.75f, 0.3f, 1f, 0.6f);
        [SerializeField, Min(0f)] private float portalSparkleRate = 45f;

        [Header("Collapse")]
        [SerializeField] private float portalOpenSeconds = 0.6f;
        [SerializeField] private float portalChargeSeconds = 0.5f;
        [Tooltip("How long each building takes to spiral into the portal.")]
        [SerializeField] private float suckSeconds = 0.75f;
        [Tooltip("Delay between one building starting to go and the next.")]
        [SerializeField] private float suckStagger = 0.2f;
        [Tooltip("Degrees each thing swirls around the portal on its way in.")]
        [SerializeField] private float swirlDegrees = 200f;
        [SerializeField] private float flashInSeconds = 0.3f;

        [Header("Rebuild")]
        [Tooltip("The screen stays white this long while the new world is generated.")]
        [SerializeField] private float flashHoldSeconds = 0.3f;
        [SerializeField] private float flashOutSeconds = 0.6f;
        [SerializeField] private float emergeSeconds = 0.22f;
        [SerializeField] private float dropSeconds = 0.34f;
        [SerializeField] private float minimumDropHeight = 3f;
        [SerializeField] private float settleSeconds = 0.5f;
        [SerializeField] private float dropStagger = 0.16f;
        [SerializeField, Range(0f, 0.6f)] private float landingSquash = 0.22f;
        [SerializeField, Min(0)] private int landingDustPuffs = 16;
        [SerializeField, Min(0f)] private float landingShakeForce = 0.3f;
        [SerializeField] private float portalCloseSeconds = 0.35f;

        private struct Rest
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private CinematicPortal portal;
        private Transform cameraAnchor;
        private Image flash;

        // Tutorial.TutorialManager holds the "new run" popup until this is over.
        public static bool IsPlaying { get; private set; }

        protected override void Initialize()
        {
            base.Initialize();
            if (followCamera == null) Debug.LogError("PrestigeCinematic.followCamera is not assigned.");
            if (worldCamera == null) Debug.LogError("PrestigeCinematic.worldCamera is not assigned.");
            if (player == null) Debug.LogError("PrestigeCinematic.player is not assigned.");
            if (portalSprite == null) Debug.LogError("PrestigeCinematic.portalSprite is not assigned.");
            if (buildings == null || buildings.Length == 0) Debug.LogError("PrestigeCinematic.buildings is empty.");
        }

        private void Start()
        {
            cameraAnchor = new GameObject("Prestige Camera Anchor").transform;
            cameraAnchor.SetParent(transform, false);

            portal = CinematicPortal.Create(transform, "Prestige Portal", portalSprite, portalDiameter, portalSpinSpeed, portalGlowColor, portalSparkleRate);
            // Behind the buildings and the player, which fly into it.
            portal.Renderer.sortingOrder = 0;

            var canvasObject = new GameObject("Prestige Flash Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            flash = new GameObject("Flash").AddComponent<Image>();
            flash.transform.SetParent(canvasObject.transform, false);
            flash.raycastTarget = false;
            var flashRect = flash.rectTransform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            SetFlash(0f);
        }

        private void OnDestroy()
        {
            IsPlaying = false;
        }

        // executePrestige: the actual reset, run once the screen is fully white.
        public void Play(Action executePrestige)
        {
            if (IsPlaying)
            {
                Debug.LogError("PrestigeCinematic.Play: already playing.");
                return;
            }
            StartCoroutine(Sequence(executePrestige));
        }

        private IEnumerator Sequence(Action executePrestige)
        {
            IsPlaying = true;
            // The Museum panel this was launched from is still open.
            GameManager.EventService.Dispatch<UICloseEvent>();
            InputBlocker.SetBlocked(true);

            // Hold the camera where it is rather than following a player who is about to be
            // sucked up into the sky.
            var originalTarget = followCamera.Target;
            var anchorTarget = originalTarget;
            anchorTarget.TrackingTarget = cameraAnchor;
            cameraAnchor.position = player.transform.position;
            followCamera.Target = anchorTarget;

            Vector3 portalPosition = PortalPosition();
            GameManager.AudioService.Play(SoundId.BuildingPortal);
            yield return portal.Animate(portalPosition, 0f, 1f, portalOpenSeconds, Easing.OutBack);
            GameManager.CameraShake.Shake(Vector2.up * 0.2f, portalChargeSeconds);
            yield return new WaitForSeconds(portalChargeSeconds);

            // Left to right, then the player last.
            var rests = new List<Rest>();
            foreach (var building in buildings)
            {
                if (building.gameObject.activeInHierarchy) rests.Add(Capture(building.transform));
            }
            rests.Sort((a, b) => a.Position.x.CompareTo(b.Position.x));

            foreach (var rest in rests)
            {
                StartCoroutine(SuckIn(rest, portalPosition));
                yield return new WaitForSeconds(suckStagger);
            }

            Rest playerRest = Capture(player.transform);
            player.SetInPortal(true);
            StartCoroutine(SuckIn(playerRest, portalPosition));
            yield return new WaitForSeconds(suckSeconds - flashInSeconds);
            yield return Flash(0f, 1f, flashInSeconds);

            // Under the flash: buildings stay hidden (scale 0) at their resting places until they
            // drop back in; the player is put back together before the reset teleports them to spawn.
            foreach (var rest in rests)
            {
                rest.Transform.SetPositionAndRotation(rest.Position, rest.Rotation);
                rest.Transform.localScale = Vector3.zero;
            }
            player.transform.SetPositionAndRotation(playerRest.Position, playerRest.Rotation);
            player.transform.localScale = playerRest.Scale;
            player.SetInPortal(false);

            // A failed reset must not leave the game frozen behind a white screen.
            try
            {
                executePrestige();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // Cut straight to the spawn point instead of panning there behind the flash.
            cameraAnchor.position = player.transform.position;
            followCamera.PreviousStateIsValid = false;
            yield return new WaitForSecondsRealtime(flashHoldSeconds);

            // The camera is over the spawn point now, so the portal follows it.
            portalPosition = PortalPosition();
            portal.transform.position = portalPosition;
            StartCoroutine(Flash(1f, 0f, flashOutSeconds));
            yield return new WaitForSeconds(flashOutSeconds * 0.4f);

            for (int i = 0; i < rests.Count; i++)
            {
                var drop = StartCoroutine(DropIn(rests[i], portalPosition));
                if (i < rests.Count - 1) yield return new WaitForSeconds(dropStagger);
                else yield return drop;
            }

            GameManager.WorldEffects.SparkleBurst(portalPosition, 24, portalDiameter * 0.3f, 4f);
            yield return portal.Animate(portalPosition, 1f, 0f, portalCloseSeconds, Easing.InQuad);

            followCamera.Target = originalTarget;
            InputBlocker.SetBlocked(false);
            IsPlaying = false;
        }

        private Vector3 PortalPosition()
        {
            Vector3 view = worldCamera.transform.position;
            return new Vector3(view.x, view.y + portalLift, 0f);
        }

        private static Rest Capture(Transform target) => new()
        {
            Transform = target,
            Position = target.position,
            Rotation = target.rotation,
            Scale = target.localScale,
        };

        // Swirls around the portal on a tightening spiral, spinning and shrinking to nothing.
        private IEnumerator SuckIn(Rest rest, Vector3 portalPosition)
        {
            Vector3 offset = rest.Position - portalPosition;
            for (float t = 0f; t < suckSeconds; t += Time.deltaTime)
            {
                float k = Easing.InQuad(t / suckSeconds);
                rest.Transform.position = portalPosition + Quaternion.Euler(0f, 0f, swirlDegrees * k) * offset * (1f - k);
                rest.Transform.rotation = rest.Rotation * Quaternion.Euler(0f, 0f, swirlDegrees * 2f * k);
                rest.Transform.localScale = rest.Scale * (1f - k);
                yield return null;
            }
            rest.Transform.localScale = Vector3.zero;
        }

        // Pops out of the portal, falls to its resting place and lands in a puff of dust.
        private IEnumerator DropIn(Rest rest, Vector3 portalPosition)
        {
            var building = rest.Transform;
            // Straight down onto its spot, from the portal's height (or a building's height up,
            // if the camera - and so the portal - happens to be looking below the surface).
            float fromY = Mathf.Max(portalPosition.y, rest.Position.y + minimumDropHeight);
            Vector3 from = new Vector3(rest.Position.x, fromY, rest.Position.z);
            building.SetPositionAndRotation(from, rest.Rotation);
            for (float t = 0f; t < emergeSeconds; t += Time.deltaTime)
            {
                building.localScale = rest.Scale * Mathf.Max(0f, Easing.OutBack(t / emergeSeconds));
                yield return null;
            }

            yield return BuildingDrop.Fall(building, from, rest.Position, rest.Scale, dropSeconds);

            Bounds art = BuildingDrop.ArtBounds(building.GetComponent<SpriteRenderer>().sprite, rest.Scale);
            GameManager.AudioService.Play(SoundId.BuildingLand);
            GameManager.CameraShake.Shake(Vector2.down * landingShakeForce, 0.25f);
            GameManager.WorldEffects.DustRing(rest.Position + new Vector3(art.center.x, art.min.y, 0f), art.extents.x, landingDustPuffs);

            yield return BuildingDrop.Settle(building, rest.Position, rest.Scale, art.min.y, landingSquash, settleSeconds);
        }

        // Unscaled time: it must still fade if something froze the clock.
        private IEnumerator Flash(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFlash(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetFlash(to);
        }

        private void SetFlash(float alpha)
        {
            flash.color = new Color(1f, 1f, 1f, alpha);
            flash.gameObject.SetActive(alpha > 0f);
        }
    }
}
