using System.Collections.Generic;
using Economy;
using Events;
using Interaction;
using Player;
using Processing;
using UI.Processing;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Processing Center panel per Assets/Docs/processingImplementation.md, in two tabs (TabGroupUI
    // on this panel root). Queue: one slot per ProcessingManager.SlotCount. Clicking a slot's
    // recipe image opens the recipe-picker modal; the slot itself then owns quantity selection and
    // starting/cancelling the job. Exchange (GoodsExchangeUI): the crafted goods and their sale at
    // the current GoodsMarket price - hidden until the first good has been crafted.
    // Per CLAUDE.md's UI panel rule, this controller stays enabled on the Panel GameObject and
    // only toggles the child rendererRoot - same shape as MuseumUI. Never mutates ProcessingManager
    // state directly from a UI callback; Start/Cancel go through request events so the modals stay
    // decoupled from this panel (same shape as MarketUI.OnPurchaseRequested).
    public class ProcessingUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button closeButton;

        [Header("Tabs")]
        [SerializeField] private TabGroupUI tabGroup;
        [SerializeField] private Button exchangeTabButton;

        [Header("Queue")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private ProcessingQueueSlotUI slotPrefab;

        [Header("Modals")]
        [SerializeField] private ProcessingRecipeListModalUI recipeListModal;

        private const int QueueTab = 0;

        private readonly List<ProcessingQueueSlotUI> spawnedSlots = new();

        private void Start()
        {
            if (tabGroup == null) Debug.LogError("ProcessingUI.tabGroup is not assigned.");
            if (exchangeTabButton == null) Debug.LogError("ProcessingUI.exchangeTabButton is not assigned.");
            closeButton.onClick.AddListener(Close);
            if (recipeListModal != null) recipeListModal.Initialize(OnRecipeSelected);
            rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<ProcessingStartRequestedEvent>(OnStartRequested);
            GameManager.EventService.Add<ProcessingCancelRequestedEvent>(OnCancelRequested);
            GameManager.EventService.Add<ProcessingJobStartedEvent>(OnJobStarted);
            GameManager.EventService.Add<ProcessingJobCompletedEvent>(OnJobCompleted);
            GameManager.EventService.Add<ProcessingJobCancelledEvent>(OnJobCancelled);
            GameManager.EventService.Add<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Add<DepotChangedEvent>(RefreshExchangeTabGate);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<ProcessingStartRequestedEvent>(OnStartRequested);
            GameManager.EventService.Remove<ProcessingCancelRequestedEvent>(OnCancelRequested);
            GameManager.EventService.Remove<ProcessingJobStartedEvent>(OnJobStarted);
            GameManager.EventService.Remove<ProcessingJobCompletedEvent>(OnJobCompleted);
            GameManager.EventService.Remove<ProcessingJobCancelledEvent>(OnJobCancelled);
            GameManager.EventService.Remove<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Remove<DepotChangedEvent>(RefreshExchangeTabGate);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            if (evt.InteractableType == InteractableType.Building_Processing) Open();
            else Close();
        }

        private void Open()
        {
            if (rendererRoot == null || rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            RefreshExchangeTabGate();
            tabGroup.SelectTab(QueueTab);
            BuildSlots();
            ProcessingManager.Instance.ClearUncollectedCompletions();
        }

        private void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
            if (recipeListModal != null) recipeListModal.Close();
        }

        // The Exchange tab only appears once there's a good to trade - DepotChangedEvent covers
        // the first job finishing while the panel is already open.
        private void RefreshExchangeTabGate()
        {
            exchangeTabButton.gameObject.SetActive(Depot.Instance.DiscoveredGoods.Count > 0);
        }

        // Rebuilt on every Open() rather than incrementally maintained, so a Queue Slots purchase
        // made while the panel was closed is picked up for free next time it's opened.
        private void BuildSlots()
        {
            foreach (var slot in spawnedSlots) Destroy(slot.gameObject);
            spawnedSlots.Clear();

            int slotCount = ProcessingManager.Instance.SlotCount;
            for (int i = 0; i < slotCount; i++)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.Bind(i, OnSelectRecipeClicked);
                slot.gameObject.name = $"Slot_{i}";
                slot.SetRecipe(ProcessingManager.Instance.GetChosenRecipe(i) ?? GetDefaultRecipe(i));
                spawnedSlots.Add(slot);
            }
        }

        private void OnSelectRecipeClicked(int slotIndex)
        {
            recipeListModal.Show(slotIndex, spawnedSlots[slotIndex].SelectedRecipe);
        }

        private ProcessingRecipeDefinition GetDefaultRecipe(int slotIndex)
        {
            var unlockedRecipes = GameManager.ProcessingRecipeDatabase.Recipes.FindAll(recipe => ProcessingManager.Instance.IsRecipeUnlocked(recipe));
            for (int i = slotIndex; i < unlockedRecipes.Count; i++)
            {
                var recipe = unlockedRecipes[i];
                if (ProcessingManager.Instance.IsRecipeUnlocked(recipe)) return recipe;
            }

            return unlockedRecipes.Count > 0 ? unlockedRecipes[0] : null;
        }
        // Picking a recipe just assigns it to the slot per processingImplementation.md - the
        // slot itself now owns quantity selection and starting the job (no separate detail modal).
        private void OnRecipeSelected(int slotIndex, ProcessingRecipeDefinition recipe)
        {
            if (slotIndex < 0 || slotIndex >= spawnedSlots.Count) return;
            spawnedSlots[slotIndex].SetRecipe(recipe);
            ProcessingManager.Instance.SetChosenRecipe(slotIndex, recipe);
        }

        private void OnStartRequested(ProcessingStartRequestedEvent evt) => ProcessingManager.Instance.StartJob(evt.SlotIndex, evt.Recipe, evt.Quantity);
        private void OnCancelRequested(ProcessingCancelRequestedEvent evt) => ProcessingManager.Instance.CancelJob(evt.SlotIndex);
        private void OnSellGoodsRequested(SellGoodsRequestedEvent evt) => Depot.Instance.SellGood(evt.Id, evt.Amount);

        private void OnJobStarted(ProcessingJobStartedEvent evt) => RefreshSlot(evt.SlotIndex);
        private void OnJobCompleted(ProcessingJobCompletedEvent evt) => RefreshSlot(evt.SlotIndex);
        private void OnJobCancelled(ProcessingJobCancelledEvent evt) => RefreshSlot(evt.SlotIndex);

        private void RefreshSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < spawnedSlots.Count) spawnedSlots[slotIndex].Refresh();
        }
    }
}
