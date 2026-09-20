using Economy;
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

        private void Start()
        {
            if (icon == null) Debug.LogError("GoodsRowUI: no icon Image assigned.");
            if (nameLabel == null) Debug.LogError("GoodsRowUI: no nameLabel TMP_Text assigned.");
            if (countLabel == null) Debug.LogError("GoodsRowUI: no countLabel TMP_Text assigned.");
            if (valueLabel == null) Debug.LogError("GoodsRowUI: no valueLabel TMP_Text assigned.");
        }

        public void Bind(ProcessingRecipeDefinition recipe)
        {
            this.recipe = recipe;
            RecipeId = recipe.Id;
            icon.sprite = recipe.Icon;

            string displayName = string.IsNullOrEmpty(recipe.DisplayName) ? recipe.name : recipe.DisplayName;
            nameLabel.text = displayName;
        }

        public float SetCount(int count)
        {
            countLabel.text = count.ToString();

            var saleValue = recipe.SaleValue;
            var totalValue = saleValue * UpgradeManager.Instance.ProcessingGoodsSellMultiplier * count;
            valueLabel.text = $"${totalValue:0}";

            return totalValue;
        }
    }
}
