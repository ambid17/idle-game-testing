using Events;
using Player;
using TMPro;
using Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // World-space half of the tutorial popup system (Tutorial.TutorialManager). Shown for any
    // ShowTutorialEvent whose Entry.DisplayType is WorldPopup - currently just the first-Artifact
    // tutorial, anchored at the player's position when it was mined. Unlike
    // Interaction.InteractionPromptUI's World Space canvas (which stays parented to the player),
    // this GameObject lives at the scene root and has its transform.position moved to the requested
    // anchor each time it's shown, since the anchor is different per trigger rather than always
    // "above the player."
    // See UI.Panels.TutorialModalUI for the screen-space counterpart. Inherits ModalBase so
    // Escape can dismiss it without also closing a panel or opening the pause menu underneath.
    public class WorldTutorialPopupUI : ModalBase
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("WorldTutorialPopupUI.rendererRoot is not assigned.");
            if (titleLabel == null) Debug.LogError("WorldTutorialPopupUI.titleLabel is not assigned.");
            if (bodyLabel == null) Debug.LogError("WorldTutorialPopupUI.bodyLabel is not assigned.");
            if (closeButton == null) Debug.LogError("WorldTutorialPopupUI.closeButton is not assigned.");

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameManager.EventService.Add<ShowTutorialEvent>(OnShowTutorial);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.Entry.DisplayType != TutorialDisplayType.WorldPopup) return;
            if (rendererRoot == null || titleLabel == null || bodyLabel == null) return;

            transform.position = evt.WorldPosition.Value;
            titleLabel.text = evt.Entry.Title;
            bodyLabel.text = evt.Entry.Body;

            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            Opened();
        }

        public override void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;

            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
            Closed();
        }
    }
}
