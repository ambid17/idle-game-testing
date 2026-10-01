using System.Collections.Generic;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // World-side effects of the trap-room fixtures stamped by the hazard set-pieces (Dart
    // Corridor, Crusher Room) - the counterpart of HazardEffectResolver for blocks that are never
    // mined. Player.HazardDamageHandler still owns all player damage.
    //   Pressure Plate + Dart Trap: a plate being stepped on fires every Dart Trap near it, out of
    //     each face that opens onto dug-out ground.
    //   Crusher: every Crusher block on a resident layer gets a CrusherPiston.
    // Event-only coupling; lives as a child of the GameManager scene object.
    public class StructureTrapResolver : MonoBehaviour
    {
        private static readonly Vector2Int[] Directions = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        [SerializeField] private DartProjectile dartPrefab;
        [SerializeField] private CrusherPiston crusherPistonPrefab;

        [Tooltip("A plate fires every Dart Trap within this many cells of it, horizontally / vertically.")]
        [SerializeField] private int plateRangeX = 12;
        [SerializeField] private int plateRangeY = 3;

        [Tooltip("Cycle delay per column, so a row of crushers slams as a wave rather than all at once.")]
        [SerializeField] private float crusherWaveStepSeconds = 0.3f;

        private readonly Dictionary<int, List<CrusherPiston>> pistonsByLayer = new();
        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        private void OnEnable()
        {
            GameManager.EventService.Add<PressurePlateTriggeredEvent>(OnPressurePlateTriggered);
            GameManager.EventService.Add<ChunkViewShownEvent>(OnChunkViewShown);
            GameManager.EventService.Add<ChunkViewHiddenEvent>(OnChunkViewHidden);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PressurePlateTriggeredEvent>(OnPressurePlateTriggered);
            GameManager.EventService.Remove<ChunkViewShownEvent>(OnChunkViewShown);
            GameManager.EventService.Remove<ChunkViewHiddenEvent>(OnChunkViewHidden);
        }

        private void Start()
        {
            if (dartPrefab == null) Debug.LogError($"{nameof(StructureTrapResolver)}: dartPrefab is not assigned.");
            if (crusherPistonPrefab == null) Debug.LogError($"{nameof(StructureTrapResolver)}: crusherPistonPrefab is not assigned.");

            // Layers that became resident before this component started listening.
            foreach (var chunk in mapGenerationService.World.GetLoadedChunks())
            {
                if (GameManager.ChunkStreamingManager.IsLayerResident(chunk.LayerIndex)) SpawnPistons(chunk.LayerIndex);
            }
        }

        private void OnPressurePlateTriggered(PressurePlateTriggeredEvent evt)
        {
            for (int dy = -plateRangeY; dy <= plateRangeY; dy++)
            {
                for (int dx = -plateRangeX; dx <= plateRangeX; dx++)
                {
                    int x = evt.X + dx, y = evt.Y + dy;
                    var block = mapGenerationService.GetBlockTypeAt(evt.LayerIndex, x, y);
                    if (block != null && block.Id == BlockTypeId.DartTrap) FireDartTrap(evt.LayerIndex, x, y);
                }
            }
        }

        private void FireDartTrap(int layerIndex, int x, int y)
        {
            foreach (var direction in Directions)
            {
                if (!mapGenerationService.IsOpenCell(layerIndex, x + direction.x, y + direction.y)) continue;

                var dart = Instantiate(dartPrefab, mapGenerationService.CellToWorldCenter(layerIndex, x, y), Quaternion.identity, transform);
                dart.Begin(layerIndex, x, y, direction);
            }
        }

        private void OnChunkViewShown(ChunkViewShownEvent evt) => SpawnPistons(evt.LayerIndex);

        private void OnChunkViewHidden(ChunkViewHiddenEvent evt) => ClearPistons(evt.LayerIndex);

        // Crushers are unmineable and chunks regenerate identically from the seed, so one scan
        // when the layer's view comes up is all the bookkeeping they need.
        private void SpawnPistons(int layerIndex)
        {
            ClearPistons(layerIndex);

            var chunk = mapGenerationService.World.GetOrGenerateChunk(layerIndex);
            var pistons = new List<CrusherPiston>();
            for (int y = 0; y < chunk.Height; y++)
            {
                for (int x = 0; x < chunk.Width; x++)
                {
                    if (chunk.Cells[chunk.Index(x, y)].BlockTypeId != (byte)BlockTypeId.Crusher) continue;

                    var piston = Instantiate(crusherPistonPrefab, mapGenerationService.CellToWorldCenter(layerIndex, x, y), Quaternion.identity, transform);
                    piston.Begin(layerIndex, x, y, x * crusherWaveStepSeconds);
                    pistons.Add(piston);
                }
            }
            if (pistons.Count > 0) pistonsByLayer[layerIndex] = pistons;
        }

        private void ClearPistons(int layerIndex)
        {
            if (!pistonsByLayer.TryGetValue(layerIndex, out var pistons)) return;

            foreach (var piston in pistons)
            {
                if (piston != null) Destroy(piston.gameObject);
            }
            pistonsByLayer.Remove(layerIndex);
        }
    }
}
