using System;
using System.Collections.Generic;
using Automation;
using MapGeneration;
using UnityEngine;

namespace Persistence
{
    [Serializable]
    public class UpgradeLevelEntry
    {
        public string UpgradeId;
        public int Level;
    }

    [Serializable]
    public class OreAverageEntry
    {
        public BlockTypeId Id;
        public float AveragePerMinute;
    }

    [Serializable]
    public class AutomationSettingsSaveData
    {
        public TargetMode StorageDroneTargetMode;
        public TargetMode FuelDroneTargetMode;
        public float FuelSpendingCapPercent;
    }

    [Serializable]
    public class OreCountEntry
    {
        public BlockTypeId Id;
        public int Count;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public float CurrentHp;
        public float Fuel;
        public Vector3 Position;
        public int ArtifactCount;
        public List<OreCountEntry> OreCounts = new();
    }

    // Minimal save file per the resolved persistence decision - Wallet/UpgradeManager/idle-average/
    // AutomationSettings/Depot/Player state plus a last-active timestamp. Map/chunk data lives in
    // the sibling map.json (MapGeneration/Persistence's MapSaveData), not here.
    [Serializable]
    public class GameSaveData
    {
        public double Dollars;
        public List<UpgradeLevelEntry> UpgradeLevels = new();
        // Prestige points and prestige upgrade levels per GameDesignDoc "# Prestige" - deliberately
        // siblings of Dollars/UpgradeLevels above, not a separate file: they don't need independent
        // lifecycle, they just must never be touched by PrestigeManager.ExecutePrestige's in-memory
        // reset (the next autosave captures the correct post-prestige state automatically).
        public double PrestigePoints;
        public List<UpgradeLevelEntry> PrestigeUpgradeLevels = new();
        public List<OreAverageEntry> IdleAverages = new();
        public AutomationSettingsSaveData AutomationSettings = new();
        public List<OreCountEntry> DepotOres = new();
        public PlayerSaveData Player = new();
        // ISO-8601 string, since JsonUtility can't serialize DateTime directly.
        public string LastActiveUtcTimestamp;
    }
}
