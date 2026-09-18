using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One row of a crafted-goods listing in DepotUI - mirrors OreRowUI.cs exactly, but bound to
    // ProcessingRecipeId/ProcessingRecipeDatabase instead of BlockTypeId/BlockTypeDatabase since
    // goods and ore are stored in separate Depot dictionaries.
    public class GoodsRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text valueLabel;

        private ProcessingRecipeDefinition recipe;

        public ProcessingRecipeId RecipeId { get; private set; }

        public void Bind(ProcessingRecipeDefinition recipe)
        {
            this.recipe = recipe;
            RecipeId = recipe.Id;
            icon.sprite = recipe.Icon;

            string displayName = string.IsNullOrEmpty(recipe.DisplayName) ? recipe.name : recipe.DisplayName;
            nameLabel.text = displayName;
        }

        public void SetCount(int count)
        {
            countLabel.text = count.ToString();

            var saleValue = recipe.SaleValue;
            var totalValue = saleValue * count;
            valueLabel.text = $"${totalValue:0.##}";
        }

        public void SetValue(float value)
        {
            if (valueLabel != null) valueLabel.text = $"${value:0.##}";
        }
    }
}
