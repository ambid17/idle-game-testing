using System.Collections.Generic;
using UnityEngine;

namespace Processing
{
    // Mirrors MapGeneration.BlockTypeDatabase's Get-by-id lookup, but for crafted goods instead
    // of terrain/ore block types.
    [CreateAssetMenu(fileName = "ProcessingRecipeDatabase", menuName = "Processing/Recipe Database")]
    public class ProcessingRecipeDatabase : ScriptableObject
    {
        public List<ProcessingRecipeDefinition> Recipes = new();

        private Dictionary<ProcessingRecipeId, ProcessingRecipeDefinition> lookup;

        public ProcessingRecipeDefinition Get(ProcessingRecipeId id)
        {
            if (lookup == null) BuildLookup();
            lookup.TryGetValue(id, out var recipe);
            return recipe;
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<ProcessingRecipeId, ProcessingRecipeDefinition>();
            foreach (var recipe in Recipes)
            {
                if (recipe != null) lookup[recipe.Id] = recipe;
            }
        }

        private void OnEnable() => lookup = null;
    }
}
