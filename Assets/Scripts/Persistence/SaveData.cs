using System;
using System.Collections.Generic;
using Automation;
using MapGeneration;

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

    // Minimal save file per the resolved persistence decision - Wallet/UpgradeManager/idle-average/
    // AutomationSettings state plus a last-active timestamp. Map/chunk data is deliberately not
    // included (MapGeneration/Persistence's MapSaveData stays unwired, out of scope here).
    [Serializable]
    public class GameSaveData
    {
        public double Dollars;
        public List<UpgradeLevelEntry> UpgradeLevels = new();
        public List<OreAverageEntry> IdleAverages = new();
        public AutomationSettingsSaveData AutomationSettings = new();
        // ISO-8601 string, since JsonUtility can't serialize DateTime directly.
        public string LastActiveUtcTimestamp;
    }
}
