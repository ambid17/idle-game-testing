using System;
using System.Collections.Generic;
using Economy;
using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // One selectable row in ProcessingRecipeListModalUI: the recipe, what one unit costs (red for
    // any ore the Depot is short of), how long it takes, and what it currently sells for on the
    // Exchange. Mirrors SkillTreeNodeUI's Bind(model, onClicked) shape.
    public class ProcessingRecipeRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button button;
        [SerializeField] private Transform ingredientContainer;
        [SerializeField] private ProcessingIngredientRow ingredientRowPrefab;
        [SerializeField] private TMP_Text durationLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text changeLabel;
        [SerializeField] private GameObject selectedHighlight;

        private readonly List<GameObject> spawnedRows = new();

        private void Start()
        {
            if (icon == null) Debug.LogError($"ProcessingRecipeRowUI.icon is not assigned on {gameObject.name}.");
            if (nameLabel == null) Debug.LogError($"ProcessingRecipeRowUI.nameLabel is not assigned on {gameObject.name}.");
            if (button == null) Debug.LogError($"ProcessingRecipeRowUI.button is not assigned on {gameObject.name}.");
            if (ingredientContainer == null) Debug.LogError($"ProcessingRecipeRowUI.ingredientContainer is not assigned on {gameObject.name}.");
            if (ingredientRowPrefab == null) Debug.LogError($"ProcessingRecipeRowUI.ingredientRowPrefab is not assigned on {gameObject.name}.");
            if (durationLabel == null) Debug.LogError($"ProcessingRecipeRowUI.durationLabel is not assigned on {gameObject.name}.");
            if (valueLabel == null) Debug.LogError($"ProcessingRecipeRowUI.valueLabel is not assigned on {gameObject.name}.");
            if (changeLabel == null) Debug.LogError($"ProcessingRecipeRowUI.changeLabel is not assigned on {gameObject.name}.");
            if (selectedHighlight == null) Debug.LogError($"ProcessingRecipeRowUI.selectedHighlight is not assigned on {gameObject.name}.");
        }

        // `selected` marks the recipe the slot is already set to.
        public void Bind(ProcessingRecipeDefinition recipe, bool selected, Action<ProcessingRecipeDefinition> onClicked)
        {
            if (recipe.Icon != null) icon.sprite = recipe.Icon;
            nameLabel.text = recipe.DisplayName;
            gameObject.name = $"ProcessingRecipeRowUI_{recipe.DisplayName}";

            durationLabel.text = $"{ProcessingManager.Instance.UnitDuration(recipe):0.#}s";
            valueLabel.text = $"${Depot.Instance.GoodUnitValue(recipe):0}";
            changeLabel.text = GoodsExchangeUI.FormatChange(GoodsMarket.Instance.Multiplier(recipe.Id));
            selectedHighlight.SetActive(selected);

            FormatIngredients(recipe);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(recipe));
        }

        private void FormatIngredients(ProcessingRecipeDefinition recipe)
        {
            foreach (var row in spawnedRows) Destroy(row);
            spawnedRows.Clear();

            foreach (var ingredient in recipe.Ingredients)
            {
                var row = Instantiate(ingredientRowPrefab, ingredientContainer);
                var blockType = GameManager.BlockTypeDatabase.Get((byte)ingredient.Material);
                Depot.Instance.StoredOres.TryGetValue(ingredient.Material, out var stored);
                row.Bind(ingredient.Count, blockType.Icon, blockType.IconBackground, stored >= ingredient.Count);
                row.gameObject.name = $"ProcessingIngredientRowUI_{blockType.DisplayName}";
                spawnedRows.Add(row.gameObject);
            }
        }
    }
}
