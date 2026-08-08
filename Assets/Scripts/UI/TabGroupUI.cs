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
            }
        }
    }
}
