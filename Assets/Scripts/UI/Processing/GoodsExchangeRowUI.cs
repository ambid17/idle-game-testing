using System;
using Economy;
using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // One good in GoodsExchangeUI's list: icon, name, how many are banked, and its current price
    // with how far that sits above or below the recipe's base value. Clicking it selects the good
    // for the detail pane - mirrors ProcessingRecipeRowUI's Bind(model, onClicked) shape.
    public class GoodsExchangeRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text changeLabel;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedHighlight;

        private ProcessingRecipeDefinition recipe;

        private void Start()
        {
            if (icon == null) Debug.LogError($"GoodsExchangeRowUI.icon is not assigned on {gameObject.name}.");
            if (nameLabel == null) Debug.LogError($"GoodsExchangeRowUI.nameLabel is not assigned on {gameObject.name}.");
            if (countLabel == null) Debug.LogError($"GoodsExchangeRowUI.countLabel is not assigned on {gameObject.name}.");
            if (priceLabel == null) Debug.LogError($"GoodsExchangeRowUI.priceLabel is not assigned on {gameObject.name}.");
            if (changeLabel == null) Debug.LogError($"GoodsExchangeRowUI.changeLabel is not assigned on {gameObject.name}.");
            if (button == null) Debug.LogError($"GoodsExchangeRowUI.button is not assigned on {gameObject.name}.");
            if (selectedHighlight == null) Debug.LogError($"GoodsExchangeRowUI.selectedHighlight is not assigned on {gameObject.name}.");
        }

        public void Bind(ProcessingRecipeDefinition recipe, Action<ProcessingRecipeDefinition> onClicked)
        {
            this.recipe = recipe;
            icon.sprite = recipe.Icon;
            nameLabel.text = string.IsNullOrEmpty(recipe.DisplayName) ? recipe.name : recipe.DisplayName;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(recipe));
        }

        public void Refresh()
        {
            Depot.Instance.StoredGoods.TryGetValue(recipe.Id, out var count);
            countLabel.text = $"x{count}";
            priceLabel.text = $"${Depot.Instance.GoodUnitValue(recipe):0}";
            changeLabel.text = GoodsExchangeUI.FormatChange(GoodsMarket.Instance.Multiplier(recipe.Id));
        }

        public void SetSelected(bool selected) => selectedHighlight.SetActive(selected);
    }
}
