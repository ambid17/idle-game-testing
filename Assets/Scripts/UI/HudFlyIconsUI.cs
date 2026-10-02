using System.Collections;
using System.Collections.Generic;
using Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    // Icons that fly across the screen into the HUD's counters, drawn on their own overlay canvas
    // above every panel:
    //  - Selling (ore at the Depot, goods at the Exchange, critters at the shop) fountains coins
    //    out of the button that was pressed and into the dollars counter. A sale made out in the
    //    world (Deposit & Sell at the Depot) fountains them from there (HudCoinBurstRequestedEvent).
    //  - A found artifact's tablet flies from the world into the artifact counter
    //    (HudIconFlyRequestedEvent, from Player.DigFeedback).
    // Each arrival bumps the counter. Cosmetic only - the wallet is credited before any of this.
    public class HudFlyIconsUI : MonoBehaviour
    {
        [SerializeField] private RectTransform dollarsTarget;
        [SerializeField] private RectTransform artifactTarget;
        [SerializeField] private Sprite coinSprite;
        [Tooltip("Projects world positions (a mined artifact) onto the screen.")]
        [SerializeField] private Camera worldCamera;

        [Header("Coins")]
        [SerializeField, Min(1)] private int minCoins = 5;
        [SerializeField, Min(1)] private int maxCoins = 16;
        [Tooltip("Coin size as a fraction of the screen height.")]
        [SerializeField] private float coinSize = 0.045f;
        [Tooltip("How far coins scatter from the button before flying off, as a fraction of the screen height.")]
        [SerializeField] private float burstRadius = 0.11f;
        [SerializeField] private float burstSeconds = 0.28f;
        [SerializeField] private float flySeconds = 0.45f;
        [Tooltip("Delay between one coin leaving for the counter and the next.")]
        [SerializeField] private float coinStagger = 0.035f;

        [Header("Artifact")]
        [Tooltip("Tablet size as a fraction of the screen height.")]
        [SerializeField] private float artifactSize = 0.075f;
        [SerializeField] private float artifactFlySeconds = 0.65f;

        [Header("Counter bump")]
        [SerializeField] private float bumpScale = 0.3f;
        [SerializeField] private float bumpSeconds = 0.25f;

        private readonly Stack<Image> pool = new();
        private readonly Dictionary<RectTransform, Vector3> baseScales = new();
        private readonly Dictionary<RectTransform, float> bumpTimers = new();
        private RectTransform canvasRoot;
        private bool sellRequestedThisFrame;
        private double earnedThisFrame;
        private Vector2 sellScreenPosition;

        private void Awake()
        {
            if (dollarsTarget == null) Debug.LogError($"{nameof(HudFlyIconsUI)}.dollarsTarget is not assigned.");
            if (artifactTarget == null) Debug.LogError($"{nameof(HudFlyIconsUI)}.artifactTarget is not assigned.");
            if (coinSprite == null) Debug.LogError($"{nameof(HudFlyIconsUI)}.coinSprite is not assigned.");
            if (worldCamera == null) Debug.LogError($"{nameof(HudFlyIconsUI)}.worldCamera is not assigned.");

            // Its own root canvas rather than a child of the HUD's, so it sorts above open panels.
            var canvasObject = new GameObject("HUD Fly Icons Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasRoot = (RectTransform)canvasObject.transform;

            baseScales[dollarsTarget] = dollarsTarget.localScale;
            baseScales[artifactTarget] = artifactTarget.localScale;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Add<SellAllRequestedEvent>(NoteSell);
            GameManager.EventService.Add<HudCoinBurstRequestedEvent>(OnCoinBurstRequested);
            GameManager.EventService.Add<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Add<CrittersTurnedInEvent>(OnCrittersTurnedIn);
            GameManager.EventService.Add<DollarsEarnedEvent>(OnDollarsEarned);
            GameManager.EventService.Add<HudIconFlyRequestedEvent>(OnIconFlyRequested);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Remove<SellAllRequestedEvent>(NoteSell);
            GameManager.EventService.Remove<HudCoinBurstRequestedEvent>(OnCoinBurstRequested);
            GameManager.EventService.Remove<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Remove<CrittersTurnedInEvent>(OnCrittersTurnedIn);
            GameManager.EventService.Remove<DollarsEarnedEvent>(OnDollarsEarned);
            GameManager.EventService.Remove<HudIconFlyRequestedEvent>(OnIconFlyRequested);
        }

        private void OnDestroy()
        {
            if (canvasRoot != null) Destroy(canvasRoot.gameObject);
        }

        private void OnSellRequested(SellRequestedEvent evt) => NoteSell();
        private void OnSellGoodsRequested(SellGoodsRequestedEvent evt) => NoteSell();
        private void OnCrittersTurnedIn(CrittersTurnedInEvent evt) => NoteSell();
        private void OnDollarsEarned(DollarsEarnedEvent evt) => earnedThisFrame += evt.Amount;
        private void OnCoinBurstRequested(HudCoinBurstRequestedEvent evt) => SpawnCoins(worldCamera.WorldToScreenPoint(evt.WorldPosition), evt.Dollars);

        // The sale itself is handled by another listener of the same event, which may run before
        // or after this one - so the two halves (a sale was asked for, dollars arrived) are only
        // matched up at the end of the frame (LateUpdate). The button is read now, while it is
        // still there: selling out of a row can remove it.
        private void NoteSell()
        {
            sellRequestedThisFrame = true;
            sellScreenPosition = PressedScreenPosition();
        }

        private void LateUpdate()
        {
            if (sellRequestedThisFrame && earnedThisFrame > 0) SpawnCoins(sellScreenPosition, earnedThisFrame);
            sellRequestedThisFrame = false;
            earnedThisFrame = 0;

            UpdateBumps();
        }

        // The focused button (mouse clicks focus what they press, too), else the pointer.
        private static Vector2 PressedScreenPosition()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.transform is RectTransform rect)
            {
                return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            }
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            return new Vector2(Screen.width, Screen.height) * 0.5f;
        }

        private void SpawnCoins(Vector2 from, double dollars)
        {
            // More coins for bigger sales, on a log scale: $10 is a handful, $1M fills the air.
            int count = Mathf.Clamp(minCoins + Mathf.RoundToInt(Mathf.Log10((float)dollars + 1f) * 2f), minCoins, maxCoins);
            float size = coinSize * Screen.height;
            for (int i = 0; i < count; i++)
            {
                // Mostly upward, like a fountain.
                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                Vector2 scatter = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (burstRadius * Screen.height * Random.Range(0.4f, 1f));
                StartCoroutine(FlyCoin(Rent(coinSprite, size), from, scatter, i * coinStagger));
            }
        }

        private IEnumerator FlyCoin(Image coin, Vector2 from, Vector2 scatter, float delay)
        {
            var rect = coin.rectTransform;
            Vector3 fullScale = Vector3.one;

            for (float t = 0f; t < burstSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / burstSeconds;
                rect.position = Vector2.Lerp(from, scatter, Effects.Easing.OutCubic(k));
                rect.localScale = fullScale * Mathf.Min(1f, k * 3f);
                yield return null;
            }

            // Hangs for a moment, so the coins leave for the counter one after another.
            rect.position = scatter;
            rect.localScale = fullScale;
            yield return new WaitForSecondsRealtime(delay);

            for (float t = 0f; t < flySeconds; t += Time.unscaledDeltaTime)
            {
                float k = Effects.Easing.InQuad(t / flySeconds);
                // Re-read every frame: the counter can move (layout, resolution change).
                rect.position = Vector2.Lerp(scatter, dollarsTarget.position, k);
                rect.localScale = fullScale * Mathf.Lerp(1f, 0.6f, k);
                yield return null;
            }

            Return(coin);
            Bump(dollarsTarget);
        }

        private void OnIconFlyRequested(HudIconFlyRequestedEvent evt)
        {
            Vector2 from = worldCamera.WorldToScreenPoint(evt.WorldPosition);
            StartCoroutine(FlyArtifact(Rent(evt.Icon, artifactSize * Screen.height), from));
        }

        // Swoops up and over to the artifact counter, shrinking as it goes.
        private IEnumerator FlyArtifact(Image icon, Vector2 from)
        {
            var rect = icon.rectTransform;
            for (float t = 0f; t < artifactFlySeconds; t += Time.unscaledDeltaTime)
            {
                float k = Effects.Easing.InQuad(t / artifactFlySeconds);
                Vector2 to = artifactTarget.position;
                Vector2 control = Vector2.Lerp(from, to, 0.35f) + Vector2.up * (0.15f * Screen.height);
                rect.position = Vector2.Lerp(Vector2.Lerp(from, control, k), Vector2.Lerp(control, to, k), k);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, k);
                yield return null;
            }

            Return(icon);
            Bump(artifactTarget);
        }

        private Image Rent(Sprite sprite, float size)
        {
            Image image;
            if (pool.Count > 0)
            {
                image = pool.Pop();
            }
            else
            {
                image = new GameObject("Fly Icon").AddComponent<Image>();
                image.transform.SetParent(canvasRoot, false);
                image.raycastTarget = false;
                image.preserveAspect = true;
            }

            image.sprite = sprite;
            image.rectTransform.sizeDelta = Vector2.one * size;
            image.rectTransform.localScale = Vector3.zero;
            image.gameObject.SetActive(true);
            return image;
        }

        private void Return(Image image)
        {
            image.gameObject.SetActive(false);
            pool.Push(image);
        }

        private void Bump(RectTransform target) => bumpTimers[target] = bumpSeconds;

        // A quick swell that settles back; arrivals in quick succession just keep it swollen.
        private void UpdateBumps()
        {
            foreach (var pair in baseScales)
            {
                if (!bumpTimers.TryGetValue(pair.Key, out float remaining)) continue;

                remaining -= Time.unscaledDeltaTime;
                if (remaining <= 0f)
                {
                    bumpTimers.Remove(pair.Key);
                    pair.Key.localScale = pair.Value;
                    continue;
                }
                bumpTimers[pair.Key] = remaining;
                pair.Key.localScale = pair.Value * (1f + bumpScale * (remaining / bumpSeconds));
            }
        }
    }
}
