using System;
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
        [SerializeField] private TMP_Text ingredientsLabel;
        [SerializeField] private Button button;

        public void Bind(ProcessingRecipeDefinition recipe, Action<ProcessingRecipeDefinition> onClicked)
        {
            icon.sprite = recipe.Icon;
            nameLabel.text = recipe.DisplayName;
            ingredientsLabel.text = FormatIngredients(recipe);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(recipe));
        }

        private static string FormatIngredients(ProcessingRecipeDefinition recipe)
        {
            var parts = new string[recipe.Ingredients.Count];
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                var ingredient = recipe.Ingredients[i];
                parts[i] = $"- {ingredient.Count} {ingredient.Material}";
            }
            return string.Join("\n", parts);
        }
    }
}
