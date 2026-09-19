using Events;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Death screen: shown on PlayerDiedEvent (fuel or HP reaching zero, see PlayerHealth.Kill;
    // also a hazard hit, fall damage, or the pause menu's manual respawn button - see
    // PlayerHealth.DeathReason for the full list), hidden again once the player respawns. The
    // respawn button only dispatches PlayerRevivedEvent - PlayerHealth, PlayerController, and
    // PlayerInventory each reset themselves independently in response.
    public class DeathUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TextMeshProUGUI reasonLabel;
        [SerializeField] private Button respawnButton;

        private void Start()
        {
            if (reasonLabel == null) Debug.LogError($"{nameof(DeathUI)} on {name} is missing its reasonLabel reference.");
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

        private void Open(PlayerDiedEvent evt)
        {
            if (reasonLabel != null) reasonLabel.text = MessageFor(evt.Reason);
            if (rendererRoot != null) rendererRoot.SetActive(true);
        }

        private static string MessageFor(DeathReason reason) => reason switch
        {
            DeathReason.OutOfFuel => "You ran out of fuel.",
            DeathReason.FallDamage => "You fell from too great a height.",
            DeathReason.Explosive => "Caught in an explosive blast.",
            DeathReason.FallingRock => "Crushed by falling rock.",
            DeathReason.GasPocket => "Overcome by a gas pocket.",
            DeathReason.Lava => "Burned by lava.",
            DeathReason.ManualRespawn => "Manual respawn.",
            _ => "Unknown cause."
        };

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
