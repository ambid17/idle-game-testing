using System.Collections;
using Effects;
using Events;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Death screen: reason is captured on PlayerDiedEvent (fuel or HP reaching zero, see
    // PlayerHealth.Kill; also a hazard hit, fall damage, or the pause menu's manual respawn
    // button - see PlayerHealth.DeathReason for the full list), but the screen itself only opens
    // once PlayerDeathMenuRequestedEvent follows - Player.PlayerDeathEffect dispatches that after
    // its death beat finishes, so the menu doesn't cut the animation off. It fades in, with the
    // title settling into place, rather than popping on. Hidden again once
    // the player respawns. The respawn button only dispatches PlayerRevivedEvent - PlayerHealth,
    // PlayerController, and PlayerInventory each reset themselves independently in response.
    public class DeathUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TextMeshProUGUI reasonLabel;
        [SerializeField] private Button respawnButton;

        [Header("Fade-in")]
        [Tooltip("On the renderer root - fades the whole screen in.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("The \"You died\" title, which drops in from a larger size.")]
        [SerializeField] private RectTransform titleLabel;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.5f;
        [SerializeField, Min(1f)] private float titleStartScale = 1.4f;

        private Coroutine fadeRoutine;

        private void Start()
        {
            if (reasonLabel == null) Debug.LogError($"{nameof(DeathUI)} on {name} is missing its reasonLabel reference.");
            if (canvasGroup == null) Debug.LogError($"{nameof(DeathUI)} on {name} is missing its canvasGroup reference.");
            if (titleLabel == null) Debug.LogError($"{nameof(DeathUI)} on {name} is missing its titleLabel reference.");
            if (respawnButton != null) respawnButton.onClick.AddListener(Respawn);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Add<PlayerDeathMenuRequestedEvent>(ShowMenu);
            GameManager.EventService.Add<PlayerRevivedEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Remove<PlayerDeathMenuRequestedEvent>(ShowMenu);
            GameManager.EventService.Remove<PlayerRevivedEvent>(Close);
        }

        private void HandleDied(PlayerDiedEvent evt)
        {
            if (reasonLabel != null) reasonLabel.text = MessageFor(evt.Reason);
        }

        private void ShowMenu()
        {
            if (rendererRoot != null) rendererRoot.SetActive(true);
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeIn());
        }

        // Runs on this (always active) object rather than the renderer it animates.
        private IEnumerator FadeIn()
        {
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / fadeSeconds;
                canvasGroup.alpha = Easing.OutCubic(k);
                titleLabel.localScale = Vector3.one * Mathf.LerpUnclamped(titleStartScale, 1f, Easing.OutBack(k));
                yield return null;
            }
            canvasGroup.alpha = 1f;
            titleLabel.localScale = Vector3.one;
            fadeRoutine = null;
        }

        private static string MessageFor(DeathReason reason) => reason switch
        {
            DeathReason.OutOfFuel => "You ran out of fuel.",
            DeathReason.FallDamage => "You fell from too great a height.",
            DeathReason.Explosive => "Caught in an explosive blast.",
            DeathReason.FallingRock => "Crushed by falling rock.",
            DeathReason.GasPocket => "Overcome by a gas pocket.",
            DeathReason.Lava => "Burned by lava.",
            DeathReason.DartTrap => "Skewered by a dart trap.",
            DeathReason.Crusher => "Flattened by a crusher.",
            DeathReason.ManualRespawn => "Manual respawn.",
            _ => "Unknown cause."
        };

        private void Close()
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void Respawn()
        {
            GameManager.EventService.Dispatch<PlayerRevivedEvent>();
        }
    }
}
