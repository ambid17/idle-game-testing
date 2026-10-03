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
            // The artifact recipe is never sold, so it has no price or market movement to show.
            valueLabel.text = recipe.ProducesArtifact ? "+1 artifact" : $"${Depot.Instance.GoodUnitValue(recipe):0}";
            changeLabel.text = recipe.ProducesArtifact ? string.Empty : GoodsExchangeUI.FormatChange(GoodsMarket.Instance.Multiplier(recipe.Id));
            selectedHighlight.SetActive(selected);

            FormatIngredients(recipe);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(recipe));
        }

        // Past this many ingredients the "count + icon" chips overflow the column, so a recipe whose
        // ingredients are all 1 each (Artifact Synthesis) shows a compact icon grid instead.
        private const int MaxChipIngredients = 4;
        private const int CompactRows = 2;
        private const float CompactIconSize = 28f;
        private const float CompactIconSpacing = 3f;
        // Ores the Depot is short of are dimmed in the grid, in place of the chips' red count.
        private static readonly Color CompactShortTint = new(1f, 1f, 1f, 0.3f);

        private void FormatIngredients(ProcessingRecipeDefinition recipe)
        {
            foreach (var row in spawnedRows) Destroy(row);
            spawnedRows.Clear();

            if (recipe.Ingredients.Count > MaxChipIngredients && recipe.Ingredients.TrueForAll(i => i.Count == 1))
            {
                BuildCompactIngredients(recipe);
                return;
            }

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

        // Built in code rather than from a prefab: it's just a GridLayoutGroup of ore icons, and
        // only the one many-ingredient recipe ever uses it.
        private void BuildCompactIngredients(ProcessingRecipeDefinition recipe)
        {
            var grid = new GameObject("CompactIngredients", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(ingredientContainer, false);
            // Reports one chip's width to the row's layout so the time/value columns stay aligned
            // with the other rows - the icons draw past it into the column's free space.
            var chipLayout = ingredientRowPrefab.GetComponent<LayoutElement>();
            var gridLayout = grid.GetComponent<LayoutElement>();
            gridLayout.minWidth = chipLayout.minWidth;
            gridLayout.preferredWidth = chipLayout.preferredWidth;
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(CompactIconSize, CompactIconSize);
            layout.spacing = new Vector2(CompactIconSpacing, CompactIconSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            layout.constraintCount = CompactRows;
            layout.childAlignment = TextAnchor.MiddleLeft;
            spawnedRows.Add(grid);

            foreach (var ingredient in recipe.Ingredients)
            {
                var blockType = GameManager.BlockTypeDatabase.Get((byte)ingredient.Material);
                Depot.Instance.StoredOres.TryGetValue(ingredient.Material, out var stored);

                var iconObject = new GameObject($"Ingredient_{blockType.DisplayName}", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(grid.transform, false);
                var image = iconObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.SetIcon(blockType.Icon, blockType.IconBackground);

                if (stored >= ingredient.Count) continue;
                foreach (var part in iconObject.GetComponentsInChildren<Image>(true)) part.color = CompactShortTint;
            }
        }
    }
}
