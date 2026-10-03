using System.Collections;
using Audio;
using Events;
using UnityEngine;

namespace Player
{
    // Shared portal trip to the Depot, used by the Depot Recall ability and the Portal power-up:
    // a portal opens on the player and sucks them in (shrink + spin into its center), closes,
    // then reopens at depotArrivalPoint and spits them back out. Respawning after a death plays
    // just the arrival half at the spawn point. The player is out of physics and
    // input for the whole trip (PlayerController.SetInPortal). One runtime-built SpriteRenderer is
    // reused for both ends. Dying mid-trip (shouldn't happen with physics off, but a DoT could)
    // cancels it and restores the player so the normal respawn takes over.
    [RequireComponent(typeof(PlayerController))]
    public class PlayerPortalTravel : MonoBehaviour
    {
        [Tooltip("Where the player lands - a point in front of the Depot building.")]
        [SerializeField] private Transform depotArrivalPoint;
        [SerializeField] private Sprite portalSprite;
        [Tooltip("Portal diameter in world units at full size.")]
        [SerializeField] private float portalDiameter = 1.8f;
        [Tooltip("Degrees per second; negative spins clockwise, matching the art's spiral.")]
        [SerializeField] private float portalSpinSpeed = -360f;

        [Header("Timing")]
        [SerializeField] private float portalOpenSeconds = 0.3f;
        [SerializeField] private float suckInSeconds = 0.55f;
        [SerializeField] private float portalCloseSeconds = 0.2f;
        [Tooltip("Gap between the two ends while the camera pans over.")]
        [SerializeField] private float transitSeconds = 0.35f;
        [SerializeField] private float spitOutSeconds = 0.4f;
        [Tooltip("How far the player turns while being sucked in / spat out.")]
        [SerializeField] private float playerSpinDegrees = 720f;

        private PlayerController playerController;
        private SpriteRenderer playerSprite;
        private SpriteRenderer portalRenderer;
        private Coroutine travelRoutine;
        private Vector3 playerBaseScale;
        private Quaternion playerBaseRotation;
        private bool diedSinceLastRevive;

        public bool IsTraveling => travelRoutine != null;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerSprite = GetComponent<SpriteRenderer>();
            playerBaseScale = transform.localScale;
            playerBaseRotation = transform.rotation;
        }

        private void Start()
        {
            if (depotArrivalPoint == null) Debug.LogError($"{nameof(PlayerPortalTravel)} on {name} is missing depotArrivalPoint.");
            if (portalSprite == null) Debug.LogError($"{nameof(PlayerPortalTravel)} on {name} is missing portalSprite.");
            if (playerSprite == null) Debug.LogError($"{nameof(PlayerPortalTravel)} on {name} requires a SpriteRenderer.");

            // Unparented - the player shrinks and spins during the trip and the portal mustn't.
            // Drawn just behind the player in its sorting layer and with its material, so it
            // lights the same way.
            var portalObject = new GameObject("TeleportPortal");
            portalRenderer = portalObject.AddComponent<SpriteRenderer>();
            portalRenderer.sprite = portalSprite;
            portalRenderer.sharedMaterial = playerSprite.sharedMaterial;
            portalRenderer.sortingLayerID = playerSprite.sortingLayerID;
            portalRenderer.sortingOrder = playerSprite.sortingOrder - 1;
            portalObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Add<PlayerRevivedEvent>(HandleRevived);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Remove<PlayerRevivedEvent>(HandleRevived);
        }

        private void OnDestroy()
        {
            if (portalRenderer != null) Destroy(portalRenderer.gameObject);
        }

        private void Update()
        {
            if (portalRenderer.gameObject.activeSelf)
            {
                portalRenderer.transform.Rotate(0f, 0f, portalSpinSpeed * Time.deltaTime);
            }
        }

        // False (and nothing happens) if a trip is already underway.
        public bool TryTravelToDepot()
        {
            if (IsTraveling) return false;
            travelRoutine = StartCoroutine(TravelRoutine(depotArrivalPoint.position, null));
            return true;
        }

        // The same trip to anywhere (the story's endings). onTeleported runs the moment the player
        // has been moved, while both portals are shut - the place to cut the camera over.
        public bool TryTravelTo(Vector3 destination, System.Action onTeleported)
        {
            if (IsTraveling) return false;
            travelRoutine = StartCoroutine(TravelRoutine(destination, onTeleported));
            return true;
        }

        private IEnumerator TravelRoutine(Vector3 destination, System.Action onTeleported)
        {
            Vector3 origin = transform.position;
            playerController.SetInPortal(true);

            yield return AnimatePortal(origin, 0f, 1f, portalOpenSeconds, EaseOutBack);
            yield return AnimatePlayer(1f, 0f, suckInSeconds, EaseInQuad);
            yield return AnimatePortal(origin, 1f, 0f, portalCloseSeconds, EaseInQuad);

            playerController.TeleportTo(destination);
            onTeleported?.Invoke();
            yield return new WaitForSeconds(transitSeconds);

            yield return AnimatePortal(destination, 0f, 1f, portalOpenSeconds, EaseOutBack);
            yield return AnimatePlayer(0f, 1f, spitOutSeconds, EaseOutBack);
            yield return AnimatePortal(destination, 1f, 0f, portalCloseSeconds, EaseInQuad);

            EndTravel();
        }

        private IEnumerator AnimatePortal(Vector3 position, float fromSize, float toSize, float seconds, System.Func<float, float> ease)
        {
            var portal = portalRenderer.transform;
            portal.position = position;
            portalRenderer.gameObject.SetActive(true);

            // Sprite bounds are in unscaled world units, so this maps "1" to portalDiameter.
            float unitScale = portalDiameter / portalSprite.bounds.size.x;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                portal.localScale = Vector3.one * (unitScale * Mathf.LerpUnclamped(fromSize, toSize, ease(t / seconds)));
                yield return null;
            }
            portal.localScale = Vector3.one * (unitScale * toSize);
            if (toSize <= 0f) portalRenderer.gameObject.SetActive(false);
        }

        // Shrinks (or grows) the player about its pivot, which sits on the portal's center, while
        // spinning a full playerSpinDegrees.
        private IEnumerator AnimatePlayer(float fromSize, float toSize, float seconds, System.Func<float, float> ease)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                transform.localScale = playerBaseScale * Mathf.Max(0f, Mathf.LerpUnclamped(fromSize, toSize, ease(k)));
                transform.rotation = playerBaseRotation * Quaternion.Euler(0f, 0f, playerSpinDegrees * k);
                yield return null;
            }
            transform.localScale = playerBaseScale * toSize;
            transform.rotation = playerBaseRotation;
        }

        private void HandleDied(PlayerDiedEvent evt)
        {
            diedSinceLastRevive = true;
            if (!IsTraveling) return;
            StopCoroutine(travelRoutine);
            EndTravel();
        }

        // Respawning after a death arrives through a portal at the spawn point - the spit-out half
        // of a trip. PlayerRevivedEvent also fires on prestige and from dev tools, which just
        // reset the player in place.
        private void HandleRevived()
        {
            if (!diedSinceLastRevive) return;
            diedSinceLastRevive = false;
            if (IsTraveling) return;

            playerController.SetInPortal(true);
            transform.localScale = Vector3.zero;
            travelRoutine = StartCoroutine(ArrivalRoutine());
        }

        private IEnumerator ArrivalRoutine()
        {
            // PlayerController moves the player to the spawn point on the same event, possibly
            // after this listener - and the camera needs a moment to pan over from the death site.
            yield return new WaitForSeconds(transitSeconds);
            Vector3 destination = transform.position;

            GameManager.AudioService.Play(SoundId.RespawnPortal);
            yield return AnimatePortal(destination, 0f, 1f, portalOpenSeconds, EaseOutBack);
            yield return AnimatePlayer(0f, 1f, spitOutSeconds, EaseOutBack);
            GameManager.WorldEffects.SparkleBurst(destination, 14, 0.3f, 3.5f);
            yield return AnimatePortal(destination, 1f, 0f, portalCloseSeconds, EaseInQuad);

            EndTravel();
        }

        private void EndTravel()
        {
            transform.localScale = playerBaseScale;
            transform.rotation = playerBaseRotation;
            portalRenderer.gameObject.SetActive(false);
            playerController.SetInPortal(false);
            travelRoutine = null;
        }

        private static float EaseInQuad(float t) => t * t;

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
