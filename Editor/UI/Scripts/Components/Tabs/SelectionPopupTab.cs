using System.Collections.Generic;
using Unity.AI.Assistant.Bridge.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class SelectionPopupTab : Tab
    {
        public virtual bool SearchEnabled => true;
        public virtual string SearchTooltip => string.Empty;

        public abstract string Instruction1Message { get; }

        public abstract string Instruction2Message { get; }

        internal bool IsSelected { get; set; }
        internal List<Object> TabSearchResults { get; } = new();
        readonly Label k_NumberOfResultsLabel;

        int m_NumberOfResults;

        public int NumberOfResults
        {
            get => m_NumberOfResults;
            protected set
            {
                m_NumberOfResults = value;
                k_NumberOfResultsLabel.text = m_NumberOfResults > 0 ? m_NumberOfResults.ToString() : string.Empty;
            }
        }

        public virtual bool DisplayConsoleLogs => false;

        protected SelectionPopupTab(string label) : base(label)
        {
            k_NumberOfResultsLabel = new Label();
            k_NumberOfResultsLabel.AddToClassList("mui-tab-results-label");
            tabHeader.Add(k_NumberOfResultsLabel);
        }

        internal bool IsSupportedAsset(Object obj)
        {
            if (obj == null || obj is DefaultAsset)
                return false;

            var objType = obj.GetType();

            return AssetDatabase.Contains(obj) ||
                   typeof(Component).IsAssignableFrom(objType) ||
                   typeof(GameObject).IsAssignableFrom(objType) ||
                   typeof(Transform).IsAssignableFrom(objType);
        }

        /// <summary>
        /// Refresh results that are not in TabSearchResults.
        /// This can be console logs or anything else that this tab supports.
        /// </summary>
        /// <param name="gatheredConsoleLogList">Up-to-date list of console log entries</param>
        public virtual void RefreshExtraResults(List<LogData> gatheredConsoleLogList)
        {
            var resultCount = TabSearchResults.Count;
            NumberOfResults = resultCount;
        }

        public void ClearResults()
        {
            TabSearchResults.Clear();
            RefreshExtraResults(null);
        }

        internal void AddToResults(Object result)
        {
            TabSearchResults.Add(result);
        }
    }
}
