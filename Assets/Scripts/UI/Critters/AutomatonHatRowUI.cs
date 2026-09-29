using System.Collections.Generic;
using Critters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One automaton's hat selector in AutomatonHatPickerUI: < [hat] > cycling through "no hat"
    // followed by every unlocked hat.
    public class AutomatonHatRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text automatonLabel;
        [SerializeField] private Image hatIcon;
        [SerializeField] private TMP_Text hatNameLabel;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        private int automatonIndex;
        private readonly List<HatId> options = new();

        private void Awake()
        {
            if (automatonLabel == null) Debug.LogError("AutomatonHatRowUI.automatonLabel is not assigned.");
            if (hatIcon == null) Debug.LogError("AutomatonHatRowUI.hatIcon is not assigned.");
            if (hatNameLabel == null) Debug.LogError("AutomatonHatRowUI.hatNameLabel is not assigned.");
            if (previousButton == null) Debug.LogError("AutomatonHatRowUI.previousButton is not assigned.");
            if (nextButton == null) Debug.LogError("AutomatonHatRowUI.nextButton is not assigned.");

            previousButton.onClick.AddListener(() => Cycle(-1));
            nextButton.onClick.AddListener(() => Cycle(1));
        }

        public void Bind(int index, IReadOnlyList<HatDefinition> unlockedHats)
        {
            automatonIndex = index;
            options.Clear();
            options.Add(HatId.None);
            foreach (var hat in unlockedHats) options.Add(hat.Id);

            automatonLabel.text = $"Automaton #{index}";
            Refresh();
        }

        public void Refresh()
        {
            var hat = GameManager.CritterDatabase.GetHat(CritterCollection.Instance.GetAutomatonHat(automatonIndex));
            hatIcon.enabled = hat != null;
            hatIcon.sprite = hat != null ? hat.Sprite : null;
            hatIcon.preserveAspect = true;
            hatNameLabel.text = hat != null ? hat.DisplayName : "No hat";
        }

        private void Cycle(int direction)
        {
            int current = Mathf.Max(0, options.IndexOf(CritterCollection.Instance.GetAutomatonHat(automatonIndex)));
            int next = (current + direction + options.Count) % options.Count;
            CritterCollection.Instance.SetAutomatonHat(automatonIndex, options[next]);
        }
    }
}
