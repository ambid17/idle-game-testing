using Interaction;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class InteractionPromptRow : MonoBehaviour
{
    [SerializeField] private Image ButtonImage;
    [SerializeField] private TMP_Text RowText;
    public InteractableType InteractableType;

    private void Start()
    {
        if (ButtonImage == null) Debug.LogError("InteractionPromptRow.ButtonImage is not assigned.");
        if (RowText == null) Debug.LogError("InteractionPromptRow.RowText is not assigned.");
    }
}
