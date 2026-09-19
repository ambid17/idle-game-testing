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

        private void Start()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i; // capture for the closure
                if (tabs[i].Button != null) tabs[i].Button.onClick.AddListener(() => SelectTab(index));
            }

            SelectTab(defaultTabIndex);
        }

        public void SelectTab(int index)
        {
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
