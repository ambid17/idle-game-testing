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
            allRows = GetComponentsInChildren<InteractionPromptRow>(true).ToList();
        }

        public void Show(InteractableType interactableType)
        {
            promptRoot.SetActive(true);
            foreach (var row in allRows)
            {
                bool matches = row.InteractableType == interactableType;
                row.gameObject.SetActive(matches);
                if (matches) row.RefreshKeyIcon();
            }
        }

        public void Hide() => promptRoot.SetActive(false);
    }
}
