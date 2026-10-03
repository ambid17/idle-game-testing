using System;
using System.Collections.Generic;
using Automation;
using MapGeneration;
using Processing;
using Tutorial;
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
        public StorageDroneDepositMode StorageDroneDepositMode;
        // Inverted so older saves (where this is missing, i.e. false) keep notifications on.
        public bool DroneNotificationsMuted;
    }

    [Serializable]
    public class OreCountEntry
    {
        public BlockTypeId Id;
        public int Count;
    }

    [Serializable]
    public class GoodsCountEntry
    {
        public ProcessingRecipeId Id;
        public int Count;
    }

    // One good's Processing.GoodsMarket state: its recent price multipliers (the last one is the
    // current price) and any boom/crash in progress.
    [Serializable]
    public class GoodsMarketSaveEntry
    {
        public ProcessingRecipeId Id;
        public List<float> History = new();
        public float EventMultiplierTarget = 1f;
        public float EventSecondsRemaining;
    }

    [Serializable]
    public class ProcessingJobSaveEntry
    {
        public int SlotIndex;
        public ProcessingRecipeId RecipeId;
        // Units still to be made, and the seconds left on the one in progress.
        public int Quantity;
        public float TimeRemainingSeconds;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public float CurrentHp;
        public float Fuel;
        public Vector3 Position;
        public List<OreCountEntry> OreCounts = new();
        // PlayerMining's "this block can't be mined" notification tally per block type, so its
        // show-N-times cap holds across reloads. Not reset by prestige. Empty on older saves.
        public List<OreCountEntry> UnmineableNotifyCounts = new();
        // PlayerMining's "can't dig while flying" / "can't dig up" hint tallies, same show-N-times
        // cap. 0 on older saves.
        public int AirDigHintCount;
        public int DigUpHintCount;
    }

    // A Chest that was still active (unlooted) at save time - see Economy.Chest/ChestSpawner.
    [Serializable]
    public class ChestSaveEntry
    {
        public Vector3 Position;
        public List<OreCountEntry> OreCounts = new();
    }

    // Cumulative stats backing Platform.AchievementManager - never reset by prestige. The local
    // save is the source of truth for achievements: UnlockedAchievements holds Steam API names so
    // unlocks earned while Steam wasn't running get re-pushed on the next launch.
    [Serializable]
    public class LifetimeStats
    {
        public long BlocksMined;
        public int DeepestLayerIndex = -1;
        public double DollarsEarned;
        public int ArtifactsFound;
        public List<BlockTypeId> OreTypesMined = new();
        public List<BlockTypeId> PowerUpTypesCollected = new();
        public List<ProcessingRecipeId> RecipesCompleted = new();
        public List<int> HazardDeathReasons = new();
        public List<string> UnlockedAchievements = new();
    }

    // Minimal save file per the resolved persistence decision - Wallet/UpgradeManager/idle-average/
    // AutomationSettings/Depot/Player state plus a last-active timestamp. Map/chunk data lives in
    // the sibling map.json (MapGeneration/Persistence's MapSaveData), not here.
    [Serializable]
    public class GameSaveData
    {
        public double Dollars;
        // Wallet.ArtifactCount - a sibling of Dollars rather than nested under Player, since
        // artifacts are banked directly to the Wallet, not carried in PlayerInventory.
        public int ArtifactCount;
        // Wallet.DollarsEarnedThisRun (Prestige_GrantFunding basis) and PrestigeManager.PrestigeCount
        // (Prestige_Legacy basis) - both default to 0 on older saves, which is the correct fallback.
        public double DollarsEarnedThisRun;
        public int PrestigeCount;
        public List<UpgradeLevelEntry> UpgradeLevels = new();
        // Prestige upgrade levels per GameDesignDoc "# Prestige" - deliberately a sibling of
        // Dollars/UpgradeLevels above, not a separate file: it doesn't need independent lifecycle, it
        // just must never be touched by PrestigeManager.ExecutePrestige's in-memory reset (the next
        // autosave captures the correct post-prestige state automatically).
        public List<UpgradeLevelEntry> PrestigeUpgradeLevels = new();
        // Queued (paid for, not yet applied) prestige upgrade levels - a player who queues purchases
        // then closes the game before prestiging must not lose the artifacts they already spent.
        public List<UpgradeLevelEntry> PrestigeQueuedUpgradeLevels = new();
        public List<OreAverageEntry> IdleAverages = new();
        public AutomationSettingsSaveData AutomationSettings = new();
        public List<OreCountEntry> DepotOres = new();
        // Depot.SellLockedOres - ores padlocked against selling. Empty on older saves (nothing locked).
        public List<BlockTypeId> DepotSellLockedOres = new();
        // Processing Center (processingImplementation.md): Depot's crafted-goods bank and any
        // in-progress jobs, siblings of DepotOres/UpgradeLevels for the same reason - no
        // independent lifecycle.
        public List<GoodsCountEntry> DepotGoods = new();
        // Depot.DiscoveredOres/DiscoveredGoods - everything ever banked, which gates the Depot and
        // miner dashboard rows. Not reset by prestige. Empty on older saves (SaveService seeds
        // from LifetimeStats).
        public List<BlockTypeId> DepotDiscoveredOres = new();
        public List<ProcessingRecipeId> DepotDiscoveredGoods = new();
        public List<ProcessingJobSaveEntry> ProcessingJobs = new();
        // ProcessingManager.UncollectedCompletions - jobs that finished but the player hasn't
        // opened the Processing panel since, so the completion badge survives a save/reload.
        public int ProcessingUncollectedCompletions;
        // Processing.GoodsMarket - per-good price history. Not reset by prestige. Empty on older
        // saves, where the market just starts fresh.
        public List<GoodsMarketSaveEntry> GoodsMarket = new();
        public PlayerSaveData Player = new();
        // Chests still active (unlooted) at save time - see Economy.ChestRegistry.
        public List<ChestSaveEntry> Chests = new();
        // Tutorial.TutorialManager: which one-time tutorial popups have already been shown, so they
        // don't repeat after reload.
        public List<TutorialId> ShownTutorials = new();
        // Platform.AchievementManager - cumulative across prestiges.
        public LifetimeStats LifetimeStats = new();
        // Critters.CritterCollection - jar, lifetime collection, caught pockets and automaton hats.
        public Critters.CritterSaveData Critters = new();
        // Museum.RuneCollection - found/donated artifact runes, worn accessories. Not reset by prestige.
        public Museum.MuseumSaveData Museum = new();
        // Economy.SkyArtifact - the one-time relic on the cloud 200m up. Not reset by prestige.
        public bool SkyArtifactCollected;
        // Story.StoryManager - stage, Keystones taken, ending. Not reset by prestige.
        public Story.StorySaveData Story = new();
        // ISO-8601 string, since JsonUtility can't serialize DateTime directly.
        public string LastActiveUtcTimestamp;
    }
}
