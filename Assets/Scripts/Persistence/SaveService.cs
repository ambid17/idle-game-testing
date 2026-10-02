using Automation;
using Critters;
using Museum;
using Economy;
using Events;
using MapGeneration;
using Player;
using Processing;
using Tutorial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;

namespace Persistence
{
    // Save/load per the resolved persistence decision: Wallet dollars, all UpgradeManager levels,
    // IdleEarningsTracker's rolling averages, AutomationSettings, Depot's ore bank, player
    // inventory/health/fuel/position, and a last-active timestamp (used to compute "minutes away"
    // for the offline-earnings screen) - written to save.json. Mine/chunk terrain is persisted
    // separately to map.json via MapGeneration/Persistence/MapPersistenceService, since it's
    // potentially much larger and independent of the rest. Follows
    // MapGeneration/Persistence/ChunkSerializer's JsonUtility approach, and (unlike that scaffolding,
    // which sat unwired until this class picked it up) is wired to Unity's lifecycle - nothing in
    // the project persisted anything to disk before this.
    public class SaveService : Singleton<SaveService>
    {
        private const float AutosaveIntervalSeconds = 60f;

        // Market upgrade save keys (DisplayName) whose definitions were removed on purpose, skipped
        // silently on load instead of hitting UpgradeManagerBase.SetLevel's stale-key error.
        // "Fuel Efficiency" moved to the Prestige tree (Survival_FuelEfficiency) - Market levels
        // reset every prestige anyway, so nothing meaningful is lost. "Marketing" was split into the
        // per-ore-category Marketing upgrades (Economy_*OreSaleValue).
        private static readonly HashSet<string> RetiredMarketUpgradeKeys = new() { "Fuel Efficiency", "Marketing" };
        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        private string MapSavePath => Path.Combine(Application.persistentDataPath, "map.json");
        public bool HasSaveFile => File.Exists(SavePath);
        public bool HasLoadedData => hasLoadedData;
        private bool hasLoadedData = false;

        [SerializeField] private PlayerController playerController;
        [SerializeField] private ChestSpawner chestSpawner;
        [SerializeField] private SkyArtifact skyArtifact;
        private PlayerInventory playerInventory;
        private PlayerHealth playerHealth;
        private PlayerMining playerMining;

        protected override void Initialize()
        {
            base.Initialize();

            if (playerController == null)
            {
                Debug.LogError("SaveService.playerController is not assigned. Player state will not be saved/restored.");
            }
            else
            {
                playerInventory = playerController.GetComponent<PlayerInventory>();
                playerHealth = playerController.GetComponent<PlayerHealth>();
                playerMining = playerController.GetComponent<PlayerMining>();
            }

            if (chestSpawner == null)
            {
                Debug.LogError("SaveService.chestSpawner is not assigned. Active chests will not be saved/restored.");
            }

            if (skyArtifact == null) Debug.LogError("SaveService.skyArtifact is not assigned. The sky relic's collected state will not be saved/restored.");

            // OS force-kill (especially on mobile) doesn't reliably call OnApplicationQuit, so a
            // periodic safety-net autosave backs up OnApplicationQuit/OnApplicationPause below.
            InvokeRepeating(nameof(Save), AutosaveIntervalSeconds, AutosaveIntervalSeconds);
        }

        // Must override (not hide) Singleton<T>.OnApplicationQuit - Unity invokes magic methods by
        // reflecting on the most-derived declaration, so a same-named method here that doesn't
        // override would stop the base's IsQuitting flag from ever being set for this singleton.
        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            Save();
        }
        private void OnApplicationPause(bool paused) { if (paused) Save(); }

        public void Save()
        {
            Debug.Log($"SaveService.Save: writing save file to {SavePath} and map file to {MapSavePath}");
            var data = new GameSaveData
            {
                Dollars = Wallet.Instance.Dollars,
                ArtifactCount = Wallet.Instance.ArtifactCount,
                DollarsEarnedThisRun = Wallet.Instance.DollarsEarnedThisRun,
                PrestigeCount = PrestigeManager.Instance.PrestigeCount,
                LastActiveUtcTimestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                AutomationSettings = new AutomationSettingsSaveData
                {
                    StorageDroneTargetMode = AutomationSettings.Instance.StorageDroneTargetMode,
                    FuelDroneTargetMode = AutomationSettings.Instance.FuelDroneTargetMode,
                    StorageDroneDepositMode = AutomationSettings.Instance.StorageDroneDepositMode
                }
            };

            foreach (var kvp in UpgradeManager.Instance.AllLevels)
            {
                data.UpgradeLevels.Add(new UpgradeLevelEntry { UpgradeId = kvp.Key, Level = kvp.Value });
            }

            foreach (var kvp in PrestigeUpgradeManager.Instance.AllLevels)
            {
                data.PrestigeUpgradeLevels.Add(new UpgradeLevelEntry { UpgradeId = kvp.Key, Level = kvp.Value });
            }

            foreach (var kvp in PrestigeUpgradeManager.Instance.AllQueuedLevels)
            {
                data.PrestigeQueuedUpgradeLevels.Add(new UpgradeLevelEntry { UpgradeId = kvp.Key, Level = kvp.Value });
            }

            foreach (var kvp in IdleEarningsTracker.Instance.AveragePerMinute)
            {
                data.IdleAverages.Add(new OreAverageEntry { Id = kvp.Key, AveragePerMinute = kvp.Value });
            }

            foreach (var kvp in Depot.Instance.StoredOres)
            {
                data.DepotOres.Add(new OreCountEntry { Id = kvp.Key, Count = kvp.Value });
            }

            data.DepotSellLockedOres.AddRange(Depot.Instance.SellLockedOres);

            foreach (var kvp in Depot.Instance.StoredGoods)
            {
                data.DepotGoods.Add(new GoodsCountEntry { Id = kvp.Key, Count = kvp.Value });
            }

            data.DepotDiscoveredOres.AddRange(Depot.Instance.DiscoveredOres);
            data.DepotDiscoveredGoods.AddRange(Depot.Instance.DiscoveredGoods);

            var processingSlots = ProcessingManager.Instance.Slots;
            for (int i = 0; i < processingSlots.Count; i++)
            {
                var job = processingSlots[i];
                if (job == null) continue;

                data.ProcessingJobs.Add(new ProcessingJobSaveEntry
                {
                    SlotIndex = i,
                    RecipeId = job.Recipe.Id,
                    Quantity = job.Remaining,
                    TimeRemainingSeconds = job.UnitTimeRemaining
                });
            }

            data.ProcessingUncollectedCompletions = ProcessingManager.Instance.UncollectedCompletions;
            data.GoodsMarket = GoodsMarket.Instance.ToSaveData();

            if (playerController != null)
            {
                data.Player = new PlayerSaveData
                {
                    CurrentHp = playerHealth != null ? playerHealth.CurrentHp : 0f,
                    Fuel = playerController.Fuel,
                    Position = playerController.transform.position,
                };

                if (playerInventory != null)
                {
                    foreach (var kvp in playerInventory.OreCounts)
                    {
                        data.Player.OreCounts.Add(new OreCountEntry { Id = kvp.Key, Count = kvp.Value });
                    }
                }

                if (playerMining != null)
                {
                    foreach (var kvp in playerMining.UnmineableNotifyCounts)
                    {
                        data.Player.UnmineableNotifyCounts.Add(new OreCountEntry { Id = kvp.Key, Count = kvp.Value });
                    }
                }
            }

            foreach (var chest in ChestRegistry.Instance.ActiveChests)
            {
                var entry = new ChestSaveEntry { Position = chest.transform.position };
                foreach (var kvp in chest.OreCounts)
                {
                    if (kvp.Value <= 0) continue;
                    entry.OreCounts.Add(new OreCountEntry { Id = kvp.Key, Count = kvp.Value });
                }
                data.Chests.Add(entry);
            }

            if (chestSpawner != null && chestSpawner.TryGetPendingChest(out var pendingChest))
            {
                var entry = new ChestSaveEntry { Position = pendingChest.Position };
                foreach (var kvp in pendingChest.OreCounts)
                {
                    if (kvp.Value <= 0) continue;
                    entry.OreCounts.Add(new OreCountEntry { Id = kvp.Key, Count = kvp.Value });
                }
                data.Chests.Add(entry);
            }

            foreach (var id in TutorialManager.Instance.ShownTutorials)
            {
                data.ShownTutorials.Add(id);
            }

            data.LifetimeStats = GameManager.AchievementManager.Stats;
            data.Critters = CritterCollection.Instance.ToSaveData();
            data.Museum = RuneCollection.Instance.ToSaveData();
            data.SkyArtifactCollected = skyArtifact.IsCollected;

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService.Save: failed to write save file at {SavePath}: {e}");
            }

            try
            {
                var mapData = MapPersistenceService.BuildSaveData(GameManager.MapGenerationService.World);
                File.WriteAllText(MapSavePath, MapPersistenceService.ToJson(mapData));
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService.Save: failed to write map file at {MapSavePath}: {e}");
            }
        }

        public void DeleteSaveData()
        {
            Debug.Log($"SaveService.DeleteSaveData: deleting save file at {SavePath} and map file at {MapSavePath}");

            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService.DeleteSaveData: failed to delete save file at {SavePath}: {e}");
            }

            try
            {
                if (File.Exists(MapSavePath)) File.Delete(MapSavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService.DeleteSaveData: failed to delete map file at {MapSavePath}: {e}");
            }
        }

        // Null if no save file exists yet (first run).
        public GameSaveData Load()
        {
            if (!File.Exists(SavePath)) return null;

            try
            {
                return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService.Load: failed to read save file at {SavePath}: {e}");
                return null;
            }
        }

        // Null if no map file exists yet (first run). Independent of Load()/save.json so a
        // missing/corrupt file for one doesn't block the other.
        public MapSaveData LoadMap()
        {
            if (!File.Exists(MapSavePath)) return null;

            try
            {
                return MapPersistenceService.FromJson(File.ReadAllText(MapSavePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService.LoadMap: failed to read map file at {MapSavePath}: {e}");
                return null;
            }
        }

        // Restores Wallet/UpgradeManager/AutomationSettings/IdleEarningsTracker state, computes
        // minutes-away and the resulting offline ore, and dispatches OfflineEarningsReadyEvent.
        // Does NOT deposit the ore into the Depot yet - per the resolved idle-earnings decision,
        // that only happens once the player acknowledges the offline-earnings screen.
        public void ApplyLoadedData(GameSaveData data)
        {
            if (data == null)
            {
                hasLoadedData = true;
                GameManager.EventService.Dispatch<LoadCompletedEvent>();
                return;
            }

            Wallet.Instance.SetDollars(data.Dollars);
            Wallet.Instance.SetArtifactCount(data.ArtifactCount);
            // After SetDollars, which doesn't touch the run total, so the loaded value sticks.
            Wallet.Instance.SetDollarsEarnedThisRun(data.DollarsEarnedThisRun);
            PrestigeManager.Instance.SetPrestigeCount(data.PrestigeCount);
            TutorialManager.Instance.RestoreFromSaveData(data.ShownTutorials);
            GameManager.AchievementManager.RestoreFromSaveData(data.LifetimeStats);
            CritterCollection.Instance.RestoreFromSaveData(data.Critters);
            RuneCollection.Instance.RestoreFromSaveData(data.Museum);
            skyArtifact.RestoreCollected(data.SkyArtifactCollected);

            foreach (var entry in data.UpgradeLevels)
            {
                if (RetiredMarketUpgradeKeys.Contains(entry.UpgradeId)) continue;
                UpgradeManager.Instance.SetLevelFromSave(entry.UpgradeId, entry.Level);
            }

            foreach (var entry in data.PrestigeUpgradeLevels)
            {
                PrestigeUpgradeManager.Instance.SetLevel(entry.UpgradeId, entry.Level);
            }

            foreach (var entry in data.PrestigeQueuedUpgradeLevels)
            {
                PrestigeUpgradeManager.Instance.SetQueuedLevel(entry.UpgradeId, entry.Level);
            }

            if (data.AutomationSettings != null)
            {
                AutomationSettings.Instance.RestoreFromSaveData(
                    data.AutomationSettings.StorageDroneTargetMode,
                    data.AutomationSettings.FuelDroneTargetMode,
                    data.AutomationSettings.StorageDroneDepositMode);
            }

            var depotOres = new Dictionary<BlockTypeId, int>();
            foreach (var entry in data.DepotOres)
            {
                depotOres[entry.Id] = entry.Count;
            }
            Depot.Instance.RestoreFromSaveData(depotOres);
            Depot.Instance.RestoreSellLocksFromSaveData(data.DepotSellLockedOres);

            var depotGoods = new Dictionary<ProcessingRecipeId, int>();
            foreach (var entry in data.DepotGoods)
            {
                depotGoods[entry.Id] = entry.Count;
            }
            Depot.Instance.RestoreGoodsFromSaveData(depotGoods);

            // Saves from before discovery was tracked have empty lists here, so seed from the
            // lifetime stats too - otherwise everything the player already sold would hide again.
            var discoveredOres = new List<BlockTypeId>(data.DepotDiscoveredOres);
            var discoveredGoods = new List<ProcessingRecipeId>(data.DepotDiscoveredGoods);
            if (data.LifetimeStats != null)
            {
                discoveredOres.AddRange(data.LifetimeStats.OreTypesMined);
                discoveredGoods.AddRange(data.LifetimeStats.RecipesCompleted);
            }
            Depot.Instance.RestoreDiscoveredFromSaveData(discoveredOres, discoveredGoods);

            // Fast-forwards in-progress Processing jobs by the same elapsed-real-time math as the
            // idle ore average below - any job that would have finished while the game was closed
            // completes immediately (goods deposited, no popup).
            float elapsedSeconds = ComputeMinutesAway(data.LastActiveUtcTimestamp) * 60f;
            ProcessingManager.Instance.RestoreFromSaveData(data.ProcessingJobs, elapsedSeconds, data.ProcessingUncollectedCompletions);
            GoodsMarket.Instance.RestoreFromSaveData(data.GoodsMarket, elapsedSeconds);

            if (data.Player != null)
            {
                var playerOres = new Dictionary<BlockTypeId, int>();
                foreach (var entry in data.Player.OreCounts)
                {
                    playerOres[entry.Id] = entry.Count;
                }

                var unmineableNotifyCounts = new Dictionary<BlockTypeId, int>();
                foreach (var entry in data.Player.UnmineableNotifyCounts)
                {
                    unmineableNotifyCounts[entry.Id] = entry.Count;
                }

                if (playerInventory != null) playerInventory.RestoreFromSaveData(playerOres);
                if (playerMining != null) playerMining.RestoreFromSaveData(unmineableNotifyCounts);
                if (playerHealth != null) playerHealth.RestoreFromSaveData(data.Player.CurrentHp);
                if (playerController != null) playerController.RestoreFromSaveData(data.Player.Fuel, data.Player.Position);
            }

            if (chestSpawner != null && data.Chests != null)
            {
                var chestSpawnData = new List<ChestSpawnData>();
                foreach (var entry in data.Chests)
                {
                    var oreCounts = new Dictionary<BlockTypeId, int>();
                    foreach (var oreEntry in entry.OreCounts)
                    {
                        oreCounts[oreEntry.Id] = oreEntry.Count;
                    }
                    chestSpawnData.Add(new ChestSpawnData(entry.Position, oreCounts));
                }
                chestSpawner.RestoreFromSaveData(chestSpawnData);
            }

            // After upgrade levels are restored - the offline fall-off curve reads them.
            LoadOfflineEarnings(data);

            hasLoadedData = true;
            GameManager.EventService.Dispatch<LoadCompletedEvent>();
        }

        // Restores mine/chunk terrain from map.json - independent of ApplyLoadedData/save.json so
        // a missing/corrupt file for one doesn't block the other.
        public void ApplyMapData(MapSaveData mapData)
        {
            if (mapData == null) return;

            var restoredWorld = MapPersistenceService.Restore(mapData);
            GameManager.MapGenerationService.RestoreWorld(restoredWorld);
        }

        private static float ComputeMinutesAway(string lastActiveUtcTimestamp)
        {
            if (string.IsNullOrEmpty(lastActiveUtcTimestamp)) return 0f;
            if (!DateTime.TryParse(lastActiveUtcTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastActive)) return 0f;

            return Mathf.Max(0f, (float)(DateTime.UtcNow - lastActive).TotalMinutes);
        }

        private void LoadOfflineEarnings(GameSaveData data)
        {
            var averages = new Dictionary<BlockTypeId, float>();
            foreach (var entry in data.IdleAverages)
            {
                averages[entry.Id] = entry.AveragePerMinute;
            }
            IdleEarningsTracker.Instance.RestoreFromSaveData(averages);

            float minutesAway = ComputeMinutesAway(data.LastActiveUtcTimestamp);
            if (minutesAway <= 0f) return;

            var oreGained = IdleEarningsTracker.ComputeOfflineOre(averages, minutesAway);

            if (oreGained.Count == 0) return;
            GameManager.EventService.Dispatch(new OfflineEarningsReadyEvent(oreGained, minutesAway));
        }
    }
}
