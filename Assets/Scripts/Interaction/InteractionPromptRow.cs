using Interaction;
using Settings;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class InteractionPromptRow : MonoBehaviour
{
    [SerializeField] private Image ButtonImage;
    [SerializeField] private TMP_Text RowText;
    public InteractableType InteractableType;
    // Which keybind this row prompts for - ButtonImage is swapped to that key's icon each time
    // the prompt shows, so it follows rebinds made in Options > Controls.
    [SerializeField] private GameAction action = GameAction.InteractPrimary;

    private void Start()
    {
        if (ButtonImage == null) Debug.LogError("InteractionPromptRow.ButtonImage is not assigned.");
        if (RowText == null) Debug.LogError("InteractionPromptRow.RowText is not assigned.");
    }

    public void RefreshKeyIcon()
    {
        ButtonImage.sprite = GameManager.KeyIconDatabase.GetIcon(GameManager.KeybindService.GetKey(action));
    }
}
