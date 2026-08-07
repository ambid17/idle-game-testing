using MapGeneration;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private ChunkStreamingManager _chunkStreamingManager;
    [SerializeField] private MapGenerationService _mapGenerationService;
    [SerializeField] private BlockTypeDatabase _blockTypeDatabase;
    [SerializeField] private LayerConfigProvider _layerConfigProvider;

    public static ChunkStreamingManager ChunkStreamingManager => Instance._chunkStreamingManager;
    public static MapGenerationService MapGenerationService => Instance._mapGenerationService;
    public static BlockTypeDatabase BlockTypeDatabase => Instance._blockTypeDatabase;
    public static LayerConfigProvider LayerConfigProvider => Instance._layerConfigProvider;
}
