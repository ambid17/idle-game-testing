using System;
using System.Collections.Generic;
using Economy;
using MapGeneration;
using UnityEngine;

namespace Processing
{
    [Serializable]
    public class RecipeIngredient
    {
        public BlockTypeId Material;
        public int Count = 1;
    }

    // One processed-good recipe per GameDesignDoc processingImplementation.md "each recipe will
    // have: a preset sale value, duration to process, materials required". Ingredients is a list
    // (rather than a single Material/Count pair) so prestige recipes like Steel (coal + iron)
    // fit the same shape without a redesign, even though every base recipe today has exactly one.
    [CreateAssetMenu(fileName = "ProcessingRecipeDefinition", menuName = "Processing/Recipe Definition")]
    public class ProcessingRecipeDefinition : ScriptableObject
    {
        public ProcessingRecipeId Id;
        public string DisplayName;
        public Sprite Icon;
        public List<RecipeIngredient> Ingredients = new();

        [Tooltip("Sell value of one crafted unit - mirrors BlockType.Value.")]
        public float SaleValue;

        [Tooltip("Seconds to craft one unit, before UpgradeManager.ProcessingSpeedMultiplier.")]
        public float DurationPerUnit;

        [Tooltip("The UpgradeDefinition that must be maxed (purchased) before this recipe is selectable.")]
        public UpgradeDefinition RequiredUpgrade;
    }
}
