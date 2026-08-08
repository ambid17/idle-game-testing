using Automation;
using Economy;
using Events;
using MapGeneration;
using Persistence;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private ChunkStreamingManager _chunkStreamingManager;
    [SerializeField] private MapGenerationService _mapGenerationService;
    [SerializeField] private BlockTypeDatabase _blockTypeDatabase;
    [SerializeField] private LayerConfigProvider _layerConfigProvider;
    [SerializeField] private UpgradeDatabase _upgradeDatabase;
    [SerializeField] private AutomationConfig _automationConfig;

    public static ChunkStreamingManager ChunkStreamingManager => Instance._chunkStreamingManager;
    public static MapGenerationService MapGenerationService => Instance._mapGenerationService;
    public static BlockTypeDatabase BlockTypeDatabase => Instance._blockTypeDatabase;
    public static LayerConfigProvider LayerConfigProvider => Instance._layerConfigProvider;
    public static UpgradeDatabase UpgradeDatabase => Instance._upgradeDatabase;
    public static AutomationConfig AutomationConfig => Instance._automationConfig;

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
        if (_automationConfig == null)
        {
            Debug.LogError("AutomationConfig is not assigned in GameManager.");
        }
    }

    // Runs after every scene object's Awake(), so Wallet/UpgradeManager/AutomationSettings/
    // IdleEarningsTracker singletons are all safe to touch here regardless of scene script order.
    private void Start()
    {
        SaveService.Instance.ApplyLoadedData(SaveService.Instance.Load());
        SaveService.Instance.ApplyMapData(SaveService.Instance.LoadMap());
    }
}
