using System;
using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace RunModifiers
{
    // Blessing = pure upside. Gamble = upside paired with a drawback, with a bigger upside to pay for it.
    public enum RunModifierKind
    {
        Blessing = 0,
        Gamble = 1,
    }

    // At most one modifier per family is offered at once (None = unrestricted).
    public enum RunModifierFamily
    {
        None = 0,
        OreWeighting = 1,
        HazardSurge = 2,
        SpecialLayer = 3,
    }

    [Serializable]
    public class HazardWeightMultiplier
    {
        public CustomBehavior Hazard = CustomBehavior.Explosive;
        [Min(0f)] public float Multiplier = 1f;
    }

    // One pick-1-of-3 run modifier offered at prestige. Data-driven: every modifier is a
    // combination of the knobs below (all defaulting to "no change"), plus up to three
    // parameters rolled per offer (OreA, OreB, TargetLayer) that the knobs can target. Map-shape
    // changes are MapFeatureDefinitions, so new structures need no code here.
    //
    // Description supports tokens filled from the rolled parameters: {oreA} {oreB} {layer} {amount} {reward}.
    [CreateAssetMenu(fileName = "RunModifier", menuName = "Run Modifiers/Run Modifier")]
    public class RunModifierDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, unique id - saved in map.json, never rename once shipped.")]
        public string Id;
        public string DisplayName;
        [TextArea(2, 5)] public string Description;
        public RunModifierKind Kind;
        public RunModifierFamily Family;
        [Tooltip("Relative odds of being offered.")]
        [Min(0f)] public float OfferWeight = 1f;

        [Header("Rolled parameters")]
        public bool UsesOreA;
        public bool UsesOreB;
        public bool UsesTargetLayer;
        [Tooltip("OreA/OreB are drawn from ores authored in the OreTables of these layers.")]
        [Min(0)] public int OrePoolMinLayer = 1;
        [Min(0)] public int OrePoolMaxLayer = 6;
        [Min(1)] public int TargetLayerMin = 2;
        [Min(1)] public int TargetLayerMax = 6;

        [Header("Generation - every layer")]
        [Min(0f)] public float AllOreWeightMultiplier = 1f;
        [Min(0f)] public float OreAWeightMultiplier = 1f;
        [Min(0f)] public float OreBWeightMultiplier = 1f;
        [Min(0f)] public float VeinSizeMultiplier = 1f;
        [Min(0f)] public float OreAVeinSizeMultiplier = 1f;
        public bool DisableVeins;
        [Range(0f, 1f)] public float OreTierOddsBonus;
        [Min(0f)] public float HazardChanceMultiplier = 1f;
        public List<HazardWeightMultiplier> HazardWeightMultipliers = new();
        [Min(0f)] public float PowerUpSpawnRateBonus;
        [Min(0)] public int ExtraGuaranteedArtifacts;
        [Min(0f)] public float ArtifactBonusChanceMultiplier = 1f;
        [Min(0f)] public float EmptyPocketChanceMultiplier = 1f;
        [Min(0f)] public float EmptyPocketSizeMultiplier = 1f;
        [Tooltip("Run on every layer (each feature's own Placement still decides which layers).")]
        public List<MapFeatureDefinition> Features = new();

        [Header("Generation - target layer only")]
        public bool TargetLayerUsesNextOreTable;
        [Min(0f)] public float TargetLayerOreWeightMultiplier = 1f;
        [Tooltip("Run only on the rolled target layer. The feature's own Placement still applies, so leave its layer range open.")]
        public List<MapFeatureDefinition> TargetLayerFeatures = new();

        [Header("Mining")]
        [Min(0.05f)] public float BlockHealthMultiplier = 1f;
        [Tooltip("Fog reveal radius multiplier while mining on the target layer (< 1 = darker).")]
        [Min(0f)] public float TargetLayerFogRadiusMultiplier = 1f;
        [Tooltip("Ore picked up per mined ore cell on the target layer.")]
        [Min(1)] public int TargetLayerOreYieldMultiplier = 1;

        [Header("Economy")]
        [Min(0f)] public float SellValueMultiplier = 1f;
        [Min(0f)] public float OreASellMultiplier = 1f;
        [Min(0f)] public float OreBSellMultiplier = 1f;

        [Header("Contract (OreA is the contract ore)")]
        public bool IsContract;
        [Min(1)] public int ContractAmountMin = 100;
        [Min(1)] public int ContractAmountMax = 200;
        [Min(0)] public int ContractRewardArtifacts = 5;
    }
}
