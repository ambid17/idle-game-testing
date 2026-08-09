using Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Death screen: shown on PlayerDiedEvent (fuel or HP reaching zero, see PlayerHealth.Kill),
    // hidden again once the player respawns. The respawn button only dispatches
    // PlayerRevivedEvent - PlayerHealth, PlayerController, and PlayerInventory each reset
    // themselves independently in response.
    public class DeathUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button respawnButton;

        private void Start()
        {
            if (respawnButton != null) respawnButton.onClick.AddListener(Respawn);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerDiedEvent>(Open);
            GameManager.EventService.Add<PlayerRevivedEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDiedEvent>(Open);
            GameManager.EventService.Remove<PlayerRevivedEvent>(Close);
        }

        private void Open()
        {
            if (rendererRoot != null) rendererRoot.SetActive(true);
        }

        private void Close()
        {
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void Respawn()
        {
            GameManager.EventService.Dispatch<PlayerRevivedEvent>();
        }
    }
}
