using System.Collections.Generic;
using MapGeneration;

namespace RunModifiers
{
    // Pure translation of (definition, rolled state) into generation/mining numbers - no scene
    // access, so MineWorld (and anything headless) can call it.
    public static class RunModifierResolver
    {
        public static LayerGenerationTweaks BuildTweaks(RunModifierDefinition def, RunModifierState state, int layerIndex)
        {
            if (def == null || state == null) return LayerGenerationTweaks.None;

            bool isTarget = def.UsesTargetLayer && layerIndex == state.TargetLayer;
            var tweaks = new LayerGenerationTweaks
            {
                AllOreWeightMultiplier = def.AllOreWeightMultiplier * (isTarget ? def.TargetLayerOreWeightMultiplier : 1f),
                UseNextLayerOreTable = isTarget && def.TargetLayerUsesNextOreTable,
                OreTierOddsBonus = def.OreTierOddsBonus,
                DisableVeins = def.DisableVeins,
                VeinSizeMultiplier = def.VeinSizeMultiplier,
                HazardChanceMultiplier = def.HazardChanceMultiplier,
                PowerUpSpawnRateBonus = def.PowerUpSpawnRateBonus,
                ExtraGuaranteedArtifacts = def.ExtraGuaranteedArtifacts,
                ArtifactBonusChanceMultiplier = def.ArtifactBonusChanceMultiplier,
                EmptyPocketChanceMultiplier = def.EmptyPocketChanceMultiplier,
                EmptyPocketSizeMultiplier = def.EmptyPocketSizeMultiplier,
            };

            if (def.UsesOreA && def.OreAWeightMultiplier != 1f) SetMultiplier(ref tweaks.OreWeightMultipliers, state.OreA, def.OreAWeightMultiplier);
            if (def.UsesOreB && def.OreBWeightMultiplier != 1f) SetMultiplier(ref tweaks.OreWeightMultipliers, state.OreB, def.OreBWeightMultiplier);
            if (def.UsesOreA && def.OreAVeinSizeMultiplier != 1f) SetMultiplier(ref tweaks.OreVeinSizeMultipliers, state.OreA, def.OreAVeinSizeMultiplier);

            foreach (var hazard in def.HazardWeightMultipliers)
            {
                tweaks.HazardWeightMultipliers ??= new Dictionary<CustomBehavior, float>();
                tweaks.HazardWeightMultipliers[hazard.Hazard] = hazard.Multiplier;
            }

            tweaks.Features.AddRange(def.Features);
            if (isTarget) tweaks.Features.AddRange(def.TargetLayerFeatures);
            return tweaks;
        }

        private static void SetMultiplier(ref Dictionary<BlockTypeId, float> map, BlockTypeId id, float multiplier)
        {
            map ??= new Dictionary<BlockTypeId, float>();
            map[id] = multiplier;
        }

        public static float SellValueMultiplier(RunModifierDefinition def, RunModifierState state, BlockTypeId ore)
        {
            if (def == null) return 1f;
            float multiplier = def.SellValueMultiplier;
            if (def.UsesOreA && ore == state.OreA) multiplier *= def.OreASellMultiplier;
            if (def.UsesOreB && ore == state.OreB) multiplier *= def.OreBSellMultiplier;
            return multiplier;
        }

        public static bool IsTargetLayer(RunModifierDefinition def, RunModifierState state, int layerIndex) =>
            def != null && def.UsesTargetLayer && state.TargetLayer == layerIndex;
    }
}
