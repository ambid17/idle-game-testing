using System.Collections.Generic;
using Economy;
using Events;
using MapGeneration;
using Persistence;
using Player;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using UnityEngine;

namespace Platform
{
    public enum AchievementId
    {
        FirstBlock,
        Layer4,
        Layer8,
        Layer11,
        AllOres,
        Artifacts100,
        AllPowerUps,
        Earn1M,
        MaxUpgrade,
        MaxAutomatons,
        AllRecipes,
        LayerClear,
        FirstPrestige,
        Prestige10,
        HazardDeaths
    }

    // Tracks cumulative LifetimeStats (saved in save.json, never reset by prestige) and unlocks
    // Steam achievements from them. The local save is the source of truth: unlocks are recorded in
    // LifetimeStats.UnlockedAchievements first, then pushed to Steam if it's running, and every
    // local unlock is re-pushed after each load - so progress made offline (or before an
    // achievement existed) still reaches Steam. Lives on the GameManager GameObject and is
    // accessed via GameManager.AchievementManager.
    public class AchievementManager : MonoBehaviour
    {
        // Steam API names - must match the achievements defined in Steamworks App Admin exactly.
        private static readonly Dictionary<AchievementId, string> ApiNames = new()
        {
            { AchievementId.FirstBlock, "ACH_FIRST_BLOCK" },
            { AchievementId.Layer4, "ACH_LAYER_4" },
            { AchievementId.Layer8, "ACH_LAYER_8" },
            { AchievementId.Layer11, "ACH_LAYER_11" },
            { AchievementId.AllOres, "ACH_ALL_ORES" },
            { AchievementId.Artifacts100, "ACH_ARTIFACTS_100" },
            { AchievementId.AllPowerUps, "ACH_ALL_POWERUPS" },
            { AchievementId.Earn1M, "ACH_EARN_1M" },
            { AchievementId.MaxUpgrade, "ACH_MAX_UPGRADE" },
            { AchievementId.MaxAutomatons, "ACH_AUTOMATONS" },
            { AchievementId.AllRecipes, "ACH_ALL_RECIPES" },
            { AchievementId.LayerClear, "ACH_LAYER_CLEAR" },
            { AchievementId.FirstPrestige, "ACH_FIRST_PRESTIGE" },
            { AchievementId.Prestige10, "ACH_PRESTIGE_10" },
            { AchievementId.HazardDeaths, "ACH_HAZARD_DEATHS" },
        };

        // Layer indices are 0-based; the achievement names use the 1-based layer number.
        private const int Layer4Index = 3;
        private const int Layer8Index = 7;
        private const int Layer11Index = 10;
        private const int ArtifactsTarget = 100;
        private const double DollarsTarget = 1_000_000;
        private const float LayerClearFraction = 0.95f;
        private const int PrestigeTarget = 10;

        private static readonly DeathReason[] HazardDeathReasons =
        {
            DeathReason.Explosive, DeathReason.FallingRock, DeathReason.GasPocket, DeathReason.Lava
        };

        public LifetimeStats Stats { get; private set; } = new();

        private void OnEnable()
        {
            GameManager.EventService.Add<BlockMinedEvent>(OnBlockMined);
            GameManager.EventService.Add<DollarsEarnedEvent>(OnDollarsEarned);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<ProcessingJobCompletedEvent>(OnProcessingJobCompleted);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Add<PlayerDiedEvent>(OnPlayerDied);
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BlockMinedEvent>(OnBlockMined);
            GameManager.EventService.Remove<DollarsEarnedEvent>(OnDollarsEarned);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<ProcessingJobCompletedEvent>(OnProcessingJobCompleted);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Remove<PlayerDiedEvent>(OnPlayerDied);
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
        }

        // Called by SaveService.ApplyLoadedData, before LoadCompletedEvent fires.
        public void RestoreFromSaveData(LifetimeStats stats)
        {
            Stats = stats ?? new LifetimeStats();
        }

        private void OnBlockMined(BlockMinedEvent e)
        {
            Stats.BlocksMined++;
            if (e.LayerIndex > Stats.DeepestLayerIndex) Stats.DeepestLayerIndex = e.LayerIndex;

            switch (e.BlockType.Category)
            {
                case BlockCategory.Ore:
                    AddUnique(Stats.OreTypesMined, e.BlockType.Id);
                    break;
                case BlockCategory.PowerUp:
                    AddUnique(Stats.PowerUpTypesCollected, e.BlockType.Id);
                    break;
                case BlockCategory.Artifact:
                    Stats.ArtifactsFound++;
                    break;
            }

            // MineCell has already incremented the chunk's MinedCount by the time this fires.
            var chunk = GameManager.MapGenerationService.World.GetOrGenerateChunk(e.LayerIndex);
            if (chunk.CompletionRatio >= LayerClearFraction) Unlock(AchievementId.LayerClear);

            EvaluateStatAchievements();
        }

        private void OnDollarsEarned(DollarsEarnedEvent e)
        {
            Stats.DollarsEarned += e.Amount;
            if (Stats.DollarsEarned >= DollarsTarget) Unlock(AchievementId.Earn1M);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent e)
        {
            EvaluateUpgradeAchievements();
        }

        private void OnProcessingJobCompleted(ProcessingJobCompletedEvent e)
        {
            AddUnique(Stats.RecipesCompleted, e.Recipe.Id);
            EvaluateStatAchievements();
        }

        private void OnPrestigeCompleted(PrestigeCompletedEvent e)
        {
            EvaluatePrestigeAchievements();
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (System.Array.IndexOf(HazardDeathReasons, e.Reason) < 0) return;

            AddUnique(Stats.HazardDeathReasons, (int)e.Reason);
            EvaluateStatAchievements();
        }

        // Retroactively unlocks anything an existing save already qualifies for, then re-pushes
        // every local unlock in case Steam wasn't running when it was earned.
        private void OnLoadCompleted()
        {
            EvaluateStatAchievements();
            EvaluateUpgradeAchievements();
            EvaluatePrestigeAchievements();
            if (Stats.DollarsEarned >= DollarsTarget) Unlock(AchievementId.Earn1M);

            SyncAllToSteam();
        }

        private void EvaluateStatAchievements()
        {
            if (Stats.BlocksMined > 0) Unlock(AchievementId.FirstBlock);
            if (Stats.DeepestLayerIndex >= Layer4Index) Unlock(AchievementId.Layer4);
            if (Stats.DeepestLayerIndex >= Layer8Index) Unlock(AchievementId.Layer8);
            if (Stats.DeepestLayerIndex >= Layer11Index) Unlock(AchievementId.Layer11);
            if (Stats.ArtifactsFound >= ArtifactsTarget) Unlock(AchievementId.Artifacts100);
            if (Stats.HazardDeathReasons.Count >= HazardDeathReasons.Length) Unlock(AchievementId.HazardDeaths);

            if (Stats.OreTypesMined.Count >= CountBlockTypes(BlockCategory.Ore)) Unlock(AchievementId.AllOres);
            if (Stats.PowerUpTypesCollected.Count >= CountBlockTypes(BlockCategory.PowerUp)) Unlock(AchievementId.AllPowerUps);
            if (Stats.RecipesCompleted.Count >= GameManager.ProcessingRecipeDatabase.Recipes.Count) Unlock(AchievementId.AllRecipes);
        }

        private void EvaluateUpgradeAchievements()
        {
            foreach (var def in GameManager.UpgradeDatabase.Upgrades)
            {
                if (UpgradeManager.Instance.GetLevelIncludingPrestige(def) < def.MaxLevel) continue;

                Unlock(AchievementId.MaxUpgrade);
                if (def.Effect == UpgradeEffect.Automation_AutomatonCount) Unlock(AchievementId.MaxAutomatons);
            }
        }

        private void EvaluatePrestigeAchievements()
        {
            int count = PrestigeManager.Instance.PrestigeCount;
            if (count >= 1) Unlock(AchievementId.FirstPrestige);
            if (count >= PrestigeTarget) Unlock(AchievementId.Prestige10);
        }

        private static int CountBlockTypes(BlockCategory category)
        {
            int count = 0;
            foreach (var blockType in GameManager.BlockTypeDatabase.BlockTypes)
            {
                if (blockType.Category == category) count++;
            }
            return count;
        }

        private static void AddUnique<T>(List<T> list, T value)
        {
            if (!list.Contains(value)) list.Add(value);
        }

        public bool IsUnlocked(AchievementId id) => Stats.UnlockedAchievements.Contains(ApiNames[id]);

        private void Unlock(AchievementId id)
        {
            string apiName = ApiNames[id];
            if (Stats.UnlockedAchievements.Contains(apiName)) return;

            Stats.UnlockedAchievements.Add(apiName);
            Debug.Log($"AchievementManager: unlocked {apiName}");
            PushToSteam(apiName);
            StoreSteamStats();
        }

        private void SyncAllToSteam()
        {
            if (Stats.UnlockedAchievements.Count == 0) return;

            foreach (var apiName in Stats.UnlockedAchievements)
            {
                PushToSteam(apiName);
            }
            StoreSteamStats();
        }

        private void PushToSteam(string apiName)
        {
#if !DISABLESTEAMWORKS
            if (!GameManager.SteamManager.IsInitialized) return;

            // Fails if the achievement isn't defined (or published) in Steamworks App Admin yet.
            if (!SteamUserStats.SetAchievement(apiName))
            {
                Debug.LogWarning($"AchievementManager: SteamUserStats.SetAchievement({apiName}) failed - is it defined and published in Steamworks?");
            }
#endif
        }

        private void StoreSteamStats()
        {
#if !DISABLESTEAMWORKS
            if (!GameManager.SteamManager.IsInitialized) return;
            SteamUserStats.StoreStats();
#endif
        }

        // Dev-only: wipes local lifetime stats/unlocks and clears achievements on the Steam account.
        [ContextMenu("Reset All Achievements")]
        private void ResetAllAchievements()
        {
            Stats = new LifetimeStats();
#if !DISABLESTEAMWORKS
            if (GameManager.SteamManager.IsInitialized)
            {
                SteamUserStats.ResetAllStats(true);
                SteamUserStats.StoreStats();
            }
#endif
            Debug.Log("AchievementManager: reset all achievements and lifetime stats.");
        }
    }
}
