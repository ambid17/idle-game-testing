using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Automation;
using Economy;
using Events;
using MapGeneration;
using UnityEngine;

namespace Persistence
{
    // Minimal save/load per the resolved persistence decision: Wallet dollars, all UpgradeManager
    // levels, IdleEarningsTracker's rolling averages, AutomationSettings, and a last-active
    // timestamp (used to compute "minutes away" for the offline-earnings screen). Follows
    // MapGeneration/Persistence/ChunkSerializer's JsonUtility approach, but unlike that (never
    // actually called) code, this is wired to Unity's lifecycle - nothing in the project persisted
    // anything to disk before this.
    public class SaveService : Singleton<SaveService>
    {
        private const float AutosaveIntervalSeconds = 60f;
        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        protected override void Initialize()
        {
            base.Initialize();
            // OS force-kill (especially on mobile) doesn't reliably call OnApplicationQuit, so a
            // periodic safety-net autosave backs up OnApplicationQuit/OnApplicationPause below.
            InvokeRepeating(nameof(Save), AutosaveIntervalSeconds, AutosaveIntervalSeconds);
        }

        private void OnApplicationQuit() => Save();
        private void OnApplicationPause(bool paused) { if (paused) Save(); }

        public void Save()
        {
            var data = new GameSaveData
            {
                Dollars = Wallet.Instance.Dollars,
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

            foreach (var kvp in IdleEarningsTracker.Instance.AveragePerMinute)
            {
                data.IdleAverages.Add(new OreAverageEntry { Id = kvp.Key, AveragePerMinute = kvp.Value });
            }

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService.Save: failed to write save file at {SavePath}: {e}");
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

        // Restores Wallet/UpgradeManager/AutomationSettings/IdleEarningsTracker state, computes
        // minutes-away and the resulting offline ore, and dispatches OfflineEarningsReadyEvent.
        // Does NOT deposit the ore into the Depot yet - per the resolved idle-earnings decision,
        // that only happens once the player acknowledges the offline-earnings screen.
        public void ApplyLoadedData(GameSaveData data)
        {
            if (data == null) return;

            Wallet.Instance.SetDollars(data.Dollars);

            foreach (var entry in data.UpgradeLevels)
            {
                UpgradeManager.Instance.SetLevel(entry.UpgradeId, entry.Level);
            }

            if (data.AutomationSettings != null)
            {
                AutomationSettings.Instance.RestoreFromSaveData(
                    data.AutomationSettings.StorageDroneTargetMode,
                    data.AutomationSettings.FuelDroneTargetMode,
                    data.AutomationSettings.FuelSpendingCapPercent);
            }

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

        private static float ComputeMinutesAway(string lastActiveUtcTimestamp)
        {
            if (string.IsNullOrEmpty(lastActiveUtcTimestamp)) return 0f;
            if (!DateTime.TryParse(lastActiveUtcTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastActive)) return 0f;

            return Mathf.Max(0f, (float)(DateTime.UtcNow - lastActive).TotalMinutes);
        }
    }
}
