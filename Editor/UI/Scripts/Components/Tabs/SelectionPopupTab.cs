using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class SelectionPopupTab: Tab
    {
        public abstract string DefaultLabel { get; }
        internal bool IsSelected { get; set; }
        internal List<Object> TabSearchResults { get; set; }
        private readonly Label k_NumberOfResultsLabel;

        protected SelectionPopupTab(string label) : base(label)
        {
            k_NumberOfResultsLabel = new Label();
            k_NumberOfResultsLabel.AddToClassList("mui-tab-results-label");
            tabHeader.Add(k_NumberOfResultsLabel);
            TabSearchResults = new List<Object>();
        }

        internal void SetNumberOfResults(int results, int consoleResults = 0)
        {
            k_NumberOfResultsLabel.text = results > 0 ? (results + consoleResults).ToString(): string.Empty;
        }

        public void ClearResults()
        {
            TabSearchResults.Clear();
        }

        internal void AddToResults(Object result)
        {
            TabSearchResults.Add(result);
        }
    }
}
