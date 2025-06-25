using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class SelectionPopup : ManagedTemplate
    {
        const int k_MaxSearchResults = 50;

        internal class ListEntry
        {
            public Object Object;
            public SelectionPopup Owner;
            public LogData? LogData;
            public bool IsSelected;
        }

        enum SearchState
        {
            NoSearchTerm,
            NoResults,
            Loading,
            HasResults
        }

        VisualElement m_Root;
        VisualElement m_AdaptiveListViewContainer;
        ToolbarSearchField m_SearchField;
        AdaptiveListView<ListEntry, SelectionElement> m_ListView;
        VisualElement m_InitialSelection;
        VisualElement m_NoResultsContainer;
        Label m_SearchStringDisplay;
        Label m_Instruction1Message;
        Label m_Instruction2Message;
        VisualElement m_LoadingIndicator;
        Image m_LoadingIcon;
        VisualElement m_PagingContainer;
        Label m_PagingLabel;

        TabView m_SelectionTabView;
        readonly List<SelectionPopupTab> k_AllTabs = new();
        List<EditorSelectionTab> m_EditorSelectionTabs;

        double m_LastConsoleCheckTime;
        readonly float k_ConsoleCheckInterval = 0.2f;
        readonly string k_PopupTabClass = "mui-selection-popup-tab";

        List<LogData> m_LastUpdatedLogReferences = new ();

        public readonly List<Object> ObjectSelection = new();
        public readonly List<Object> CombinedSelection = new();
        public readonly List<LogData> ConsoleSelection = new();

        public Action OnSelectionChanged;
        public Action<Object> OnContextObjectAdded;
        public Action<LogData> OnContextLogAdded;

        SelectionPopupTab m_SelectedTab;
        int m_SelectedTabIndex;

        string m_ActiveSearchFilter = string.Empty;

        #region Paging
        const string k_PagingLabelText = "Showing {0} - {1} results";
        int m_CurrentPage;
        #endregion

        public SelectionPopup()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public void SetSelectionFromContext(List<AssistantContextEntry> context, bool notify = true)
        {
            ObjectSelection.Clear();
            ConsoleSelection.Clear();

            for (var i = 0; i < context.Count; i++)
            {
                var entry = context[i];
                switch (entry.EntryType)
                {
                    case AssistantContextType.HierarchyObject:
                    case AssistantContextType.SubAsset:
                    case AssistantContextType.SceneObject:
                    {
                        var target = entry.GetTargetObject();
                        if (target != null)
                        {
                            ObjectSelection.Add(target);
                        }

                        break;
                    }

                    case AssistantContextType.ConsoleMessage:
                    {
                        var logEntry = new LogData
                        {
                            Message = entry.Value,
                            Type = Enum.Parse<LogDataType>(entry.ValueType)
                        };

                        ConsoleSelection.Add(logEntry);
                        break;
                    }
                }
            }

            if (notify)
            {
                OnSelectionChanged?.Invoke();
            }
        }

        void CheckAndRefilterSearchResults(bool force = false)
        {
            string newFilterValue = m_SearchField.value.Trim();
            if (newFilterValue == m_ActiveSearchFilter && !force)
            {
                return;
            }

            m_ActiveSearchFilter = newFilterValue;

            Search();

            if (m_EditorSelectionTabs.Contains(m_SelectedTab))
            {
                return;
            }

            if (string.IsNullOrEmpty(m_ActiveSearchFilter))
            {
                ScheduleSearchRefresh();
                RefreshSearchState();
                return;
            }


            ScheduleSearchRefresh();
        }

        void ScheduleSearchRefresh()
        {
            EditorApplication.delayCall += PopulateSearchListView;
        }

        void OnTabResults(IEnumerable<Object> items, SelectionPopupTab popupTab)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (popupTab.IsSupportedAsset(item))
                    {
                        popupTab.AddToResults(item);
                    }
                }
            }

            ConsoleUtils.GetConsoleLogs(m_LastUpdatedLogReferences);

            popupTab.RefreshExtraResults(m_LastUpdatedLogReferences);

            if (popupTab == m_SelectedTab)
            {
                ScheduleSearchRefresh();
            }
        }

        void RefreshSearchState()
        {
            if (m_SelectedTab == null)
            {
                SetSearchState(SearchState.NoSearchTerm);
                return;
            }
            var results = m_SelectedTab.NumberOfResults;

            if (results > 0)
                SetSearchState(SearchState.HasResults);
            else if (m_SelectedTab is SearchableTab { IsLoading: true })
                SetSearchState(SearchState.Loading);
            else if (!string.IsNullOrEmpty(m_ActiveSearchFilter))
                SetSearchState(SearchState.NoResults);
            else
                SetSearchState(SearchState.NoSearchTerm);
        }

        void SetSearchState(SearchState state)
        {
            m_NoResultsContainer.style.display = DisplayStyle.None;
            m_InitialSelection.style.display = DisplayStyle.None;
            m_AdaptiveListViewContainer.style.display = DisplayStyle.None;
            m_LoadingIndicator.style.display = DisplayStyle.None;
            m_PagingContainer.style.display = DisplayStyle.None;

            switch (state)
            {
                case SearchState.NoSearchTerm:
                    m_InitialSelection.style.display = DisplayStyle.Flex;
                    break;
                case SearchState.NoResults:
                    m_InitialSelection.style.display = DisplayStyle.Flex;

                    if (m_SelectedTab is SearchableTab)
                    {
                        m_NoResultsContainer.style.display = DisplayStyle.Flex;
                    }

                    m_SearchStringDisplay.text = m_ActiveSearchFilter;
                    break;
                case SearchState.HasResults:
                    m_AdaptiveListViewContainer.style.display = DisplayStyle.Flex;

                    var numberOfResults = m_SelectedTab.NumberOfResults;
                    if (numberOfResults > k_MaxSearchResults)
                    {
                        m_PagingContainer.style.display = DisplayStyle.Flex;
                        m_PagingLabel.text = string.Format(k_PagingLabelText, m_CurrentPage * k_MaxSearchResults + 1,
                            Math.Min(m_CurrentPage * k_MaxSearchResults + k_MaxSearchResults, numberOfResults));
                    }

                    break;
                case SearchState.Loading:
                    m_LoadingIndicator.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        void Search()
        {
            m_CurrentPage = 0;
            SearchableTab.SetupSearchProviders(m_ActiveSearchFilter);

            foreach (var tab in k_AllTabs)
            {
                if (tab is SearchableTab searchableTab)
                {
                    searchableTab.ClearResults();

                    foreach (var searchContext in searchableTab.SearchProviders)
                    {
                        searchContext.Callbacks.Add(items =>
                        {
                            if (IsShown)
                            {
                                OnTabResults(items, searchableTab);
                            }
                        });
                    }
                }
            }

            SearchableTab.StartSearchers();
        }

        void SetSelectedTab(SelectionPopupTab selectedTab)
        {
            foreach (var tab in k_AllTabs)
            {
                tab.IsSelected = selectedTab == tab;
            }

            m_SelectedTab = selectedTab;
            m_SelectedTabIndex = k_AllTabs.IndexOf(m_SelectedTab);
        }

        void OnTabSelected(SelectionPopupTab tab)
        {
            m_SearchField.SetEnabled(tab.SearchEnabled);
            m_SearchField.tooltip = tab.SearchTooltip;

            m_Instruction1Message.text = tab.Instruction1Message;
            m_Instruction2Message.text = tab.Instruction2Message;

            SetSelectedTab(tab);
            ScheduleSearchRefresh();
        }

        void InitializeTabs(TabView tabView)
        {
            k_AllTabs.Clear();

            var searchableTabs = new List<SearchableTab>();
            var typeList = TypeCache.GetTypesDerivedFrom<SearchableTab>();
            foreach (var type in typeList)
            {
                if (Activator.CreateInstance(type) is SearchableTab tab)
                {
                    tab.selected += _ => OnTabSelected(tab);
                    tab.tabHeader.AddToClassList(k_PopupTabClass);
                    searchableTabs.Add(tab);
                }
            }

            searchableTabs.Sort((a, b) => a.Order - b.Order);
            foreach (var tab in searchableTabs)
            {
                tabView.Add(tab);
                k_AllTabs.Add(tab);
            }

            m_EditorSelectionTabs = new List<EditorSelectionTab>();
            var selectionTypeList = TypeCache.GetTypesDerivedFrom<EditorSelectionTab>();
            foreach (var type in selectionTypeList)
            {
                if (Activator.CreateInstance(type) is EditorSelectionTab tab)
                {
                    tab.selected += _ => OnTabSelected(tab);
                    tab.tabHeader.AddToClassList(k_PopupTabClass);
                    m_EditorSelectionTabs.Add(tab);

                    tab.tabHeader.AddToClassList("mui-selection-popup-selected-tab");
                    tab.tabHeader.AddToClassList(k_PopupTabClass);
                }
            }

            m_EditorSelectionTabs.Sort((a, b) => a.Order - b.Order);
            foreach (var tab in m_EditorSelectionTabs)
            {
                k_AllTabs.Add(tab);
                tabView.Add(tab);
            }

            tabView.selectedTabIndex = m_SelectedTabIndex;

            OnTabSelected(k_AllTabs[m_SelectedTabIndex]);
            PopulateSearchListView();
            RefreshSelectionCount();

            try
            {
                RefreshTabButtons();
            }
            catch
            {
                // ignore
            }
        }

        internal void RefreshTabButtons()
        {
            // TabView sometimes shows the next and previous buttons even though there's no need for them.
            // This is a workaround to force the buttons to update their state.
            var updateButtonsMethod = m_SelectionTabView.GetType()
                .GetMethod("UpdateButtons", BindingFlags.NonPublic | BindingFlags.Instance);
            updateButtonsMethod.Invoke(m_SelectionTabView, new[] { (object)Vector3.zero });
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("popupRoot");

            m_SelectionTabView = view.Q<TabView>("selectionTabView");

            m_AdaptiveListViewContainer = view.Q<VisualElement>("adaptiveListViewContainer");
            m_AdaptiveListViewContainer.style.display = DisplayStyle.None;
            m_NoResultsContainer = view.Q<VisualElement>("noResultsMessage");
            m_SearchStringDisplay = view.Q<Label>("noResultsSearchDisplay");
            m_Instruction1Message = view.Q<Label>("instruction1Message");
            m_Instruction2Message = view.Q<Label>("instruction2Message");
            m_InitialSelection = view.Q<VisualElement>("initialSelectionPopupMessage");
            m_LoadingIndicator = view.Q<VisualElement>("loadingIndicator");
            m_LoadingIcon = m_LoadingIndicator.Q<Image>("loadingIcon");
            m_PagingContainer = view.Q<VisualElement>("pagingContainer");
            m_PagingLabel = view.Q<Label>("pagingLabel");
            view.SetupButton("previousPageButton", PreviousPage);
            view.SetupButton("nextPageButton", NextPage);

            schedule.Execute(() =>
            {
                var newAngle = (m_LoadingIcon.style.rotate.value.angle.value + 10) % 360;
                m_LoadingIcon.style.rotate = new StyleRotate(new Rotate(newAngle));
            }).Every(33);

            var searchFieldRoot = view.Q<VisualElement>("attachItemSearchFieldRoot");
            m_SearchField = new ToolbarSearchField();
            m_SearchField.AddToClassList("mui-selection-search-bar");
            m_SearchField.RegisterValueChangedCallback(_ => CheckAndRefilterSearchResults());
            searchFieldRoot.Add(m_SearchField);

            m_ListView = new()
            {
                EnableDelayedElements = false,
                EnableVirtualization = false,
                EnableScrollLock = true,
                EnableHorizontalScroll = false
            };
            m_ListView.Initialize(Context);
            m_AdaptiveListViewContainer.Add(m_ListView);

            InitializeTabs(m_SelectionTabView);
            RefreshSearchState();

            ScheduleSearchRefresh();

            m_LastConsoleCheckTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += DetectLogChanges;
        }

        public override void Show(bool sendVisibilityChanged = true)
        {
            base.Show(sendVisibilityChanged);

            RefreshSelectionCount();

            m_SelectionTabView.Clear();

            InitializeTabs(m_SelectionTabView);

            CheckAndRefilterSearchResults(true);

            m_SearchField.Focus();
        }

        public void PopulateSearchListView()
        {
            RefreshSelectionCount();

            if (m_SelectedTab == null)
            {
                m_ListView.ClearData();
                return;
            }

            // When search results come in or tabs change, we need to rebuild the list.
            // This is expensive and when only later pages change, it's not needed.
            // Only rebuild the list if the actual data has changed:
            if (GetEntriesToShow(out var entriesToShow))
            {
                m_ListView.ClearData();

                m_ListView.BeginUpdate();

                foreach (var entry in entriesToShow)
                {
                    m_ListView.AddData(entry);
                }

                m_ListView.EndUpdate(false, false);
            }

            RefreshSearchState();
        }

        int GetNumberOfPages()
        {
            var tab = m_SelectedTab;

            var objectsToShow = tab.TabSearchResults;
            var totalEntryCount = objectsToShow.Count;

            if (tab.DisplayConsoleLogs)
            {
                ConsoleUtils.GetConsoleLogs(m_LastUpdatedLogReferences);
                totalEntryCount += m_LastUpdatedLogReferences.Count;
            }

            return totalEntryCount / k_MaxSearchResults;
        }

        bool GetEntriesToShow(out List<ListEntry> results)
        {
            results = new List<ListEntry>();

            var changed = false;

            var tab = m_SelectedTab;

            // Make sure we don't go past the last page when tabs are switched on later pages:
            m_CurrentPage = Math.Min(m_CurrentPage, GetNumberOfPages());

            var objectsToShow = tab.TabSearchResults;
            var totalEntryCount = objectsToShow.Count;

            int startIndex, endIndex, returnedEntryCount = 0, logEntryCount = 0;

            if (tab.DisplayConsoleLogs)
            {
                ConsoleUtils.GetConsoleLogs(m_LastUpdatedLogReferences);
                logEntryCount = m_LastUpdatedLogReferences.Count;
                totalEntryCount += logEntryCount;

                startIndex = m_CurrentPage * k_MaxSearchResults;
                endIndex = Math.Min(m_LastUpdatedLogReferences.Count,
                    Math.Min(
                        startIndex + k_MaxSearchResults,
                        totalEntryCount));

                for (var i = startIndex; i < endIndex; i++, returnedEntryCount++)
                {
                    var logRef = m_LastUpdatedLogReferences[i];

                    var entry = new ListEntry
                    {
                        Object = null,
                        Owner = this,
                        LogData = logRef,
                        IsSelected = ConsoleUtils.FindLogEntry(ConsoleSelection, logRef) >= 0
                    };

                    if (!changed && !DoEntriesMatch(i, entry))
                    {
                        changed = true;
                    }

                    results.Add(entry);
                }

                if (returnedEntryCount >= k_MaxSearchResults)
                {
                    return HasDataChanged(results);
                }
            }

            startIndex = Math.Max(0, m_CurrentPage * k_MaxSearchResults - logEntryCount);
            endIndex = Math.Min(startIndex + k_MaxSearchResults, objectsToShow.Count);

            for (var i = startIndex;
                 i < endIndex && returnedEntryCount < k_MaxSearchResults;
                 i++, returnedEntryCount++)
            {
                var obj = objectsToShow[i];

                var entry = new ListEntry { Object = obj, Owner = this, IsSelected = ObjectSelection.Contains(obj) };

                if (!changed && !DoEntriesMatch(i, entry))
                {
                    changed = true;
                }

                results.Add(entry);
            }

            return HasDataChanged(results);

            bool HasDataChanged(List<ListEntry> results)
            {
                return changed || m_ListView.Data.Count != results.Count;
            }

            bool DoEntriesMatch(int index, ListEntry entry)
            {
                var currentEntries = m_ListView.Data;

                if (currentEntries.Count <= index)
                    return false;

                var currentEntry = currentEntries[index];

                if (currentEntry.LogData.HasValue != entry.LogData.HasValue)
                    return false;

                if (currentEntry.LogData.HasValue && !currentEntry.LogData.Value.Equals(entry.LogData.Value))
                    return false;

                return currentEntry.Object == entry.Object
                       && currentEntry.IsSelected == entry.IsSelected;
            }
        }

        void RefreshSelectionCount()
        {
            var logs = new List<LogData>();
            ConsoleUtils.GetConsoleLogs(logs);

            ValidateObjectSelection();

            foreach (var tab in k_AllTabs)
            {
                tab.RefreshExtraResults(logs);

                if (tab.IsSelected)
                {
                    RefreshSearchState();
                }
            }
        }

        internal void PingObject(Object obj)
        {
            EditorGUIUtility.PingObject(obj);
        }

        internal void SelectedObject(Object obj, SelectionElement e)
        {
            if (!ObjectSelection.Contains(obj))
            {
                AddObjectToSelection(obj);
                e.SetSelected(true);
            }
            else
            {
                ObjectSelection.Remove(obj);
                e.SetSelected(false);
            }

            OnSelectionChanged?.Invoke();

            RefreshSelectionCount();
        }

        void AddObjectToSelection(Object obj, bool notifySelectionChanged = false)
        {
            ObjectSelection.Add(obj);
            OnContextObjectAdded?.Invoke(obj);

            if (notifySelectionChanged)
                OnSelectionChanged?.Invoke();
        }

        internal void SelectedLogReference(LogData logRef, SelectionElement e)
        {
            var existingEntryIndex = ConsoleUtils.FindLogEntry(ConsoleSelection, logRef);
            if (existingEntryIndex < 0)
            {
                AddLogReferenceToSelection(logRef);
                e.SetSelected(true);
            }
            else
            {
                ConsoleSelection.RemoveAt(existingEntryIndex);
                e.SetSelected(false);
            }

            OnSelectionChanged?.Invoke();

            RefreshSelectionCount();
        }

        void AddLogReferenceToSelection(LogData logRef, bool notifySelectionChanged = false)
        {
            ConsoleSelection.Add(logRef);
            OnContextLogAdded?.Invoke(logRef);

            if (notifySelectionChanged)
                OnSelectionChanged?.Invoke();
        }

        void DetectLogChanges()
        {
            if (!IsShown || EditorApplication.timeSinceStartup < m_LastConsoleCheckTime + k_ConsoleCheckInterval)
                return;

            List<LogData> logs = new();
            ConsoleUtils.GetConsoleLogs(logs);

            if (m_LastUpdatedLogReferences.Count != logs.Count
                || m_LastUpdatedLogReferences.Any(log => !ConsoleUtils.HasEqualLogEntry(logs, log))
                || logs.Any(log => !ConsoleUtils.HasEqualLogEntry(m_LastUpdatedLogReferences, log)))
            {
                PopulateSearchListView();
            }

            m_LastConsoleCheckTime = EditorApplication.timeSinceStartup;
        }

        void ValidateObjectSelection()
        {
            for (var i = ObjectSelection.Count - 1; i >= 0; i--)
            {
                if (ObjectSelection[i] == null)
                {
                    ObjectSelection.RemoveAt(i);
                }
            }
        }

        void PreviousPage(PointerUpEvent evt)
        {
            if (m_CurrentPage > 0)
            {
                m_CurrentPage -= 1;
                ScheduleSearchRefresh();
            }
        }

        void NextPage(PointerUpEvent evt)
        {
            if (m_CurrentPage < GetNumberOfPages())
            {
                m_CurrentPage += 1;
                ScheduleSearchRefresh();
            }
        }
    }
}
