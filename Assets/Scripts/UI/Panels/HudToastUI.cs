using System.Collections;
using Events;
using TMPro;
using UnityEngine;

namespace UI
{
    // Generic HUD toast driven by HudNotificationEvent - originally the inventory-full-only popup,
    // now reused for any brief, non-blocking message (e.g. PlayerMining's full-inventory notice,
    // PlayerController's low-fuel warning). Auto-dismisses after a few seconds rather than
    // requiring a click, since these are frequent nuisance messages the player may hit repeatedly,
    // not modals that should halt play.
    public class HudToastUI : MonoBehaviour
    {
        private const float DisplaySeconds = 2f;

        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TMP_Text messageLabel;

        private Coroutine hideRoutine;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("HudToastUI.rendererRoot is not assigned.");
            if (messageLabel == null) Debug.LogError("HudToastUI.messageLabel is not assigned.");
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<HudNotificationEvent>(Open);
        private void OnDisable() => GameManager.EventService.Remove<HudNotificationEvent>(Open);

        private void Open(HudNotificationEvent evt)
        {
            if (rendererRoot == null) return;

            if (messageLabel != null) messageLabel.text = evt.Message;
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
