using System.Collections;
using Events;
using UnityEngine;

namespace UI
{
    // Shown briefly when PlayerMining blocks a dig because PlayerInventory.IsFull (see
    // PlayerMining.Update's blockedByFullInventory guard). Auto-dismisses after a few seconds
    // rather than requiring a click, since this is a frequent, non-blocking nuisance message the
    // player will hit repeatedly while full, not a modal that should halt play.
    public class InventoryFullUI : MonoBehaviour
    {
        private const float DisplaySeconds = 2f;

        [SerializeField] private GameObject rendererRoot;

        private Coroutine hideRoutine;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("InventoryFullUI.rendererRoot is not assigned.");
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<InventoryFullEvent>(Open);
        private void OnDisable() => GameManager.EventService.Remove<InventoryFullEvent>(Open);

        private void Open()
        {
            if (rendererRoot == null) return;

            rendererRoot.SetActive(true);
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(DisplaySeconds);
            if (rendererRoot != null) rendererRoot.SetActive(false);
            hideRoutine = null;
        }
    }
}
