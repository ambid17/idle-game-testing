using System.Collections.Generic;
using Events;
using MapGeneration;
using UnityEngine;

namespace Buildings
{
    // Shows the player banking their ore from outside the Depot (the Deposit / Deposit & Sell
    // interactions, which otherwise just empty the HUD inventory): chunks of each carried ore hop
    // out of the player one after another and arc into the doorway, each landing with a sparkle.
    // When the trip also sold the Depot's stock, the last chunk landing sends coins from the
    // doorway to the HUD's dollars counter (HudCoinBurstRequestedEvent). Cosmetic only - the ore
    // and dollars are credited before any of this. Lives on the Depot building.
    public class DepotDepositEffect : MonoBehaviour
    {
        private const int PoolSize = 24;

        [Tooltip("Where the ore flies to - the doorway.")]
        [SerializeField] private Transform dropPoint;

        [Tooltip("Most chunks one deposit throws, however much ore was carried.")]
        [SerializeField, Range(1, PoolSize)] private int maxIcons = 14;
        [Tooltip("Chunk size in world units.")]
        [SerializeField] private float iconSize = 0.6f;
        [SerializeField] private float flySeconds = 0.5f;
        [Tooltip("Delay between one chunk leaving the player and the next.")]
        [SerializeField] private float iconStagger = 0.05f;
        [Tooltip("How high the arc rises above the straight line to the doorway (world units).")]
        [SerializeField] private float arcHeight = 1.4f;
        [Tooltip("Chunks leave from within this box around the player (world units, half extents).")]
        [SerializeField] private Vector2 spawnExtents = new(0.3f, 0.25f);
        [Tooltip("Chunks land within this box around the doorway (world units, half extents).")]
        [SerializeField] private Vector2 landExtents = new(0.2f, 0.12f);
        [SerializeField] private int iconSortingOrder = 6;
        [SerializeField] private Color sellSparkleColor = new(1f, 0.85f, 0.3f, 1f);

        private struct Icon
        {
            public SpriteRenderer Renderer;
            public Transform Source;
            public Vector3 SourceOffset;
            public Vector3 Target;
            public float BaseScale;
            public float Spin;
            // Negative while waiting its turn to leave.
            public float Age;
            public bool Flying;
            // Dollars to burst out of the doorway when this chunk lands (the last of a sale).
            public double SoldFor;
        }

        private readonly Icon[] icons = new Icon[PoolSize];
        private readonly List<BlockTypeId> spawnOrder = new();
        private readonly Dictionary<BlockTypeId, int> remainingByType = new();
        private Transform iconRoot;

        private void Awake()
        {
            if (dropPoint == null) Debug.LogError("DepotDepositEffect.dropPoint is not assigned.");

            // A scene root rather than a child: the building's SortingGroup would otherwise pin
            // the chunks behind the player they leave from.
            iconRoot = new GameObject("DepotDepositIcons").transform;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Icon");
                go.transform.SetParent(iconRoot, false);
                var iconRenderer = go.AddComponent<SpriteRenderer>();
                iconRenderer.sortingOrder = iconSortingOrder;
                iconRenderer.enabled = false;
                icons[i].Renderer = iconRenderer;
            }
        }

        private void OnDestroy()
        {
            if (iconRoot != null) Destroy(iconRoot.gameObject);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerDepotDropOffEvent>(OnDropOff);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDepotDropOffEvent>(OnDropOff);
        }

        private void OnDropOff(PlayerDepotDropOffEvent evt)
        {
            BuildSpawnOrder(evt.Ores);

            // Nothing was carried, but the Depot's own stock sold: just the coins.
            if (spawnOrder.Count == 0)
            {
                if (evt.SoldFor > 0) BurstCoins(evt.SoldFor);
                return;
            }

            for (int n = 0; n < spawnOrder.Count; n++)
            {
                int i = FreeIconIndex();
                if (i < 0) break;

                var sprite = GameManager.BlockTypeDatabase.Get((byte)spawnOrder[n]).Icon;
                bool last = n == spawnOrder.Count - 1;

                icons[i].Source = evt.Source;
                icons[i].SourceOffset = new Vector3(Random.Range(-spawnExtents.x, spawnExtents.x), Random.Range(-spawnExtents.y, spawnExtents.y), 0f);
                icons[i].Target = dropPoint.position + new Vector3(Random.Range(-landExtents.x, landExtents.x), Random.Range(-landExtents.y, landExtents.y), 0f);
                icons[i].BaseScale = iconSize / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                icons[i].Spin = Random.Range(-360f, 360f);
                icons[i].Age = -n * iconStagger;
                icons[i].Flying = true;
                icons[i].SoldFor = last ? evt.SoldFor : 0;
                icons[i].Renderer.sprite = sprite;
            }
        }

        // One chunk per ore carried, taken a type at a time so every type shows up before any
        // repeats, up to maxIcons.
        private void BuildSpawnOrder(IReadOnlyDictionary<BlockTypeId, int> ores)
        {
            spawnOrder.Clear();
            remainingByType.Clear();
            foreach (var kvp in ores)
            {
                if (kvp.Value > 0) remainingByType[kvp.Key] = kvp.Value;
            }

            var types = new List<BlockTypeId>(remainingByType.Keys);
            bool added = true;
            while (added && spawnOrder.Count < maxIcons)
            {
                added = false;
                foreach (var type in types)
                {
                    if (spawnOrder.Count >= maxIcons) break;
                    if (remainingByType[type] <= 0) continue;

                    remainingByType[type]--;
                    spawnOrder.Add(type);
                    added = true;
                }
            }
        }

        private int FreeIconIndex()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (!icons[i].Flying) return i;
            }
            return -1;
        }

        private void Update()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (!icons[i].Flying) continue;

                icons[i].Age += Time.deltaTime;
                if (icons[i].Age < 0f) continue;

                var iconRenderer = icons[i].Renderer;
                float t = icons[i].Age / flySeconds;
                if (t >= 1f)
                {
                    icons[i].Flying = false;
                    iconRenderer.enabled = false;
                    Land(icons[i].Target, icons[i].SoldFor);
                    continue;
                }

                // Leaves from wherever the player is now, so a chunk still waiting its turn
                // doesn't appear behind a player who walked on.
                Vector3 from = icons[i].Source.position + icons[i].SourceOffset;
                Vector3 to = icons[i].Target;
                float k = Effects.Easing.InQuad(t);
                Vector3 position = Vector3.Lerp(from, to, k);
                position.y += arcHeight * Mathf.Sin(t * Mathf.PI);

                // Pops out of the player, then shrinks into the doorway.
                float scale = Mathf.Min(1f, t * 6f) * Mathf.Lerp(1f, 0.45f, k);
                iconRenderer.enabled = true;
                iconRenderer.transform.position = position;
                iconRenderer.transform.localScale = Vector3.one * (icons[i].BaseScale * scale);
                iconRenderer.transform.rotation = Quaternion.Euler(0f, 0f, icons[i].Spin * t);
            }
        }

        private void Land(Vector3 position, double soldFor)
        {
            GameManager.WorldEffects.SparkleBurst(position, 3, 0.05f, 1.2f);
            if (soldFor > 0) BurstCoins(soldFor);
        }

        private void BurstCoins(double dollars)
        {
            GameManager.WorldEffects.SparkleBurst(dropPoint.position, 10, 0.15f, 2.5f, sellSparkleColor);
            GameManager.EventService.Dispatch(new HudCoinBurstRequestedEvent(dropPoint.position, dollars));
        }
    }
}
