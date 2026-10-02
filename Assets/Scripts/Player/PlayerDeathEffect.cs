using System.Collections;
using System.Collections.Generic;
using Atmosphere;
using Effects;
using Events;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Player
{
    // Plays the death beat at the death location, then hands off to DeathUI - decouples the death
    // screen's reveal from PlayerDiedEvent itself (see PlayerDeathMenuRequestedEvent) so this
    // component alone controls how long that delay is.
    // The real player sprite is hidden and a stand-in "corpse" (a copy of the current frame and the
    // accessories worn) is animated instead, so nothing here touches the player's own transform.
    // What happens to the corpse depends on the DeathReason:
    //  - Shatter (default, blast, fall): hit-stop and flash, then it breaks into scrap cut from the
    //    sprite, which tumbles, bounces off the ground and lies there.
    //  - Crush (crusher, falling rock): squashed flat, then the flattened scrap is squeezed out sideways.
    //  - Power-down (out of fuel): sputters smoke, goes dark and slumps nose-down.
    //  - Choke (gas): turns green and shudders in a green haze, then slumps nose-down.
    //  - Melt (lava): glows hot and sinks away in embers and smoke.
    // Once the beat is over the world drains of colour (a global Volume above the biome grading)
    // as the death screen comes up.
    // Whatever is left lying around fades out on PlayerRevivedEvent.
    public class PlayerDeathEffect : MonoBehaviour
    {
        private class Scrap
        {
            public Transform Transform;
            public Vector2 Velocity;
            public float Spin;
            public bool Resting;
        }

        // Above Atmosphere.BiomeGrading's per-biome Volumes.
        private const float GradingVolumePriority = 10f;
        private const float ScrapRadius = 0.08f;

        [Header("Timing")]
        [Tooltip("Brief freeze on the killing blow, before the robot breaks apart.")]
        [SerializeField, Min(0f)] private float hitStopSeconds = 0.07f;
        [Tooltip("Pause between the end of the death beat and the death screen opening.")]
        [SerializeField, Min(0f)] private float menuDelaySeconds = 0.45f;
        [Tooltip("How long the leftovers take to fade out on respawn.")]
        [SerializeField, Min(0.01f)] private float cleanupFadeSeconds = 0.35f;

        [Header("Slump (fuel, gas)")]
        [Tooltip("The sprite's outline is a loose hull around the art, so resting that on the ground leaves the robot floating - it sinks this much further (world units).")]
        [SerializeField, Min(0f)] private float slumpSink = 0.14f;

        [Header("Scrap")]
        [SerializeField, Min(1)] private int scrapColumns = 3;
        [SerializeField, Min(1)] private int scrapRows = 3;
        [SerializeField] private Vector2 scrapSpeedRange = new(2.5f, 5f);
        [SerializeField, Min(0f)] private float scrapGravity = 16f;
        [SerializeField, Range(0f, 1f)] private float scrapBounce = 0.3f;
        [Tooltip("Scrap, flashes and glows draw above terrain and the player.")]
        [SerializeField] private int sortingOrder = 6;

        [Header("Screen shake")]
        [SerializeField, Min(0f)] private float shakeForce = 0.35f;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.3f;

        [Header("Colours")]
        [SerializeField] private Color flashColor = new(0.7f, 1f, 1f, 0.9f);
        [SerializeField] private Color sparkColor = new(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color fireColor = new(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private Color smokeColor = new(0.25f, 0.25f, 0.28f, 0.8f);
        [SerializeField] private Color gasColor = new(0.55f, 0.95f, 0.35f, 0.7f);
        [SerializeField] private Color poweredDownTint = new(0.5f, 0.52f, 0.6f, 1f);

        [Header("World grading while dead")]
        [SerializeField, Range(-100f, 0f)] private float deadSaturation = -70f;
        [SerializeField] private float deadExposure = -0.2f;
        [SerializeField, Min(0.01f)] private float gradingFadeInSeconds = 0.7f;
        [SerializeField, Min(0.01f)] private float gradingFadeOutSeconds = 0.4f;

        private SpriteRenderer playerSprite;
        private PlayerController controller;
        private PlayerAccessories accessories;
        private float halfWidth;
        private float halfHeight;
        private int groundMask;

        private Transform corpseRoot;
        private SpriteRenderer corpseBody;
        private readonly List<SpriteRenderer> corpseRenderers = new();
        private readonly List<Scrap> scraps = new();
        private readonly List<Sprite> scrapSprites = new();
        private Coroutine deathRoutine;
        private Coroutine cleanupRoutine;
        private Coroutine gradingRoutine;
        private bool hitStopActive;
        private Volume gradingVolume;
        private VolumeProfile gradingProfile;

        private float Facing => corpseBody.flipX ? -1f : 1f;
        private Vector3 PlayerFeet => transform.position + Vector3.down * halfHeight;
        private Vector3 CorpseCenter => corpseRoot.position + Vector3.up * halfHeight;

        private void Awake()
        {
            playerSprite = GetComponent<SpriteRenderer>();
            controller = GetComponent<PlayerController>();
            accessories = GetComponent<PlayerAccessories>();
            groundMask = LayerMask.GetMask("Ground");
        }

        private void Start()
        {
            if (playerSprite == null) Debug.LogError($"{nameof(PlayerDeathEffect)} on {name} requires a SpriteRenderer.");
            if (controller == null) Debug.LogError($"{nameof(PlayerDeathEffect)} on {name} requires a PlayerController.");
            if (accessories == null) Debug.LogError($"{nameof(PlayerDeathEffect)} on {name} requires a PlayerAccessories.");

            var capsule = GetComponent<CapsuleCollider2D>();
            if (capsule == null) Debug.LogError($"{nameof(PlayerDeathEffect)} on {name} requires a CapsuleCollider2D.");
            halfWidth = capsule.size.x * 0.5f * Mathf.Abs(transform.lossyScale.x);
            halfHeight = capsule.size.y * 0.5f * Mathf.Abs(transform.lossyScale.y);

            gradingProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var adjustments = gradingProfile.Add<ColorAdjustments>();
            adjustments.saturation.Override(deadSaturation);
            adjustments.postExposure.Override(deadExposure);

            // Not parented to the player, like the other effect objects.
            var volumeObject = new GameObject("Death Grading");
            volumeObject.transform.SetParent(transform.parent, false);
            gradingVolume = volumeObject.AddComponent<Volume>();
            gradingVolume.isGlobal = true;
            gradingVolume.priority = GradingVolumePriority;
            gradingVolume.weight = 0f;
            gradingVolume.sharedProfile = gradingProfile;
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

            // A beat cut short (scene unload, player disabled) must not leave the game frozen or gray.
            StopAllCoroutines();
            ReleaseHitStop();
            deathRoutine = null;
            cleanupRoutine = null;
            gradingRoutine = null;
            if (gradingVolume != null) gradingVolume.weight = 0f;
            DestroyLeftovers();
        }

        private void OnDestroy()
        {
            if (gradingVolume != null) Destroy(gradingVolume.gameObject);
            if (gradingProfile != null) Destroy(gradingProfile);
        }

        private void Update() => UpdateScraps(Time.deltaTime);

        private void HandleDied(PlayerDiedEvent evt)
        {
            if (deathRoutine != null) StopCoroutine(deathRoutine);
            if (cleanupRoutine != null) StopCoroutine(cleanupRoutine);
            cleanupRoutine = null;
            ReleaseHitStop();
            DestroyLeftovers();

            BuildCorpse();
            playerSprite.enabled = false;
            deathRoutine = StartCoroutine(PlayThenRequestMenu(evt.Reason));
        }

        private void HandleRevived()
        {
            if (deathRoutine != null) StopCoroutine(deathRoutine);
            deathRoutine = null;
            ReleaseHitStop();

            playerSprite.enabled = true;
            FadeGrading(0f, gradingFadeOutSeconds);
            if (corpseRoot != null && cleanupRoutine == null) cleanupRoutine = StartCoroutine(FadeOutLeftovers());
        }

        private IEnumerator PlayThenRequestMenu(DeathReason reason)
        {
            switch (reason)
            {
                case DeathReason.OutOfFuel:
                    yield return PowerDown();
                    break;
                case DeathReason.GasPocket:
                    yield return Choke();
                    break;
                case DeathReason.Lava:
                    yield return Melt();
                    break;
                case DeathReason.Crusher:
                case DeathReason.FallingRock:
                    yield return Crush();
                    break;
                case DeathReason.Explosive:
                    yield return Shatter(speedScale: 1.8f, verticalScale: 1f, upwardKick: 3f, fireColor, sparks: 26, smokePuffs: 9, shakeScale: 1.4f);
                    break;
                case DeathReason.FallDamage:
                    // Splats outward along the ground rather than bursting upward.
                    GameManager.WorldEffects.DustRing(corpseRoot.position, halfWidth, 10, 0.4f);
                    yield return Shatter(speedScale: 1.1f, verticalScale: 0.45f, upwardKick: 1f, sparkColor, sparks: 12, smokePuffs: 3, shakeScale: 1f);
                    break;
                default:
                    yield return Shatter(speedScale: 1f, verticalScale: 1f, upwardKick: 2.5f, sparkColor, sparks: 16, smokePuffs: 5, shakeScale: 1f);
                    break;
            }

            // Only once the beat is over - its fire, gas and sparks need their colour.
            FadeGrading(1f, gradingFadeInSeconds);
            yield return new WaitForSeconds(menuDelaySeconds);
            deathRoutine = null;
            GameManager.EventService.Dispatch<PlayerDeathMenuRequestedEvent>();
        }

        // ---- Variants ----

        // sparkTint also colours the flash. verticalScale < 1 flattens the burst toward the ground.
        private IEnumerator Shatter(float speedScale, float verticalScale, float upwardKick, Color sparkTint, int sparks, int smokePuffs, float shakeScale)
        {
            Vector3 center = corpseBody.transform.position;
            StartCoroutine(Flash(center, Color.Lerp(flashColor, sparkTint, 0.5f), 3.2f * Mathf.Sqrt(speedScale)));
            yield return HitStop();

            BreakIntoScrap(center, speedScale, verticalScale, upwardKick);
            GameManager.WorldEffects.SparkleBurst(center, sparks, 0.15f, 5f * speedScale, sparkTint);
            GameManager.WorldEffects.Puff(center, smokePuffs, 0.6f, smokeColor, Vector3.up * 0.6f);
            GameManager.CameraShake.Shake(Random.insideUnitCircle.normalized * (shakeForce * shakeScale), shakeSeconds, CinemachineImpulseDefinition.ImpulseShapes.Explosion);

            // Long enough for the scrap to come down.
            yield return new WaitForSeconds(0.9f);
        }

        private IEnumerator Crush()
        {
            // Each piece is squashed toward the feet itself (not the root), so the scrap it becomes
            // can spin without shearing under a stretched parent.
            var pieces = new Transform[corpseRenderers.Count];
            var positions = new Vector3[pieces.Length];
            var scales = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i] = corpseRenderers[i].transform;
                positions[i] = pieces[i].localPosition;
                scales[i] = pieces[i].localScale;
            }

            const float squashSeconds = 0.1f;
            for (float t = 0f; t < squashSeconds + Time.deltaTime; t += Time.deltaTime)
            {
                float k = Easing.InQuad(Mathf.Min(1f, t / squashSeconds));
                var squash = new Vector3(Mathf.Lerp(1f, 1.5f, k), Mathf.Lerp(1f, 0.22f, k), 1f);
                for (int i = 0; i < pieces.Length; i++)
                {
                    pieces[i].localPosition = Vector3.Scale(positions[i], squash);
                    pieces[i].localScale = Vector3.Scale(scales[i], squash);
                }
                yield return null;
            }

            GameManager.WorldEffects.DustRing(corpseRoot.position, halfWidth * 1.5f, 10, 0.4f);
            GameManager.CameraShake.Shake(Vector2.down * (shakeForce * 1.2f), shakeSeconds);
            yield return HitStop();
            yield return new WaitForSeconds(0.18f);

            Vector3 center = corpseBody.transform.position;
            // Flat plates skid out along the ground; spinning freely they'd read as sticks.
            BreakIntoScrap(center, speedScale: 1.2f, verticalScale: 0.3f, upwardKick: 0.8f, spinScale: 0.12f);
            GameManager.WorldEffects.SparkleBurst(center, 12, 0.3f, 4f, sparkColor);
            yield return new WaitForSeconds(0.8f);
        }

        private IEnumerator PowerDown()
        {
            const float sputterSeconds = 0.8f;
            float puffTimer = 0f;
            for (float t = 0f; t < sputterSeconds; t += Time.deltaTime)
            {
                float k = t / sputterSeconds;
                // Lights stutter on and off as they dim; the body shivers less and less.
                float flicker = Mathf.Sin(t * 45f) > 0.2f ? 1f : 0.55f;
                var tint = Color.Lerp(Color.white, poweredDownTint, k) * flicker;
                tint.a = 1f;
                TintCorpse(tint);
                corpseRoot.position = PlayerFeet + Vector3.right * (Mathf.Sin(t * 70f) * 0.025f * (1f - k));

                puffTimer -= Time.deltaTime;
                if (puffTimer <= 0f)
                {
                    puffTimer = 0.2f;
                    GameManager.WorldEffects.Puff(CorpseCenter + Vector3.up * (halfHeight * 0.6f), 2, 0.35f, smokeColor, Vector3.up * 1.2f);
                }
                yield return null;
            }
            TintCorpse(poweredDownTint);

            yield return FallToGround();
            yield return Topple();
            GameManager.WorldEffects.SparkleBurst(CorpseCenter, 5, 0.1f, 2.5f, sparkColor);
            yield return new WaitForSeconds(0.25f);
        }

        private IEnumerator Choke()
        {
            const float shudderSeconds = 0.9f;
            var sickTint = new Color(gasColor.r, gasColor.g, gasColor.b, 1f);
            float puffTimer = 0f;
            for (float t = 0f; t < shudderSeconds; t += Time.deltaTime)
            {
                float k = t / shudderSeconds;
                TintCorpse(Color.Lerp(Color.white, sickTint, Mathf.Min(1f, k * 2.5f)));
                // Rocks on its feet, harder and faster as it goes.
                corpseRoot.SetPositionAndRotation(PlayerFeet, Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.Lerp(18f, 40f, k)) * Mathf.Lerp(3f, 11f, k)));

                puffTimer -= Time.deltaTime;
                if (puffTimer <= 0f)
                {
                    puffTimer = 0.16f;
                    GameManager.WorldEffects.Puff(CorpseCenter, 2, 0.55f, gasColor, Vector3.up * 0.5f);
                }
                yield return null;
            }
            corpseRoot.rotation = Quaternion.identity;

            yield return FallToGround();
            TintCorpse(Color.Lerp(sickTint, poweredDownTint, 0.55f));
            yield return Topple();
            GameManager.WorldEffects.Puff(CorpseCenter, 4, 0.5f, gasColor, Vector3.up * 0.4f);
            yield return new WaitForSeconds(0.25f);
        }

        private IEnumerator Melt()
        {
            var hot = new Color(fireColor.r, fireColor.g, fireColor.b, 1f);
            var charred = new Color(0.35f, 0.08f, 0.05f, 1f);
            var glow = GlowSprites.CreateGlow(corpseRoot, new Color(hot.r, hot.g, hot.b, 0f), 2.6f);
            glow.transform.localPosition = Vector3.up * halfHeight;
            glow.sortingOrder = sortingOrder;

            const float heatSeconds = 0.3f;
            for (float t = 0f; t < heatSeconds; t += Time.deltaTime)
            {
                float k = t / heatSeconds;
                corpseRoot.position = PlayerFeet;
                TintCorpse(Color.Lerp(Color.white, hot, k));
                glow.color = new Color(hot.r, hot.g, hot.b, 0.7f * k);
                yield return null;
            }

            // Sinks into its own feet: the root sits at the feet, so scaling it melts the body downward.
            const float meltSeconds = 1f;
            float emberTimer = 0f;
            float smokeTimer = 0f;
            for (float t = 0f; t < meltSeconds; t += Time.deltaTime)
            {
                float k = t / meltSeconds;
                corpseRoot.localScale = new Vector3(Mathf.Lerp(1f, 1.3f, k), Mathf.Lerp(1f, 0.12f, Easing.InQuad(k)), 1f);
                var tint = Color.Lerp(hot, charred, k);
                tint.a = 1f - Mathf.InverseLerp(0.6f, 1f, k);
                TintCorpse(tint);
                glow.color = new Color(hot.r, hot.g, hot.b, 0.7f * (1f - k));

                emberTimer -= Time.deltaTime;
                if (emberTimer <= 0f)
                {
                    emberTimer = 0.04f;
                    Vector3 position = corpseRoot.position + new Vector3(Random.Range(-halfWidth, halfWidth) * 1.3f, Random.Range(0f, halfHeight * 2f * (1f - k)), 0f);
                    GameManager.WorldEffects.Sparkle(position, new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1.2f, 3f), 0f), Random.Range(0.12f, 0.28f), Random.Range(0.4f, 0.8f), hot);
                }
                smokeTimer -= Time.deltaTime;
                if (smokeTimer <= 0f)
                {
                    smokeTimer = 0.18f;
                    GameManager.WorldEffects.Puff(corpseRoot.position + Vector3.up * (halfHeight * (1f - k)), 2, 0.5f, smokeColor, Vector3.up * 1.4f);
                }
                yield return null;
            }

            Destroy(glow.gameObject);
            foreach (var renderer in corpseRenderers) renderer.enabled = false;
        }

        // ---- Corpse ----

        // A copy of the player as they look right now, standing on a (unit-scale) root placed at their feet.
        private void BuildCorpse()
        {
            corpseRoot = new GameObject("Player Corpse").transform;
            corpseRoot.position = PlayerFeet;

            corpseBody = CopyRenderer(playerSprite, "Body", transform.position, transform.rotation, transform.lossyScale);
            foreach (var accessory in accessories.WornRenderers)
            {
                var piece = accessory.transform;
                CopyRenderer(accessory, accessory.name, piece.position, piece.rotation, piece.lossyScale);
            }
        }

        // localScale: relative to the corpse root.
        private SpriteRenderer CopyRenderer(SpriteRenderer source, string copyName, Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            var copy = new GameObject(copyName).AddComponent<SpriteRenderer>();
            copy.transform.SetParent(corpseRoot, false);
            copy.transform.SetPositionAndRotation(position, rotation);
            copy.transform.localScale = localScale;
            copy.sprite = source.sprite;
            copy.flipX = source.flipX;
            copy.color = source.color;
            copy.sharedMaterial = source.sharedMaterial;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder;
            corpseRenderers.Add(copy);
            return copy;
        }

        private void TintCorpse(Color color)
        {
            foreach (var renderer in corpseRenderers) renderer.color = color;
        }

        // The dead body keeps falling under the player's own physics (e.g. fuel ran out mid-air),
        // so the corpse rides it down before it does anything that needs the ground.
        private IEnumerator FallToGround()
        {
            const float maxSeconds = 3f;
            for (float t = 0f; t < maxSeconds && !controller.IsGrounded; t += Time.deltaTime)
            {
                corpseRoot.position = PlayerFeet;
                yield return null;
            }
            corpseRoot.position = PlayerFeet;
        }

        // Drops out of its hover and slumps nose-down the way it was facing, landing with a thud and
        // a small rebound. Only tilts a little: the robot is wider than tall with the cannon out
        // front, so tipped right over it would stand on its nose. How far it drops comes from the
        // art's outline once tilted (the robot floats inside its frame, above the collider's base),
        // so whatever ends up lowest - the nose - is what rests on the ground.
        private IEnumerator Topple()
        {
            const float restAngle = 20f;
            const float impactAngle = 26f;
            float facing = Facing;
            Vector3 start = corpseRoot.position;
            Vector3 center = corpseBody.transform.position;

            var rest = Quaternion.Euler(0f, 0f, -restAngle * facing);
            Vector3 lowest = Vector3.zero;
            foreach (var vertex in corpseBody.sprite.vertices)
            {
                Vector3 tilted = rest * new Vector3(vertex.x * facing, vertex.y, 0f);
                if (tilted.y < lowest.y) lowest = tilted;
            }
            Vector3 drop = Vector3.down * (center.y + lowest.y - start.y + slumpSink);

            const float fallSeconds = 0.3f;
            for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
            {
                float k = Easing.InQuad(t / fallSeconds);
                SetToppleAngle(start + drop * k, center + drop * k, -impactAngle * facing * k);
                yield return null;
            }

            GameManager.WorldEffects.DustRing(new Vector3(center.x + lowest.x, start.y, start.z), halfWidth, 6, 0.3f);
            GameManager.CameraShake.Shake(Vector2.down * (shakeForce * 0.4f), 0.15f);

            const float settleSeconds = 0.12f;
            for (float t = 0f; t < settleSeconds; t += Time.deltaTime)
            {
                SetToppleAngle(start + drop, center + drop, -Mathf.Lerp(impactAngle, restAngle, Easing.OutCubic(t / settleSeconds)) * facing);
                yield return null;
            }
            SetToppleAngle(start + drop, center + drop, -restAngle * facing);
        }

        // Places the corpse root (at rootPosition when upright) turned by angle about pivot.
        private void SetToppleAngle(Vector3 rootPosition, Vector3 pivot, float angle)
        {
            var rotation = Quaternion.Euler(0f, 0f, angle);
            corpseRoot.SetPositionAndRotation(pivot + rotation * (rootPosition - pivot), rotation);
        }

        // ---- Scrap ----

        // Swaps the corpse's body for a grid of pieces cut out of its sprite (at uneven cut lines,
        // over the opaque part of the frame only) and throws them, and whatever it was wearing, away
        // from center. The pieces keep the body's current scale, so a crushed robot sheds flat scrap.
        private void BreakIntoScrap(Vector3 center, float speedScale, float verticalScale, float upwardKick, float spinScale = 1f)
        {
            var sprite = corpseBody.sprite;
            float pixelsPerUnit = sprite.pixelsPerUnit;
            Vector2 min = sprite.vertices[0];
            Vector2 max = min;
            foreach (var vertex in sprite.vertices)
            {
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }

            // Cut lines in pixels within the sprite's rect.
            int[] cutsX = CutLines(sprite.pivot.x + min.x * pixelsPerUnit, sprite.pivot.x + max.x * pixelsPerUnit, scrapColumns, sprite.rect.width);
            int[] cutsY = CutLines(sprite.pivot.y + min.y * pixelsPerUnit, sprite.pivot.y + max.y * pixelsPerUnit, scrapRows, sprite.rect.height);

            var body = corpseBody.transform;
            for (int column = 0; column < scrapColumns; column++)
            {
                for (int row = 0; row < scrapRows; row++)
                {
                    int width = cutsX[column + 1] - cutsX[column];
                    int height = cutsY[row + 1] - cutsY[row];
                    if (width <= 0 || height <= 0) continue;

                    var rect = new Rect(sprite.rect.x + cutsX[column], sprite.rect.y + cutsY[row], width, height);
                    var pieceSprite = Sprite.Create(sprite.texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    scrapSprites.Add(pieceSprite);

                    var local = new Vector3(
                        (cutsX[column] + width * 0.5f - sprite.pivot.x) / pixelsPerUnit * Facing,
                        (cutsY[row] + height * 0.5f - sprite.pivot.y) / pixelsPerUnit,
                        0f);
                    var piece = CopyRenderer(corpseBody, "Scrap", body.TransformPoint(local), body.rotation, body.localScale);
                    piece.sprite = pieceSprite;
                    piece.sortingOrder = sortingOrder;
                    AddScrap(piece.transform, center, speedScale, verticalScale, upwardKick, spinScale);
                }
            }

            foreach (var renderer in corpseRenderers)
            {
                if (renderer == corpseBody || renderer.sprite == null || scrapSprites.Contains(renderer.sprite)) continue;
                // Accessories fly off whole.
                renderer.sortingOrder = sortingOrder;
                AddScrap(renderer.transform, center, speedScale, verticalScale, upwardKick, spinScale);
            }
            corpseBody.enabled = false;
        }

        // count + 1 increasing pixel positions from min to max, the inner ones nudged off the even
        // grid and snapped to whole art pixels (2 texels).
        private static int[] CutLines(float min, float max, int count, float limit)
        {
            var cuts = new int[count + 1];
            float cell = (max - min) / count;
            for (int i = 0; i <= count; i++)
            {
                float jitter = i == 0 || i == count ? 0f : Random.Range(-0.25f, 0.25f) * cell;
                cuts[i] = Mathf.Clamp(Mathf.RoundToInt((min + cell * i + jitter) * 0.5f) * 2, 0, (int)limit);
            }
            return cuts;
        }

        private void AddScrap(Transform piece, Vector3 center, float speedScale, float verticalScale, float upwardKick, float spinScale)
        {
            Vector2 away = piece.position - center;
            Vector2 direction = (away.normalized + Random.insideUnitCircle * 0.5f).normalized;
            Vector2 velocity = direction * (Random.Range(scrapSpeedRange.x, scrapSpeedRange.y) * speedScale);
            velocity.y = velocity.y * verticalScale + upwardKick * Random.Range(0.6f, 1.2f);
            scraps.Add(new Scrap
            {
                Transform = piece,
                Velocity = velocity,
                Spin = Random.Range(180f, 540f) * spinScale * (Random.value < 0.5f ? -1f : 1f),
            });
        }

        // Falls, bounces off the ground and comes to rest. Raycast along each step, so fast pieces
        // can't tunnel through a floor.
        private void UpdateScraps(float dt)
        {
            if (dt <= 0f) return;

            foreach (var scrap in scraps)
            {
                if (scrap.Resting) continue;

                scrap.Velocity += Vector2.down * (scrapGravity * dt);
                Vector2 position = scrap.Transform.position;
                Vector2 step = scrap.Velocity * dt;
                var hit = Physics2D.Raycast(position, step.normalized, step.magnitude + ScrapRadius, groundMask);

                // distance 0 = started inside the ground (crushed into a wall): let it work its way out.
                if (hit.collider != null && hit.distance > 0f)
                {
                    position = hit.point + hit.normal * ScrapRadius;
                    float intoSurface = Vector2.Dot(scrap.Velocity, hit.normal);
                    Vector2 along = scrap.Velocity - hit.normal * intoSurface;
                    scrap.Velocity = along * 0.6f - hit.normal * (intoSurface * scrapBounce);
                    scrap.Spin *= 0.5f;
                    scrap.Resting = hit.normal.y > 0.5f && scrap.Velocity.sqrMagnitude < 0.8f * 0.8f;
                }
                else
                {
                    position += step;
                }

                scrap.Transform.position = new Vector3(position.x, position.y, scrap.Transform.position.z);
                scrap.Transform.Rotate(0f, 0f, scrap.Spin * dt);
            }
        }

        // ---- Shared bits ----

        // A glow that snaps on and swells as it fades. Parented to the corpse so it's cleaned up with it.
        private IEnumerator Flash(Vector3 position, Color color, float diameter)
        {
            var glow = GlowSprites.CreateGlow(corpseRoot, color, diameter);
            glow.transform.position = position;
            glow.sortingOrder = sortingOrder + 1;

            const float seconds = 0.3f;
            for (float t = 0f; t < seconds && glow != null; t += Time.deltaTime)
            {
                float k = t / seconds;
                glow.transform.localScale = Vector3.one * (diameter * Mathf.Lerp(0.5f, 1f, Easing.OutCubic(k)));
                glow.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
                yield return null;
            }
            if (glow != null) Destroy(glow.gameObject);
        }

        private IEnumerator HitStop()
        {
            // Only freeze from normal speed - never stomp on anything else that changed timeScale.
            if (hitStopSeconds <= 0f || !Mathf.Approximately(Time.timeScale, 1f)) yield break;

            hitStopActive = true;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopSeconds);
            ReleaseHitStop();
        }

        private void ReleaseHitStop()
        {
            if (!hitStopActive) return;
            hitStopActive = false;
            Time.timeScale = 1f;
        }

        private void FadeGrading(float targetWeight, float seconds)
        {
            if (gradingRoutine != null) StopCoroutine(gradingRoutine);
            gradingRoutine = StartCoroutine(FadeGradingRoutine(targetWeight, seconds));
        }

        private IEnumerator FadeGradingRoutine(float targetWeight, float seconds)
        {
            float startWeight = gradingVolume.weight;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                gradingVolume.weight = Mathf.Lerp(startWeight, targetWeight, t / seconds);
                yield return null;
            }
            gradingVolume.weight = targetWeight;
            gradingRoutine = null;
        }

        private IEnumerator FadeOutLeftovers()
        {
            var renderers = corpseRoot.GetComponentsInChildren<SpriteRenderer>();
            var startAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) startAlphas[i] = renderers[i].color.a;

            for (float t = 0f; t < cleanupFadeSeconds; t += Time.deltaTime)
            {
                float k = 1f - t / cleanupFadeSeconds;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var color = renderers[i].color;
                    color.a = startAlphas[i] * k;
                    renderers[i].color = color;
                }
                yield return null;
            }

            cleanupRoutine = null;
            DestroyLeftovers();
        }

        private void DestroyLeftovers()
        {
            scraps.Clear();
            corpseRenderers.Clear();
            foreach (var sprite in scrapSprites) Destroy(sprite);
            scrapSprites.Clear();
            if (corpseRoot != null) Destroy(corpseRoot.gameObject);
            corpseRoot = null;
            corpseBody = null;
        }
    }
}
