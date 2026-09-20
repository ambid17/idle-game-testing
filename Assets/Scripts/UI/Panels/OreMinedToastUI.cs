using System.Collections;
using Events;
using MapGeneration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // HUD toast driven by OreMinedEvent - shows the icon+name of whatever Ore-category block the
    // player just personally mined (PlayerMining.CollectMinedBlock). Single-slot and restarts its
    // dismiss timer on every new event, same behavior as HudToastUI, rather than queuing - mining
    // fires this often enough that a strict per-mine queue would just pile up.
    public class OreMinedToastUI : MonoBehaviour
    {
        private const float DisplaySeconds = 1.5f;

        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;

        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;
        private Coroutine hideRoutine;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("OreMinedToastUI.rendererRoot is not assigned.");
            if (iconImage == null) Debug.LogError("OreMinedToastUI.iconImage is not assigned.");
            if (nameLabel == null) Debug.LogError("OreMinedToastUI.nameLabel is not assigned.");
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<OreMinedEvent>(Open);
        private void OnDisable() => GameManager.EventService.Remove<OreMinedEvent>(Open);

        private void Open(OreMinedEvent evt)
        {
            if (rendererRoot == null) return;

            var blockType = blockTypeDatabase.Get((byte)evt.Id);
            if (blockType == null)
            {
                Debug.LogError($"OreMinedToastUI: no BlockType registered for {evt.Id}.");
                return;
            }

            if (iconImage != null) iconImage.sprite = blockType.Icon;
            if (nameLabel != null) nameLabel.text = $"+{evt.Amount} {blockType.DisplayName}";

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
