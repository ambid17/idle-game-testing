using System;
using System.Collections.Generic;
using Processing;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // "select recipe" modal per processingImplementation.md: lists every recipe unlocked via its
    // chained UpgradeDefinition. Instantiate-into-container + Bind(model, onClick) pattern copied
    // from SkillTreePanelUI, but as a flat list rather than a graph - there's no prerequisite
    // visualization need here, IsRecipeUnlocked already filters to only what's selectable.
    public class ProcessingRecipeListModalUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private ProcessingRecipeRowUI rowPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<ProcessingRecipeRowUI> spawnedRows = new();
        private Action<int, ProcessingRecipeDefinition> onRecipeSelected;
        private int slotIndex;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (root != null) root.SetActive(false);
        }

        public void Initialize(Action<int, ProcessingRecipeDefinition> onRecipeSelected) => this.onRecipeSelected = onRecipeSelected;

        public void Show(int slotIndex)
        {
            this.slotIndex = slotIndex;
            if (root != null) root.SetActive(true);
            BuildRows();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        private void BuildRows()
        {
            if (rowPrefab == null || rowContainer == null)
            {
                Debug.LogError("ProcessingRecipeListModalUI.BuildRows: missing rowPrefab or rowContainer.");
                return;
            }

            foreach (var row in spawnedRows) Destroy(row.gameObject);
            spawnedRows.Clear();

            var database = GameManager.ProcessingRecipeDatabase;
            if (database == null)
            {
                Debug.LogError("ProcessingRecipeListModalUI.BuildRows: GameManager.ProcessingRecipeDatabase is not assigned.");
                return;
            }

            foreach (var recipe in database.Recipes)
            {
                if (recipe == null || !ProcessingManager.Instance.IsRecipeUnlocked(recipe)) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                row.Bind(recipe, OnRecipeClicked);
                row.gameObject.name = $"Row_{recipe.name}";
                spawnedRows.Add(row);
            }
        }

        private void OnRecipeClicked(ProcessingRecipeDefinition recipe)
        {
            onRecipeSelected?.Invoke(slotIndex, recipe);
            Close();
        }
    }
}
