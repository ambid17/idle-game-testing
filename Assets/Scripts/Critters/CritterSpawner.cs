using System.Collections.Generic;
using Atmosphere;
using Events;
using MapGeneration;
using UnityEngine;

namespace Critters
{
    // Populates each resident layer's pre-carved empty pockets (ChunkData.EmptyPockets) with
    // critters from that layer's LayerConfig.CritterTable, and removes them again when the layer
    // stops being resident. Every roll is hashed from (world seed, layer, pocket seed cell) via
    // MapRng, so a pocket always holds the same critter at the same spot across loads - and once
    // caught (CritterCollection.IsSpawnCaught) it stays gone until prestige brings a new seed.
    // Critters are built in code rather than from a prefab: they're a sprite, a trigger and a
    // Critter component, all driven by their CritterDefinition.
    public class CritterSpawner : Singleton<CritterSpawner>
    {
        private enum Salt
        {
            Gate = 101,
            Species = 102,
            Cell = 103,
        }

        private const string InteractableLayerName = "Interactable";
        private const int CritterSortingOrder = 2;
        private const float InteractionRadius = 0.6f;

        private static MapGenerationService map => GameManager.MapGenerationService;

        [Tooltip("The jar a caught critter is sucked into (Critter's catch animation).")]
        [SerializeField] private Sprite jarSprite;

        private readonly Dictionary<int, List<Critter>> crittersByLayer = new();
        private int interactableLayer;

        protected override void Initialize()
        {
            base.Initialize();
            interactableLayer = LayerMask.NameToLayer(InteractableLayerName);
            if (interactableLayer < 0) Debug.LogError($"CritterSpawner: no '{InteractableLayerName}' physics layer - critters won't be catchable.");
            if (jarSprite == null) Debug.LogError("CritterSpawner.jarSprite is not assigned.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<ChunkViewShownEvent>(OnChunkViewShown);
            GameManager.EventService.Add<ChunkViewHiddenEvent>(OnChunkViewHidden);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<ChunkViewShownEvent>(OnChunkViewShown);
            GameManager.EventService.Remove<ChunkViewHiddenEvent>(OnChunkViewHidden);
        }

        private void OnChunkViewShown(ChunkViewShownEvent evt)
        {
            if (crittersByLayer.ContainsKey(evt.LayerIndex)) return;
            crittersByLayer[evt.LayerIndex] = SpawnLayer(evt.LayerIndex);
        }

        private void OnChunkViewHidden(ChunkViewHiddenEvent evt)
        {
            if (!crittersByLayer.TryGetValue(evt.LayerIndex, out var critters)) return;

            foreach (var critter in critters)
            {
                if (critter != null) Destroy(critter.gameObject);
            }
            crittersByLayer.Remove(evt.LayerIndex);
        }

        private List<Critter> SpawnLayer(int layerIndex)
        {
            var spawned = new List<Critter>();
            var config = GameManager.LayerConfigProvider.GetConfig(layerIndex);
            if (config == null || config.CritterTable.Count == 0 || config.CritterChancePerPocket <= 0f) return spawned;

            var chunk = map.World.GetOrGenerateChunk(layerIndex);
            int seed = map.World.Seed;

            foreach (var pocket in chunk.EmptyPockets)
            {
                var seedCell = pocket[0];
                var spawnKey = new Vector3Int(seedCell.x, seedCell.y, layerIndex);
                if (CritterCollection.Instance.IsSpawnCaught(spawnKey)) continue;
                if (MapRng.Value01(seed, layerIndex, seedCell.x, seedCell.y, (int)Salt.Gate) >= config.CritterChancePerPocket) continue;

                var species = PickSpecies(config.CritterTable, MapRng.Value01(seed, layerIndex, seedCell.x, seedCell.y, (int)Salt.Species));
                if (species == null) continue;

                if (!TryPickHomeCell(layerIndex, pocket, species, MapRng.Value01(seed, layerIndex, seedCell.x, seedCell.y, (int)Salt.Cell), out var homeCell)) continue;

                spawned.Add(CreateCritter(species, spawnKey, map.CellToWorldCenter(layerIndex, homeCell.x, homeCell.y), layerIndex));
            }

            return spawned;
        }

        // Flyers can live in any pocket cell; ground movers need one with solid ground beneath.
        // A pocket with no floor at all (e.g. a vertical shaft opened up below it) just stays empty.
        private static bool TryPickHomeCell(int layerIndex, List<Vector2Int> pocket, CritterDefinition species, float roll01, out Vector2Int homeCell)
        {
            homeCell = default;
            var candidates = new List<Vector2Int>(pocket.Count);
            foreach (var cell in pocket)
            {
                var center = map.CellToWorldCenter(layerIndex, cell.x, cell.y);
                if (!map.IsOpenAt(center)) continue;
                if (!species.IsFlyer && map.IsOpenAt(center + Vector3.down * map.CellSize)) continue;
                candidates.Add(cell);
            }

            if (candidates.Count == 0) return false;
            homeCell = candidates[Mathf.Min(candidates.Count - 1, Mathf.FloorToInt(roll01 * candidates.Count))];
            return true;
        }

        private static CritterDefinition PickSpecies(List<WeightedCritterEntry> table, float roll01)
        {
            float total = 0f;
            foreach (var entry in table)
            {
                if (entry.Critter != null) total += entry.Weight;
            }
            if (total <= 0f) return null;

            float target = roll01 * total;
            float cumulative = 0f;
            CritterDefinition last = null;
            foreach (var entry in table)
            {
                if (entry.Critter == null) continue;
                cumulative += entry.Weight;
                last = entry.Critter;
                if (target <= cumulative) return last;
            }
            return last;
        }

        private Critter CreateCritter(CritterDefinition species, Vector3Int spawnKey, Vector3 homePosition, int layerIndex)
        {
            var root = new GameObject($"Critter_{species.Id}_L{layerIndex}_{spawnKey.x}_{spawnKey.y}");
            root.transform.SetParent(transform, false);
            root.transform.position = homePosition;
            if (interactableLayer >= 0) root.layer = interactableLayer;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var body = visual.AddComponent<SpriteRenderer>();
            body.sprite = species.Sprite;
            body.sortingOrder = CritterSortingOrder;
            float spriteHeight = species.Sprite != null ? Mathf.Max(0.01f, species.Sprite.bounds.size.y) : 1f;
            visual.transform.localScale = Vector3.one * (species.Size / spriteHeight);

            SpriteRenderer glow = null;
            if (species.GlowColor.a > 0f) glow = GlowSprites.CreateGlow(root.transform, species.GlowColor, species.Size * 3f);

            // Kinematic body so the player's trigger reliably sees a collider that moves every frame.
            var rigidbody2d = root.AddComponent<Rigidbody2D>();
            rigidbody2d.bodyType = RigidbodyType2D.Kinematic;
            var trigger = root.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = InteractionRadius;

            var critter = root.AddComponent<Critter>();
            critter.Configure(species, spawnKey, homePosition, body, glow, trigger, jarSprite);
            return critter;
        }
    }
}
