using Events;
using UnityEngine;

namespace Buildings
{
    // Shows the Depot's free refuel/repair happening (Player.PlayerDepotResupply tops the player
    // off instantly on arrival, which is otherwise only a toast): for a moment the console screen
    // beside the door pulses and fuel-drop / repair-cross icons float up off the player - only the
    // kinds that were actually topped off. Lives on the Depot building.
    public class DepotResupplyEffect : MonoBehaviour
    {
        private const int PoolSize = 16;

        [SerializeField] private SpriteRenderer consoleGlow;
        [SerializeField] private Sprite fuelIcon;
        [SerializeField] private Sprite repairIcon;

        [SerializeField] private float duration = 1.2f;
        [SerializeField] private float consolePulsesPerSecond = 4f;
        [SerializeField] private float iconInterval = 0.08f;
        [SerializeField] private float iconLifetime = 0.7f;
        [SerializeField] private float iconRiseSpeed = 1.1f;
        [Tooltip("Icons spawn within this box around the player (world units, half extents).")]
        [SerializeField] private Vector2 iconSpawnExtents = new(0.4f, 0.3f);
        [SerializeField] private int iconSortingOrder = 6;

        private struct Icon
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public float Age;
        }

        private readonly Icon[] icons = new Icon[PoolSize];
        private Transform iconRoot;
        private Transform target;
        private bool refueled;
        private bool repaired;
        private float remaining;
        private float spawnTimer;
        private int spawnCount;

        private void Awake()
        {
            if (consoleGlow == null) Debug.LogError("DepotResupplyEffect.consoleGlow is not assigned.");
            if (fuelIcon == null) Debug.LogError("DepotResupplyEffect.fuelIcon is not assigned.");
            if (repairIcon == null) Debug.LogError("DepotResupplyEffect.repairIcon is not assigned.");

            consoleGlow.enabled = false;

            // A scene root rather than a child: the building's SortingGroup would otherwise pin
            // the icons behind the player they float over.
            iconRoot = new GameObject("DepotResupplyIcons").transform;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Icon");
                go.transform.SetParent(iconRoot, false);
                var iconRenderer = go.AddComponent<SpriteRenderer>();
                iconRenderer.sortingOrder = iconSortingOrder;
                iconRenderer.enabled = false;
                icons[i].Renderer = iconRenderer;
                icons[i].Age = iconLifetime;
            }
        }

        private void OnDestroy()
        {
            if (iconRoot != null) Destroy(iconRoot.gameObject);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<DepotResupplyEvent>(OnResupply);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DepotResupplyEvent>(OnResupply);
        }

        private void OnResupply(DepotResupplyEvent evt)
        {
            target = evt.Target;
            refueled = evt.Refueled;
            repaired = evt.Repaired;
            remaining = duration;
            spawnTimer = 0f;
        }

        private void Update()
        {
            if (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    spawnTimer += iconInterval;
                    SpawnIcon();
                }
            }

            UpdateConsole();
            UpdateIcons();
        }

        private void UpdateConsole()
        {
            consoleGlow.enabled = remaining > 0f;
            if (!consoleGlow.enabled) return;

            float pulse = 0.5f - 0.5f * Mathf.Cos((duration - remaining) * consolePulsesPerSecond * Mathf.PI * 2f);
            consoleGlow.color = new Color(1f, 1f, 1f, pulse);
        }

        private void SpawnIcon()
        {
            // Alternates fuel/repair when both were topped off.
            bool fuel = refueled && (!repaired || spawnCount % 2 == 0);
            spawnCount++;

            for (int i = 0; i < PoolSize; i++)
            {
                if (icons[i].Age < iconLifetime) continue;

                var offset = new Vector3(Random.Range(-iconSpawnExtents.x, iconSpawnExtents.x), Random.Range(-iconSpawnExtents.y, iconSpawnExtents.y), 0f);
                icons[i].Origin = target.position + offset;
                icons[i].Age = 0f;
                icons[i].Renderer.sprite = fuel ? fuelIcon : repairIcon;
                icons[i].Renderer.enabled = true;
                return;
            }
        }

        private void UpdateIcons()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (icons[i].Age >= iconLifetime) continue;

                icons[i].Age += Time.deltaTime;
                float t = Mathf.Clamp01(icons[i].Age / iconLifetime);
                var iconRenderer = icons[i].Renderer;
                if (t >= 1f)
                {
                    iconRenderer.enabled = false;
                    continue;
                }

                // Pops in, rises at a steady pace, fades out over the back half.
                iconRenderer.transform.position = icons[i].Origin + Vector3.up * (iconRiseSpeed * icons[i].Age);
                iconRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(t * 5f));
                iconRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - t) * 2f));
            }
        }
    }
}
