using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // One selectable row in ProcessingRecipeListModalUI - mirrors SkillTreeNodeUI's
    // Bind(model, onClicked) shape.
    public class ProcessingRecipeRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button button;
        [SerializeField] private ProcessingIngredientRow ingredientRowPrefab;

        private List<GameObject> spawnedRows;

        public void Bind(ProcessingRecipeDefinition recipe, Action<ProcessingRecipeDefinition> onClicked)
        {
            if(recipe.Icon != null) icon.sprite = recipe.Icon;
            nameLabel.text = recipe.DisplayName;
            gameObject.name = $"ProcessingRecipeRowUI_{recipe.DisplayName}";

            FormatIngredients(recipe);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(recipe));
        }

        private void FormatIngredients(ProcessingRecipeDefinition recipe)
        {
            if(spawnedRows == null) spawnedRows = new List<GameObject>();
            else
            {
                foreach (var row in spawnedRows)
                {
                    Destroy(row);
                }
                spawnedRows.Clear();
            }

            // Create new ingredient rows
            foreach (var ingredient in recipe.Ingredients)
            {
                var row = Instantiate(ingredientRowPrefab, transform);
                var blockType =GameManager.BlockTypeDatabase.Get((byte)ingredient.Material);
                row.Bind(ingredient.Count, blockType.Icon);
                row.gameObject.name = $"ProcessingIngredientRowUI_{blockType.DisplayName}";
                spawnedRows.Add(row.gameObject);
            }
        }
    }
}
