using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Generic button <-> content-root tab switcher, used by ControlCenterUI's four dashboards.
    // Chosen over MarketUI's filter-by-UpgradeBranch trick because those tabs have structurally
    // different content (graph, targeting control, slider), not a homogeneous list of one prefab.
    public class TabGroupUI : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Button Button;
            public GameObject ContentRoot;
        }

        [SerializeField] private List<Tab> tabs = new();
        [SerializeField] private int defaultTabIndex;

        // Opt-in (defaults false so existing consumers like ControlCenterUI are unaffected) -
        // UI.DevPanelUI is the first consumer to set this, per CLAUDE.md's tab-coloring rule.
        [SerializeField] private bool colorTabs = false;
        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = new(0.7f, 0.7f, 0.7f);

        private int currentIndex;

        private void Start()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i; // capture for the closure
                if (tabs[i].Button != null) tabs[i].Button.onClick.AddListener(() => SelectTab(index));
            }

            SelectTab(defaultTabIndex);
        }

        // Controller LB/RB cycle tabs, but only in the frontmost panel (see GamepadFocus), so a
        // panel underneath an open modal doesn't switch tabs behind it.
        private void Update()
        {
            if (tabs.Count < 2 || !GamepadFocus.IsInTopmost(transform)) return;

            var keybinds = GameManager.KeybindService;
            if (keybinds.WasTabNextPressedThisFrame()) CycleTab(1);
            else if (keybinds.WasTabPreviousPressedThisFrame()) CycleTab(-1);
        }

        // Skips tabs whose button is hidden or non-interactable (e.g. locked dashboards).
        private void CycleTab(int direction)
        {
            for (int step = 1; step < tabs.Count; step++)
            {
                int index = ((currentIndex + direction * step) % tabs.Count + tabs.Count) % tabs.Count;
                var button = tabs[index].Button;
                if (button != null && (!button.isActiveAndEnabled || !button.interactable)) continue;

                SelectTab(index);
                return;
            }
        }

        public void SelectTab(int index)
        {
            currentIndex = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].ContentRoot != null) tabs[i].ContentRoot.SetActive(i == index);

                if (colorTabs && tabs[i].Button != null && tabs[i].Button.targetGraphic != null)
                {
                    tabs[i].Button.targetGraphic.color = i == index ? activeTabColor : inactiveTabColor;
                }
            }
        }
    }
}
