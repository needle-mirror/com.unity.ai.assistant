using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class SelectionPopupTab: Tab
    {
        public abstract string DefaultLabel { get; }

        public abstract string Instruction1Message { get; }

        public abstract string Instruction2Message { get; }

        internal bool IsSelected { get; set; }
        internal HashSet<Object> TabSearchResults { get; } = new();
        private readonly Label k_NumberOfResultsLabel;

        public int NumberOfResults { get; private set; }


        protected SelectionPopupTab(string label) : base(label)
        {
            k_NumberOfResultsLabel = new Label();
            k_NumberOfResultsLabel.AddToClassList("mui-tab-results-label");
            tabHeader.Add(k_NumberOfResultsLabel);
        }

        internal void SetNumberOfResults(int results, int consoleResults = 0)
        {
            NumberOfResults = results;
            k_NumberOfResultsLabel.text = results > 0 ? (results + consoleResults).ToString(): string.Empty;
        }

        public void ClearResults()
        {
            TabSearchResults.Clear();
            SetNumberOfResults(0);
        }

        internal void AddToResults(Object result)
        {
            TabSearchResults.Add(result);
        }

        internal virtual void NewResultsReceived()
        {
            ClearResults();
        }
    }
}
