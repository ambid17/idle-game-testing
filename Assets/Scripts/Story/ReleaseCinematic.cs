using System.Collections;
using System.Collections.Generic;
using Atmosphere;
using Audio;
using Effects;
using Events;
using Player;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Story
{
    // The Release ending (GameDesignDoc "# Story & Endgame: The Seals"). The last Seal shatters, the
    // Bound wakes and can't hold what he is, and he throws the player up to the surface through a
    // portal before it all comes out of him: the sky goes dark, he rises over the horizon, and
    // bolts rain down until every building is gone. Then a white flash, the end card, and the
    // scene reloads to the main menu.
    //
    // Nothing here is saved. StoryManager.BeginRelease writes the save first (the player still at
    // the Vault, the choice un-made) and suspends saving, so Continue puts the player back in front
    // of the whole Seal - which is also why the wrecked buildings are never put back.
    public class ReleaseCinematic : MonoBehaviour
    {
        private const string AwakeningConversation = "Story.Release.Awakening";
        private const string LamentConversation = "Story.Release.Lament";
        // Above Atmosphere.BiomeGrading's per-biome Volumes and the death grading.
        private const float GradingVolumePriority = 20f;
        // Under StoryManager's flash canvas (40), so the flash hides the card coming up.
        private const int EndCardSortingOrder = 39;

        [SerializeField] private CinemachineCamera followCamera;
        [Tooltip("The Bound and the bolts are placed around what this camera is looking at.")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private PlayerPortalTravel playerTravel;
        [SerializeField] private SealChamber vault;
        [Tooltip("Where the Bound sets the player down on the surface.")]
        [SerializeField] private Transform surfaceArrivalPoint;
        [Tooltip("Every surface building. Ones that are still hidden (not yet revealed) are skipped.")]
        [SerializeField] private SpriteRenderer[] buildings;
        [SerializeField] private Sprite boundSprite;
        [SerializeField] private TMP_FontAsset endCardFont;

        [Header("The Seal breaks")]
        [SerializeField, Min(0f)] private float rumbleSeconds = 1.4f;
        [SerializeField, Min(0f)] private float rumbleForce = 0.45f;
        [SerializeField] private Color voidColor = new(0.75f, 0.3f, 1f, 1f);

        [Header("Surface")]
        [Tooltip("How far above the arrival point the camera looks, so the sky has room for the Bound.")]
        [SerializeField] private float cameraLift = 1.8f;
        [SerializeField, Min(0.01f)] private float skyDarkenSeconds = 2f;
        [SerializeField] private Color skyFilter = new(0.62f, 0.4f, 0.85f, 1f);
        [SerializeField] private float skyExposure = -0.7f;

        [Header("The Bound")]
        [Tooltip("World units tall.")]
        [SerializeField] private float boundHeight = 9f;
        [Tooltip("Where his middle ends up, relative to the camera's view centre.")]
        [SerializeField] private Vector2 boundOffset = new(0f, 1.2f);
        [SerializeField] private float boundRiseDistance = 4f;
        [SerializeField, Min(0.01f)] private float boundRiseSeconds = 3f;
        [Tooltip("Behind the buildings and the terrain.")]
        [SerializeField] private int boundSortingOrder = -10;
        [SerializeField] private Color boundGlowColor = new(0.75f, 0.3f, 1f, 0.5f);

        [Header("Destruction")]
        [Tooltip("Half the width of the strip of ground the bolts land on, around the camera.")]
        [SerializeField] private float strikeHalfWidth = 9f;
        [SerializeField] private float boltDropHeight = 11f;
        [SerializeField, Min(0.01f)] private float boltSeconds = 0.32f;
        [SerializeField] private Vector2 boltSize = new(0.45f, 3.2f);
        [Tooltip("Seconds between stray bolts at the start and at the height of it.")]
        [SerializeField] private Vector2 strayBoltInterval = new(0.5f, 0.12f);
        [SerializeField, Min(0f)] private float openingBarrageSeconds = 3f;
        [Tooltip("Seconds between one building being hit and the next.")]
        [SerializeField, Min(0f)] private float buildingStrikeInterval = 0.9f;
        [SerializeField, Min(0f)] private float impactShakeForce = 0.35f;
        [SerializeField] private Color fireColor = new(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private Color smokeColor = new(0.2f, 0.18f, 0.25f, 0.85f);
        [SerializeField, Min(0f)] private float wreckGravity = 14f;

        [Header("End card")]
        [SerializeField, Min(0f)] private float finalFlashInSeconds = 1.6f;
        [SerializeField, Min(0f)] private float finalFlashHoldSeconds = 1f;
        [SerializeField, Min(0f)] private float finalFlashOutSeconds = 2f;
        [SerializeField, Min(0.01f)] private float cardLineFadeSeconds = 1.2f;
        [SerializeField, Min(0f)] private float cardLineGapSeconds = 1.4f;

        private static StoryManager story => GameManager.StoryManager;

        private Transform cameraAnchor;
        private SpriteRenderer bound;
        private SpriteRenderer boundGlow;
        private Vector3 boundRestPosition;
        private Volume gradingVolume;
        private VolumeProfile gradingProfile;
        private readonly List<Vector3> fires = new();
        private string pendingConversation;
        private bool isPlaying;
        private bool barrage;
        private float groundY;

        private void Awake()
        {
            if (followCamera == null) Debug.LogError("ReleaseCinematic.followCamera is not assigned.");
            if (worldCamera == null) Debug.LogError("ReleaseCinematic.worldCamera is not assigned.");
            if (playerTravel == null) Debug.LogError("ReleaseCinematic.playerTravel is not assigned.");
            if (vault == null) Debug.LogError("ReleaseCinematic.vault is not assigned.");
            if (surfaceArrivalPoint == null) Debug.LogError("ReleaseCinematic.surfaceArrivalPoint is not assigned.");
            if (buildings == null || buildings.Length == 0) Debug.LogError("ReleaseCinematic.buildings is empty.");
            if (boundSprite == null) Debug.LogError("ReleaseCinematic.boundSprite is not assigned.");
            if (endCardFont == null) Debug.LogError("ReleaseCinematic.endCardFont is not assigned.");
        }

        private void Start()
        {
            cameraAnchor = new GameObject("Release Camera Anchor").transform;
            cameraAnchor.SetParent(transform, false);

            bound = new GameObject("The Bound").AddComponent<SpriteRenderer>();
            bound.transform.SetParent(transform, false);
            bound.sprite = boundSprite;
            bound.sortingOrder = boundSortingOrder;
            bound.transform.localScale = Vector3.one * (boundHeight / boundSprite.bounds.size.y);
            boundGlow = GlowSprites.CreateGlow(bound.transform, boundGlowColor, boundSprite.bounds.size.y * 1.6f);
            // Same order as him and a touch farther from the camera, so it draws just behind him
            // but still in front of the sky backdrop.
            boundGlow.sortingOrder = boundSortingOrder;
            boundGlow.transform.localPosition = Vector3.forward * 0.1f;
            bound.gameObject.SetActive(false);

            gradingProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var adjustments = gradingProfile.Add<ColorAdjustments>();
            adjustments.colorFilter.Override(skyFilter);
            adjustments.postExposure.Override(skyExposure);
            gradingVolume = gameObject.AddComponent<Volume>();
            gradingVolume.isGlobal = true;
            gradingVolume.priority = GradingVolumePriority;
            gradingVolume.weight = 0f;
            gradingVolume.sharedProfile = gradingProfile;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<DialogFinishedEvent>(OnDialogFinished);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DialogFinishedEvent>(OnDialogFinished);
        }

        private void OnDestroy()
        {
            if (gradingProfile != null) Destroy(gradingProfile);
        }

        private void OnDialogFinished(DialogFinishedEvent e)
        {
            if (e.ConversationId == pendingConversation) pendingConversation = null;
        }

        public void Play()
        {
            if (isPlaying)
            {
                Debug.LogError("ReleaseCinematic.Play: already playing.");
                return;
            }
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            isPlaying = true;
            story.SetEndingPlaying(true);
            GameManager.EventService.Dispatch<UICloseEvent>();
            InputBlocker.SetBlocked(true);
            story.BeginRelease();

            // ---- The Vault: the Seal breaks, and he wakes ----
            Vector3 sealPosition = vault.StonePosition;
            GameManager.AudioService.Play(SoundId.RockRumble);
            GameManager.CameraShake.Shake(Vector2.up * rumbleForce, rumbleSeconds);
            for (float t = 0f; t < rumbleSeconds; t += Time.deltaTime)
            {
                // Cracks of light leak out of it faster and faster.
                if (Random.value < Mathf.Lerp(0.1f, 0.9f, t / rumbleSeconds))
                {
                    GameManager.WorldEffects.Sparkle(sealPosition, Random.insideUnitCircle.normalized * Random.Range(2f, 6f), Random.Range(0.2f, 0.45f), 0.5f, voidColor);
                }
                yield return null;
            }

            vault.Break();
            GameManager.AudioService.Play(SoundId.Explosion);
            GameManager.WorldEffects.SparkleBurst(sealPosition, 40, 0.3f, 9f, voidColor);
            GameManager.WorldEffects.Puff(sealPosition, 8, 0.9f, smokeColor, Vector3.up * 0.5f);
            GameManager.CameraShake.Shake(Random.insideUnitCircle.normalized * (rumbleForce * 1.5f), 0.5f, CinemachineImpulseDefinition.ImpulseShapes.Explosion);
            StartCoroutine(GlowFlash(sealPosition, voidColor, 7f, 0.6f));
            yield return story.Flash(voidColor, 0.6f, 0f, 0.5f);

            yield return Converse(AwakeningConversation, story.Content.ReleaseAwakening);

            // ---- Up ----
            var originalTarget = followCamera.Target;
            Vector3 arrival = surfaceArrivalPoint.position;
            GameManager.AudioService.Play(SoundId.BuildingPortal);
            playerTravel.TryTravelTo(arrival, () =>
            {
                // The camera stops following the player and holds on the surface, looking up.
                var anchorTarget = originalTarget;
                anchorTarget.TrackingTarget = cameraAnchor;
                cameraAnchor.position = arrival + Vector3.up * cameraLift;
                followCamera.Target = anchorTarget;
                followCamera.PreviousStateIsValid = false;
                StartCoroutine(story.Flash(voidColor, 1f, 0f, 0.8f));
            });
            while (playerTravel.IsTraveling) yield return null;

            // ---- The surface ----
            float cellSize = GameManager.MapGenerationService.CellSize;
            groundY = GameManager.MapGenerationService.CellToWorldCenter(0, 0, 0).y + cellSize * 0.5f;

            GameManager.AudioService.Play(SoundId.RockRumble);
            GameManager.CameraShake.Shake(Vector2.up * rumbleForce, skyDarkenSeconds);
            for (float t = 0f; t < skyDarkenSeconds; t += Time.deltaTime)
            {
                gradingVolume.weight = t / skyDarkenSeconds;
                yield return null;
            }
            gradingVolume.weight = 1f;

            yield return RiseBound();

            barrage = true;
            StartCoroutine(StrayBolts());
            StartCoroutine(Burn());
            yield return new WaitForSeconds(openingBarrageSeconds);

            // Farthest from the player first, so the last one goes down right beside them.
            var targets = new List<SpriteRenderer>();
            foreach (var building in buildings)
            {
                if (building.gameObject.activeInHierarchy) targets.Add(building);
            }
            targets.Sort((a, b) => Mathf.Abs(b.transform.position.x - arrival.x).CompareTo(Mathf.Abs(a.transform.position.x - arrival.x)));

            foreach (var target in targets)
            {
                StartCoroutine(StrikeBuilding(target));
                yield return new WaitForSeconds(buildingStrikeInterval);
            }
            yield return new WaitForSeconds(1.2f);

            yield return Converse(LamentConversation, story.Content.ReleaseLament);

            // ---- The end ----
            GameManager.AudioService.Play(SoundId.Prestige);
            GameManager.CameraShake.Shake(Vector2.up * (rumbleForce * 1.5f), finalFlashInSeconds);
            StartCoroutine(SwellBoundGlow(finalFlashInSeconds));
            yield return story.Flash(Color.white, 0f, 1f, finalFlashInSeconds);
            barrage = false;

            BuildEndCard(out var lines, out var prompt);
            yield return new WaitForSecondsRealtime(finalFlashHoldSeconds);
            yield return story.Flash(Color.white, 1f, 0f, finalFlashOutSeconds);

            foreach (var line in lines)
            {
                yield return FadeText(line, cardLineFadeSeconds);
                yield return new WaitForSecondsRealtime(cardLineGapSeconds);
            }
            yield return FadeText(prompt, cardLineFadeSeconds * 0.5f, 0.6f);
            // Skip a frame so a key held through the last fade doesn't count.
            yield return null;
            while (!WasAnyKeyPressed()) yield return null;

            const float fadeOutSeconds = 0.6f;
            for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                foreach (var line in lines) line.alpha = 1f - t / fadeOutSeconds;
                prompt.alpha = 0.6f * (1f - t / fadeOutSeconds);
                yield return null;
            }

            // Back to the main menu; Continue loads the save from before the Seal broke.
            StoryManager.RewoundThisSession = true;
            InputBlocker.ResetForSceneReload();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Opens a conversation and waits for it to close. The destruction keeps going underneath.
        private IEnumerator Converse(string conversationId, string[] entries)
        {
            pendingConversation = conversationId;
            GameManager.EventService.Dispatch(new DialogRequestedEvent(conversationId, story.BuildLines(entries)));
            while (pendingConversation != null) yield return null;
        }

        // ---- The Bound ----

        private IEnumerator RiseBound()
        {
            Vector3 view = worldCamera.transform.position;
            boundRestPosition = new Vector3(view.x + boundOffset.x, view.y + boundOffset.y, 0f);
            Vector3 from = boundRestPosition + Vector3.down * boundRiseDistance;
            bound.transform.position = from;
            bound.gameObject.SetActive(true);

            GameManager.AudioService.Play(SoundId.BoundVoice);
            GameManager.CameraShake.Shake(Vector2.up * rumbleForce, boundRiseSeconds);
            Color glow = boundGlowColor;
            for (float t = 0f; t < boundRiseSeconds; t += Time.deltaTime)
            {
                float k = t / boundRiseSeconds;
                bound.transform.position = Vector3.Lerp(from, boundRestPosition, Easing.OutCubic(k));
                bound.color = new Color(1f, 1f, 1f, Mathf.Min(1f, k * 1.5f));
                boundGlow.color = new Color(glow.r, glow.g, glow.b, glow.a * k);
                yield return null;
            }
            bound.color = Color.white;
            boundGlow.color = glow;
        }

        private void Update()
        {
            if (!barrage) return;

            // He hangs there, breathing.
            float t = Time.time;
            bound.transform.position = boundRestPosition + Vector3.up * (Mathf.Sin(t * 0.9f) * 0.25f);
            boundGlow.transform.localScale = Vector3.one * (boundSprite.bounds.size.y * 1.6f * (1f + Mathf.Sin(t * 2.3f) * 0.08f));
        }

        private IEnumerator SwellBoundGlow(float seconds)
        {
            Color glow = boundGlowColor;
            float baseDiameter = boundSprite.bounds.size.y * 1.6f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                boundGlow.color = new Color(Mathf.Lerp(glow.r, 1f, k), Mathf.Lerp(glow.g, 1f, k), 1f, Mathf.Lerp(glow.a, 1f, k));
                boundGlow.transform.localScale = Vector3.one * (baseDiameter * Mathf.Lerp(1f, 3f, Easing.InQuad(k)));
                yield return null;
            }
        }

        // ---- Bolts ----

        // Bolts on random ground, quicker and quicker, until the final flash.
        private IEnumerator StrayBolts()
        {
            float elapsed = 0f;
            float rampSeconds = openingBarrageSeconds + buildings.Length * buildingStrikeInterval;
            while (barrage)
            {
                float x = worldCamera.transform.position.x + Random.Range(-strikeHalfWidth, strikeHalfWidth);
                StartCoroutine(Bolt(new Vector3(x, groundY, 0f), 1f));

                float wait = Mathf.Lerp(strayBoltInterval.x, strayBoltInterval.y, Mathf.Clamp01(elapsed / rampSeconds)) * Random.Range(0.6f, 1.4f);
                elapsed += wait;
                yield return new WaitForSeconds(wait);
            }
        }

        // A streak of light from the sky down to target, then the impact.
        private IEnumerator Bolt(Vector3 target, float scale)
        {
            Vector3 from = target + new Vector3(Random.Range(-3f, 3f), boltDropHeight, 0f);
            var streak = new GameObject("Bolt").AddComponent<SpriteRenderer>();
            streak.transform.SetParent(transform, false);
            streak.sprite = GlowSprites.RadialGlow;
            streak.sharedMaterial = GlowSprites.GlowMaterial;
            streak.color = Color.Lerp(voidColor, Color.white, 0.5f);
            streak.sortingOrder = GameManager.AtmosphereConfig.GlowSortingOrder;
            streak.transform.localScale = new Vector3(boltSize.x * scale, boltSize.y * scale, 1f);
            streak.transform.rotation = Quaternion.FromToRotation(Vector3.up, from - target);

            for (float t = 0f; t < boltSeconds; t += Time.deltaTime)
            {
                streak.transform.position = Vector3.Lerp(from, target, Easing.InQuad(t / boltSeconds));
                yield return null;
            }
            Destroy(streak.gameObject);

            Impact(target, scale);
        }

        private void Impact(Vector3 position, float scale)
        {
            GameManager.AudioService.Play(SoundId.Explosion);
            GameManager.WorldEffects.SparkleBurst(position, Mathf.RoundToInt(14 * scale), 0.2f, 6f * scale, voidColor);
            GameManager.WorldEffects.Puff(position, Mathf.RoundToInt(4 * scale), 0.7f * scale, fireColor, Vector3.up * 2f);
            GameManager.WorldEffects.Puff(position + Vector3.up * 0.3f, Mathf.RoundToInt(5 * scale), 0.9f * scale, smokeColor, Vector3.up * 1.2f);
            GameManager.WorldEffects.DustRing(position, 0.6f * scale, 8, 0.6f);
            GameManager.CameraShake.Shake(Random.insideUnitCircle.normalized * (impactShakeForce * scale), 0.3f, CinemachineImpulseDefinition.ImpulseShapes.Explosion);
            StartCoroutine(GlowFlash(position, voidColor, 4.5f * scale, 0.35f));
            fires.Add(position);
        }

        // A big bolt takes a building: it is blown off its foundations, tumbling and shrinking away.
        private IEnumerator StrikeBuilding(SpriteRenderer building)
        {
            Vector3 center = building.bounds.center;
            yield return Bolt(center, 2f);

            var wreck = building.transform;
            Vector3 position = wreck.position;
            Vector3 scale = wreck.localScale;
            var velocity = new Vector3(Random.Range(-5f, 5f), Random.Range(7f, 10f), 0f);
            float spin = Random.Range(140f, 300f) * (velocity.x < 0f ? 1f : -1f);
            fires.Add(new Vector3(center.x, groundY, 0f));

            const float seconds = 1.5f;
            float smokeTimer = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                velocity += Vector3.down * (wreckGravity * Time.deltaTime);
                position += velocity * Time.deltaTime;
                wreck.position = position;
                wreck.Rotate(0f, 0f, spin * Time.deltaTime);
                wreck.localScale = scale * (1f - Easing.InQuad(t / seconds));

                smokeTimer -= Time.deltaTime;
                if (smokeTimer <= 0f)
                {
                    smokeTimer = 0.06f;
                    GameManager.WorldEffects.Puff(building.bounds.center, 1, 0.8f, Random.value < 0.4f ? fireColor : smokeColor, Vector3.up * 0.8f);
                }
                yield return null;
            }
            wreck.localScale = Vector3.zero;
        }

        // Every impact keeps burning: embers and smoke rising from where it landed.
        private IEnumerator Burn()
        {
            var wait = new WaitForSeconds(0.08f);
            while (barrage)
            {
                // A handful per tick, however many fires there are.
                for (int i = 0; i < Mathf.Min(fires.Count, 6); i++)
                {
                    Vector3 fire = fires[Random.Range(0, fires.Count)] + new Vector3(Random.Range(-0.6f, 0.6f), 0.1f, 0f);
                    if (Random.value < 0.7f)
                    {
                        GameManager.WorldEffects.Sparkle(fire, new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(1.5f, 3.5f), 0f), Random.Range(0.14f, 0.3f), Random.Range(0.5f, 0.9f), fireColor);
                    }
                    else
                    {
                        GameManager.WorldEffects.Puff(fire, 1, 0.6f, smokeColor, Vector3.up * 1.6f);
                    }
                }
                yield return wait;
            }
        }

        // A glow that snaps on and swells as it fades.
        private IEnumerator GlowFlash(Vector3 position, Color color, float diameter, float seconds)
        {
            var glow = GlowSprites.CreateGlow(transform, color, diameter);
            glow.transform.position = position;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                glow.transform.localScale = Vector3.one * (diameter * Mathf.Lerp(0.5f, 1f, Easing.OutCubic(k)));
                glow.color = new Color(color.r, color.g, color.b, 1f - k);
                yield return null;
            }
            Destroy(glow.gameObject);
        }

        // ---- End card ----

        // A black screen with the closing lines stacked in its middle, every line still invisible.
        private void BuildEndCard(out List<TMP_Text> lines, out TMP_Text prompt)
        {
            var content = story.Content;

            var card = new GameObject("Release End Card");
            card.transform.SetParent(transform, false);
            var canvas = card.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = EndCardSortingOrder;
            var scaler = card.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = new GameObject("Background").AddComponent<Image>();
            background.transform.SetParent(card.transform, false);
            background.color = Color.black;
            Stretch(background.rectTransform);

            var column = new GameObject("Lines").AddComponent<VerticalLayoutGroup>();
            column.transform.SetParent(card.transform, false);
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            column.spacing = 34f;
            var columnRect = (RectTransform)column.transform;
            columnRect.anchorMin = new Vector2(0.5f, 0f);
            columnRect.anchorMax = new Vector2(0.5f, 1f);
            columnRect.sizeDelta = new Vector2(1300f, 0f);

            lines = new List<TMP_Text> { AddCardLine(column.transform, content.ReleaseEndTitle, 76f, voidColor) };
            foreach (string entry in content.ReleaseEndLines)
            {
                string text = entry == "{keystones}" ? story.KeystoneLine(content.ReleaseKeystoneOutcomes) : entry;
                if (!string.IsNullOrEmpty(text)) lines.Add(AddCardLine(column.transform, text, 32f, Color.white));
            }
            lines.Add(AddCardLine(column.transform, content.ReleaseEndClosing, 60f, Color.white));
            lines.Add(AddCardLine(column.transform, content.ReleaseEndThanks, 32f, Color.white));
            prompt = AddCardLine(column.transform, content.ReleaseEndContinuePrompt, 24f, Color.white);
        }

        private TMP_Text AddCardLine(Transform parent, string text, float size, Color color)
        {
            var label = new GameObject("Line").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.font = endCardFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.alpha = 0f;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static IEnumerator FadeText(TMP_Text label, float seconds, float alpha = 1f)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                label.alpha = alpha * t / seconds;
                yield return null;
            }
            label.alpha = alpha;
        }

        private static bool WasAnyKeyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            var gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }
    }
}
