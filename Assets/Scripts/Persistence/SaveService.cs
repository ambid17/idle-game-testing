using Automation;
using Economy;
using Events;
using MapGeneration;
using Player;
using Processing;
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
        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        private string MapSavePath => Path.Combine(Application.persistentDataPath, "map.json");

        [SerializeField] private PlayerController playerController;
        private PlayerInventory playerInventory;
        private PlayerHealth playerHealth;

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
            }

            // OS force-kill (especially on mobile) doesn't reliably call OnApplicationQuit, so a
            // periodic safety-net autosave backs up OnApplicationQuit/OnApplicationPause below.
            InvokeRepeating(nameof(Save), AutosaveIntervalSeconds, AutosaveIntervalSeconds);
        }

        private void OnApplicationQuit() => Save();
        private void OnApplicationPause(bool paused) { if (paused) Save(); }

        public void Save()
        {
            Debug.Log($"SaveService.Save: writing save file to {SavePath} and map file to {MapSavePath}");
            var data = new GameSaveData
            {
                Dollars = Wallet.Instance.Dollars,
                ArtifactCount = Wallet.Instance.ArtifactCount,
                LastActiveUtcTimestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                AutomationSettings = new AutomationSettingsSaveData
                {
                    StorageDroneTargetMode = AutomationSettings.Instance.StorageDroneTargetMode,
                    FuelDroneTargetMode = AutomationSettings.Instance.FuelDroneTargetMode,
                    FuelSpendingCapPercent = AutomationSettings.Instance.FuelSpendingCapPercent
                }
            };

            foreach (var kvp in UpgradeManager.Instance.AllLevels)
            {
                data.UpgradeLevels.Add(new UpgradeLevelEntry { UpgradeId = kvp.Key, Level = kvp.Value });
            }

            data.PrestigePoints = PrestigePoints.Instance.Points;
            foreach (var kvp in PrestigeUpgradeManager.Instance.AllLevels)
            {
                data.PrestigeUpgradeLevels.Add(new UpgradeLevelEntry { UpgradeId = kvp.Key, Level = kvp.Value });
            }

            foreach (var kvp in IdleEarningsTracker.Instance.AveragePerMinute)
            {
                data.IdleAverages.Add(new OreAverageEntry { Id = kvp.Key, AveragePerMinute = kvp.Value });
            }

            foreach (var kvp in Depot.Instance.StoredOres)
            {
                data.DepotOres.Add(new OreCountEntry { Id = kvp.Key, Count = kvp.Value });
            }

            foreach (var kvp in Depot.Instance.StoredGoods)
            {
                data.DepotGoods.Add(new GoodsCountEntry { Id = kvp.Key, Count = kvp.Value });
            }

            var processingSlots = ProcessingManager.Instance.Slots;
            for (int i = 0; i < processingSlots.Count; i++)
            {
                var job = processingSlots[i];
                if (job == null) continue;

                data.ProcessingJobs.Add(new ProcessingJobSaveEntry
                {
                    SlotIndex = i,
                    RecipeId = job.Recipe.Id,
                    Quantity = job.Quantity,
                    TimeRemainingSeconds = job.TimeRemaining
                });
            }

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
            }

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
            if (data == null) return;

            Wallet.Instance.SetDollars(data.Dollars);
            Wallet.Instance.SetArtifactCount(data.ArtifactCount);

            foreach (var entry in data.UpgradeLevels)
            {
                UpgradeManager.Instance.SetLevelFromSave(entry.UpgradeId, entry.Level);
            }

            PrestigePoints.Instance.SetPoints(data.PrestigePoints);
            foreach (var entry in data.PrestigeUpgradeLevels)
            {
                PrestigeUpgradeManager.Instance.SetLevel(entry.UpgradeId, entry.Level);
            }

            if (data.AutomationSettings != null)
            {
                AutomationSettings.Instance.RestoreFromSaveData(
                    data.AutomationSettings.StorageDroneTargetMode,
                    data.AutomationSettings.FuelDroneTargetMode,
                    data.AutomationSettings.FuelSpendingCapPercent);
            }

            var depotOres = new Dictionary<BlockTypeId, int>();
            foreach (var entry in data.DepotOres)
            {
                depotOres[entry.Id] = entry.Count;
            }
            Depot.Instance.RestoreFromSaveData(depotOres);

            var depotGoods = new Dictionary<ProcessingRecipeId, int>();
            foreach (var entry in data.DepotGoods)
            {
                depotGoods[entry.Id] = entry.Count;
            }
            Depot.Instance.RestoreGoodsFromSaveData(depotGoods);

            // Fast-forwards in-progress Processing jobs by the same elapsed-real-time math as the
            // idle ore average below - any job that would have finished while the game was closed
            // completes immediately (goods deposited, no popup).
            float elapsedSeconds = ComputeMinutesAway(data.LastActiveUtcTimestamp) * 60f;
            ProcessingManager.Instance.RestoreFromSaveData(data.ProcessingJobs, elapsedSeconds);

            if (data.Player != null)
            {
                var playerOres = new Dictionary<BlockTypeId, int>();
                foreach (var entry in data.Player.OreCounts)
                {
                    playerOres[entry.Id] = entry.Count;
                }

                if (playerInventory != null) playerInventory.RestoreFromSaveData(playerOres);
                if (playerHealth != null) playerHealth.RestoreFromSaveData(data.Player.CurrentHp);
                if (playerController != null) playerController.RestoreFromSaveData(data.Player.Fuel, data.Player.Position);
            }

            LoadOfflineEarnings(data);
            
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

            var oreGained = new Dictionary<BlockTypeId, int>();
            foreach (var kvp in averages)
            {
                int amount = Mathf.RoundToInt(kvp.Value * minutesAway);
                if (amount > 0) oreGained[kvp.Key] = amount;
            }

            if (oreGained.Count == 0) return;
            GameManager.EventService.Dispatch(new OfflineEarningsReadyEvent(oreGained, minutesAway));
        }
    }
}
