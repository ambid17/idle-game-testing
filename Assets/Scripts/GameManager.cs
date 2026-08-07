using Economy;
using Events;
using MapGeneration;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private ChunkStreamingManager _chunkStreamingManager;
    [SerializeField] private MapGenerationService _mapGenerationService;
    [SerializeField] private BlockTypeDatabase _blockTypeDatabase;
    [SerializeField] private LayerConfigProvider _layerConfigProvider;
    [SerializeField] private UpgradeDatabase _upgradeDatabase;

    public static ChunkStreamingManager ChunkStreamingManager => Instance._chunkStreamingManager;
    public static MapGenerationService MapGenerationService => Instance._mapGenerationService;
    public static BlockTypeDatabase BlockTypeDatabase => Instance._blockTypeDatabase;
    public static LayerConfigProvider LayerConfigProvider => Instance._layerConfigProvider;
    public static UpgradeDatabase UpgradeDatabase => Instance._upgradeDatabase;

    private EventService _eventService;
    public static EventService EventService
    {
        get
        {
            if (Instance._eventService == null)
            {
                Instance._eventService = new EventService();
            }

            return Instance._eventService;
        }
    }


    protected override void Initialize()
    {
        base.Initialize();
        if(_chunkStreamingManager == null)
        {
            Debug.LogError("ChunkStreamingManager is not assigned in GameManager.");
        }
        if (_mapGenerationService == null)
        {
            Debug.LogError("MapGenerationService is not assigned in GameManager.");
        }
        if (_blockTypeDatabase == null)
        {
            Debug.LogError("BlockTypeDatabase is not assigned in GameManager.");
        }
        if (_layerConfigProvider == null)
        {
            Debug.LogError("LayerConfigProvider is not assigned in GameManager.");
        }
        if (_upgradeDatabase == null)
        {
            Debug.LogError("UpgradeDatabase is not assigned in GameManager.");
        }
    }
}
