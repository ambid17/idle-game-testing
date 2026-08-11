using System.Collections.Generic;
using Events;
using Interaction;
using Processing;
using UI.Processing;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Processing Center panel per Assets/Docs/processingImplementation.md: one queue row per
    // ProcessingManager.SlotCount, each opening a recipe-picker modal then a quantity/start modal.
    // Per CLAUDE.md's UI panel rule, this controller stays enabled on the Panel GameObject and
    // only toggles the child rendererRoot - same shape as MuseumUI. Never mutates ProcessingManager
    // state directly from a UI callback; Start/Cancel go through request events so the modals stay
    // decoupled from this panel (same shape as MarketUI.OnPurchaseRequested).
    public class ProcessingUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button closeButton;

        [Header("Queue")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private ProcessingQueueSlotUI slotPrefab;

        [Header("Modals")]
        [SerializeField] private ProcessingRecipeListModalUI recipeListModal;

        private readonly List<ProcessingQueueSlotUI> spawnedSlots = new();

        private void Start()
        {
            closeButton.onClick.AddListener(Close);
            rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<ProcessingStartRequestedEvent>(OnStartRequested);
            GameManager.EventService.Add<ProcessingCancelRequestedEvent>(OnCancelRequested);
            GameManager.EventService.Add<ProcessingJobStartedEvent>(OnJobStarted);
            GameManager.EventService.Add<ProcessingJobCompletedEvent>(OnJobCompleted);
            GameManager.EventService.Add<ProcessingJobCancelledEvent>(OnJobCancelled);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<ProcessingStartRequestedEvent>(OnStartRequested);
            GameManager.EventService.Remove<ProcessingCancelRequestedEvent>(OnCancelRequested);
            GameManager.EventService.Remove<ProcessingJobStartedEvent>(OnJobStarted);
            GameManager.EventService.Remove<ProcessingJobCompletedEvent>(OnJobCompleted);
            GameManager.EventService.Remove<ProcessingJobCancelledEvent>(OnJobCancelled);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(BuildingInteractedEvent evt)
        {
            if (evt.Type == InteractableType.Processing) Open();
            else Close();
        }

        private void Open()
        {
            rendererRoot.SetActive(true);
            BuildSlots();
        }

        private void Close()
        {
            rendererRoot.SetActive(false);
            if (recipeListModal != null) recipeListModal.Close();
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
                spawnedSlots.Add(slot);
            }
        }

        private void OnSelectRecipeClicked(int slotIndex)
        {
            recipeListModal.Show(slotIndex);
        }

        private void OnStartRequested(ProcessingStartRequestedEvent evt) => ProcessingManager.Instance.StartJob(evt.SlotIndex, evt.Recipe, evt.Quantity);
        private void OnCancelRequested(ProcessingCancelRequestedEvent evt) => ProcessingManager.Instance.CancelJob(evt.SlotIndex);

        private void OnJobStarted(ProcessingJobStartedEvent evt) => RefreshSlot(evt.SlotIndex);
        private void OnJobCompleted(ProcessingJobCompletedEvent evt) => RefreshSlot(evt.SlotIndex);
        private void OnJobCancelled(ProcessingJobCancelledEvent evt) => RefreshSlot(evt.SlotIndex);

        private void RefreshSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < spawnedSlots.Count) spawnedSlots[slotIndex].Refresh();
        }
    }
}
