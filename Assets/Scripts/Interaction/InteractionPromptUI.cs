using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Interaction
{

    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;
        private List<InteractionPromptRow> allRows;

        private void Start()
        {
            if(promptRoot == null) Debug.LogError("InteractionPromptUI.promptRoot is not assigned.");
            allRows = GetComponentsInChildren<InteractionPromptRow>().ToList();
        }

        public void Show(InteractableType interactableType)
        {
            promptRoot.SetActive(true);
            foreach (var row in allRows)
            {
                row.gameObject.SetActive(row.InteractableType == interactableType);
            }
        }

        public void Hide() => promptRoot.SetActive(false);
    }
}
