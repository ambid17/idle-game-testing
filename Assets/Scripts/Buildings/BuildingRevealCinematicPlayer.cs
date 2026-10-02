using System;
using System.Collections;
using Atmosphere;
using Audio;
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
    // references to the shared camera, effect art, and cinematic text panel.
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

        [Header("Particles")]
        [Tooltip("Alpha-blended material (Sprites/Default) the effect sheets are drawn with.")]
        [SerializeField] private Material particleMaterial;
        [Tooltip("Dust puffs stacked vertically, one per row (Tools/Effects/make_reveal_fx.py).")]
        [SerializeField] private Texture2D dustSheet;
        [SerializeField, Min(1)] private int dustSheetRows = 4;
        [Tooltip("Sparkles stacked vertically, one per row (Tools/Effects/make_reveal_fx.py).")]
        [SerializeField] private Texture2D sparkleSheet;
        [SerializeField, Min(1)] private int sparkleSheetRows = 2;
        [Tooltip("Dust and sparkles draw above the buildings and the player.")]
        [SerializeField] private int particleSortingOrder = 5;
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

        private SpriteRenderer portalRenderer;
        private Transform cameraAnchor;
        private ParticleSystem dustSystem;
        private ParticleSystem sparkleSystem;

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
            if (particleMaterial == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.particleMaterial is not assigned.");
            }
            if (dustSheet == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.dustSheet is not assigned.");
            }
            if (sparkleSheet == null)
            {
                Debug.LogError("BuildingRevealCinematicPlayer.sparkleSheet is not assigned.");
            }
        }

        private void Start()
        {
            // The camera follows this instead of the building, which is busy falling out of the sky.
            cameraAnchor = new GameObject("Building Reveal Camera Anchor").transform;
            cameraAnchor.SetParent(transform, false);

            var portalObject = new GameObject("Building Reveal Portal");
            portalObject.transform.SetParent(transform, false);
            portalRenderer = portalObject.AddComponent<SpriteRenderer>();
            portalRenderer.sprite = portalSprite;
            // Glow diameter is in the portal's local units, where the sprite itself is bounds.size across.
            GlowSprites.CreateGlow(portalObject.transform, portalGlowColor, portalSprite.bounds.size.x * 1.5f);
            portalObject.SetActive(false);

            var dustMaterial = new Material(particleMaterial) { name = "Building Reveal Dust", mainTexture = dustSheet };
            dustSystem = CreateSystem("Building Reveal Dust", dustMaterial, dustSheetRows, 80, gravity: -0.03f);
            var dustSize = dustSystem.sizeOverLifetime;
            dustSize.enabled = true;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.25f)));
            // Puffs shoot out along the ground and quickly lose their speed, like a real dust ring.
            var dustDrag = dustSystem.limitVelocityOverLifetime;
            dustDrag.enabled = true;
            dustDrag.limit = 0.3f;
            dustDrag.dampen = 0.08f;
            FadeOutAtEnd(dustSystem, 0.45f);

            var sparkleMaterial = new Material(particleMaterial) { name = "Building Reveal Sparkles", mainTexture = sparkleSheet };
            sparkleSystem = CreateSystem("Building Reveal Sparkles", sparkleMaterial, sparkleSheetRows, 200, gravity: 0f);
            FadeOutAtEnd(sparkleSystem, 0.6f);
        }

        private void Update()
        {
            if (!portalRenderer.gameObject.activeSelf) return;

            portalRenderer.transform.Rotate(0f, 0f, portalSpinSpeed * Time.deltaTime);
            EmitPortalSparkles(Time.deltaTime);
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
            Bounds art = ArtBounds(buildingSprite.sprite, restScale);
            float bottomOffset = art.min.y;
            Vector3 groundCenter = restPosition + new Vector3(art.center.x, bottomOffset + groundLineOffset, 0f);

            portalRenderer.sharedMaterial = buildingSprite.sharedMaterial;
            portalRenderer.sortingLayerID = buildingSprite.sortingLayerID;
            portalRenderer.sortingOrder = buildingSprite.sortingOrder - 1;

            var originalTarget = followCamera.Target;
            var anchorTarget = originalTarget;
            anchorTarget.TrackingTarget = cameraAnchor;
            cameraAnchor.position = restPosition + Vector3.up * cameraLift;
            followCamera.Target = anchorTarget;

            yield return new WaitForSeconds(cameraPanInSeconds);

            GameManager.AudioService.Play(SoundId.BuildingPortal);
            yield return AnimatePortal(portalPosition, 0f, 1f, portalOpenSeconds, EaseOutBack);
            yield return new WaitForSeconds(portalChargeSeconds);

            // Tumbles out of the portal's centre, growing from nothing.
            buildingTransform.position = portalPosition;
            buildingTransform.localScale = Vector3.zero;
            buildingSprite.color = emergeTint * restColor;
            building.SetActive(true);
            EmitSparkleBurst(portalPosition, 24, portalDiameter * 0.15f, 5f);

            for (float t = 0f; t < emergeSeconds; t += Time.deltaTime)
            {
                float k = t / emergeSeconds;
                buildingTransform.localScale = restScale * Mathf.Max(0f, EaseOutBack(k));
                buildingTransform.rotation = restRotation * Quaternion.Euler(0f, 0f, emergeSpinDegrees * (1f - EaseOutCubic(k)));
                buildingSprite.color = Color.Lerp(emergeTint, Color.white, k) * restColor;
                yield return null;
            }
            buildingTransform.localScale = restScale;
            buildingTransform.rotation = restRotation;
            buildingSprite.color = restColor;

            yield return new WaitForSeconds(hangSeconds);

            // Drops, stretching as it picks up speed.
            for (float t = 0f; t < dropSeconds; t += Time.deltaTime)
            {
                float k = t / dropSeconds;
                float stretch = 1f + 0.12f * k;
                buildingTransform.position = Vector3.LerpUnclamped(portalPosition, restPosition, k * k);
                buildingTransform.localScale = new Vector3(restScale.x / stretch, restScale.y * stretch, restScale.z);
                yield return null;
            }

            GameManager.AudioService.Play(SoundId.BuildingLand);
            GameManager.CameraShake.Shake(Vector2.down * landingShakeForce, landingShakeSeconds);
            EmitLandingDust(groundCenter, art.extents.x);
            StartCoroutine(AnimatePortal(portalPosition, 1f, 0f, portalCloseSeconds, EaseInQuad));
            EmitSparkleBurst(portalPosition, 16, portalDiameter * 0.3f, 3f);

            // Squashes flat on impact and springs back, keeping its base planted on the ground.
            for (float t = 0f; t < settleSeconds; t += Time.deltaTime)
            {
                float k = t / settleSeconds;
                float squash = landingSquash * Mathf.Exp(-5f * k) * Mathf.Cos(k * Mathf.PI * 4f);
                SetSquash(buildingTransform, restPosition, restScale, bottomOffset, squash);
                yield return null;
            }
            buildingTransform.position = restPosition;
            buildingTransform.localScale = restScale;

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

        // squash > 0 flattens (shorter and wider), < 0 stretches. The pivot is the sprite's centre,
        // so the building is shifted to keep the bottom of its art where it rests.
        private static void SetSquash(Transform building, Vector3 restPosition, Vector3 restScale, float bottomOffset, float squash)
        {
            float vertical = 1f - squash;
            building.localScale = new Vector3(restScale.x * (1f + squash * 0.6f), restScale.y * vertical, restScale.z);
            building.position = restPosition + Vector3.up * (bottomOffset * (1f - vertical));
        }

        private static Bounds ArtBounds(Sprite sprite, Vector3 scale)
        {
            Vector2 min = sprite.vertices[0];
            Vector2 max = min;
            foreach (Vector2 vertex in sprite.vertices)
            {
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(Vector2.Scale(min, scale), Vector2.Scale(max, scale));
            return bounds;
        }

        private IEnumerator AnimatePortal(Vector3 position, float fromSize, float toSize, float seconds, Func<float, float> ease)
        {
            var portal = portalRenderer.transform;
            portal.position = position;
            portalRenderer.gameObject.SetActive(true);

            // Sprite bounds are in unscaled world units, so this maps "1" to portalDiameter.
            float unitScale = portalDiameter / portalSprite.bounds.size.x;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                portal.localScale = Vector3.one * (unitScale * Mathf.Max(0f, Mathf.LerpUnclamped(fromSize, toSize, ease(t / seconds))));
                yield return null;
            }
            portal.localScale = Vector3.one * (unitScale * toSize);
            if (toSize <= 0f) portalRenderer.gameObject.SetActive(false);
        }

        // Motes that appear around the rim and spiral into the portal's centre.
        private void EmitPortalSparkles(float deltaTime)
        {
            float expected = portalSparkleRate * deltaTime;
            int count = Mathf.FloorToInt(expected) + (UnityEngine.Random.value < expected % 1f ? 1 : 0);
            // Follows the portal's current size, so nothing spawns outside it while it opens/closes.
            // (Not renderer.bounds - that grows and shrinks as the sprite spins.)
            float radius = portalSprite.bounds.extents.x * portalRenderer.transform.localScale.x * UnityEngine.Random.Range(0.75f, 1.05f);
            Vector3 center = portalRenderer.transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                var tangent = new Vector2(direction.y, -direction.x);
                const float lifetime = 0.6f;
                Vector3 velocity = (-direction + tangent * 0.8f) * (radius / lifetime);
                EmitSparkle(center + (Vector3)(direction * radius), velocity, UnityEngine.Random.Range(0.18f, 0.4f), lifetime);
            }
        }

        private void EmitSparkleBurst(Vector3 center, int count, float radius, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 velocity = direction * (speed * UnityEngine.Random.Range(0.4f, 1f));
                EmitSparkle(center + (Vector3)(direction * radius), velocity, UnityEngine.Random.Range(0.25f, 0.55f), UnityEngine.Random.Range(0.4f, 0.8f));
            }
        }

        private void EmitSparkle(Vector3 position, Vector3 velocity, float size, float lifetime)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = Color.white,
                applyShapeToPosition = false,
            };
            sparkleSystem.Emit(emitParams, 1);
        }

        // A ring of puffs thrown out sideways from under the building, biggest and fastest at
        // its edges, plus a few that billow up in front of it.
        private void EmitLandingDust(Vector3 groundCenter, float halfWidth)
        {
            for (int i = 0; i < landingDustPuffs; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                bool billow = i % 5 == 0;
                float along = billow ? UnityEngine.Random.Range(0f, 0.8f) : UnityEngine.Random.Range(0.6f, 1.05f);
                Vector3 position = groundCenter + new Vector3(side * halfWidth * along, UnityEngine.Random.Range(0.05f, 0.35f), 0f);
                Vector3 velocity = billow
                    ? new Vector3(side * UnityEngine.Random.Range(0.3f, 1.2f), UnityEngine.Random.Range(1.5f, 3f), 0f)
                    : new Vector3(side * UnityEngine.Random.Range(2.5f, 6.5f), UnityEngine.Random.Range(0.2f, 1.6f), 0f);
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity,
                    startSize = UnityEngine.Random.Range(0.6f, 1.25f),
                    startLifetime = UnityEngine.Random.Range(0.7f, 1.3f),
                    startColor = Color.white,
                    rotation = UnityEngine.Random.Range(-20f, 20f),
                    angularVelocity = side * UnityEngine.Random.Range(20f, 90f),
                    applyShapeToPosition = false,
                };
                dustSystem.Emit(emitParams, 1);
            }
        }

        // Emission is driven by hand (Emit); each particle shows one random row of a vertical sheet.
        private ParticleSystem CreateSystem(string systemName, Material material, int sheetRows, int maxParticles, float gravity)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 1;
            sheet.numTilesY = sheetRows;
            sheet.animation = ParticleSystemAnimationType.SingleRow;
            sheet.rowMode = ParticleSystemAnimationRowMode.Random;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = particleSortingOrder;

            system.Play();
            return system;
        }

        // Full opacity until fadeStart, then out to 0.
        private static void FadeOutAtEnd(ParticleSystem system, float fadeStart)
        {
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }

        private static float EaseInQuad(float t) => t * t;

        private static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
