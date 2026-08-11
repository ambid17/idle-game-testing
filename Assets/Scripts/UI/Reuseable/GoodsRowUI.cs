using Processing;
using TMPro;
using UnityEngine;

namespace UI
{
    // One row of a crafted-goods listing in DepotUI - mirrors OreRowUI.cs exactly, but bound to
    // ProcessingRecipeId/ProcessingRecipeDatabase instead of BlockTypeId/BlockTypeDatabase since
    // goods and ore are stored in separate Depot dictionaries.
    public class GoodsRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text valueLabel;

        private ProcessingRecipeDatabase recipeDatabase => GameManager.ProcessingRecipeDatabase;

        public ProcessingRecipeId RecipeId { get; private set; }

        public void Bind(ProcessingRecipeId id, string displayName)
        {
            RecipeId = id;
            nameLabel.text = displayName;
        }

        public void SetCount(int count)
        {
            countLabel.text = count.ToString();

            var saleValue = recipeDatabase.Get(RecipeId)?.SaleValue ?? 0;
            var totalValue = saleValue * count;
            valueLabel.text = $"${totalValue:0.##}";
        }

        public void SetValue(float value)
        {
            if (valueLabel != null) valueLabel.text = $"${value:0.##}";
        }
    }
}
