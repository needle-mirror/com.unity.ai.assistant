using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class SelectionPopup : ManagedTemplate
    {
        internal class ListEntry
        {
            public Object Object;
            public SelectionPopup Owner;
            public LogData? LogData;
            public bool IsSelected;
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
        bool m_RefreshPending;
        bool m_SearchActive;

        string m_ActiveSearchFilter = string.Empty;
        string m_TabFilter = string.Empty;

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
                m_SearchActive = false;
                ScheduleSearchRefresh();
                ShowResultsWindow(false);
                return;
            }

            m_SearchActive = true;

            ScheduleSearchRefresh();
        }


        void Refresh()
        {
            if (m_RefreshPending)
            {
                return;
            }

            m_RefreshPending = true;
        }

        void ScheduleSearchRefresh()
        {
            Refresh();
            EditorApplication.delayCall += OnRefreshSearch;
        }

        void ScheduleSelectionRefresh()
        {
            Refresh();
            EditorApplication.delayCall += OnRefreshSelection;
        }

        void OnRefreshSearch()
        {
            m_RefreshPending = false;
            PopulateSearchListView();
        }

        void OnRefreshSelection()
        {
            m_RefreshPending = false;
            PopulateSelectionListView();
        }

        void OnTabResults(SearchContext context, IEnumerable<SearchItem> items, SelectionPopupTab popupTab)
        {
            popupTab.ClearResults();
            foreach (var item in items)
            {
                var obj = item.ToObject();

                if (IsSupportedAsset(obj))
                {
                    popupTab.AddToResults(obj);
                }
            }

            var consoleItemsCount = ConsoleUtils.GetConsoleLogs(m_LastUpdatedLogReferences);

            popupTab.SetNumberOfResults(popupTab.TabSearchResults.Count, consoleItemsCount);
        }

        void ShowNoResultsText(bool show)
        {
            if (show)
            {
                m_NoResultsContainer.style.display = DisplayStyle.Flex;
                m_SearchStringDisplay.text = m_ActiveSearchFilter;
            }
            else
            {
                m_NoResultsContainer.style.display = DisplayStyle.None;
            }
        }

        void ShowResultsWindow(bool show)
        {
            if (show)
            {
                m_AdaptiveListViewContainer.style.display = DisplayStyle.Flex;
                m_InitialSelection.style.display = DisplayStyle.None;
            }
            else
            {
                m_AdaptiveListViewContainer.style.display = DisplayStyle.None;
                m_InitialSelection.style.display = DisplayStyle.Flex;
            }
        }

        void Search()
        {
            foreach (var tab in k_AllTabs)
            {
                if (tab is SearchableTab searchableTab)
                {
                    SearchService.Request(
                        $"{searchableTab.Filter}{m_ActiveSearchFilter}",
                        (context, items) => OnTabResults(context, items, searchableTab)
                    );
                }
            }
        }

        void SetSelectedTab(SelectionPopupTab selectedTab)
        {
            foreach (var tab in k_AllTabs)
            {
                tab.IsSelected = selectedTab == tab;
            }
            m_SelectedTab = selectedTab;
        }

        void OnSearchTabSelected(SelectionPopupTab tab, string filter)
        {
            m_SearchField.SetEnabled(true);
            m_SearchField.tooltip = string.Empty;

            m_Instruction1Message.text = tab.Instruction1Message;
            m_Instruction2Message.text = tab.Instruction2Message;

            SetSelectedTab(tab);
            m_TabFilter = filter;
            ScheduleSearchRefresh();
        }

        void OnSelectionTabSelected(EditorSelectionTab tab)
        {
            m_SearchField.SetEnabled(false);
            m_SearchField.tooltip = "Searching and filtering are not available for Console and Selection.";

            m_Instruction1Message.text = tab.Instruction1Message;
            m_Instruction2Message.text = tab.Instruction2Message;

            SetSelectedTab(tab);
            if (tab.IsSelected)
            {
                ScheduleSelectionRefresh();
                return;
            }
            ScheduleSearchRefresh();
        }

        void InitializeTabs(TabView tabView)
        {
            var searchableTabs = new List<SearchableTab>();
            var typeList = TypeCache.GetTypesDerivedFrom<SearchableTab>();
            foreach (var type in typeList)
            {
                if (Activator.CreateInstance(type) is SearchableTab tab)
                {
                    tab.selected += _ => OnSearchTabSelected(tab, tab.Filter);
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
                    tab.selected += _ => OnSelectionTabSelected(tab);
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

            SetSelectedTab(k_AllTabs[0]);
            RefreshSelectionCount();
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
            InitializeTabs(m_SelectionTabView);
            ShowNoResultsText(false);

            var searchFieldRoot = view.Q<VisualElement>("attachItemSearchFieldRoot");
            m_SearchField = new ToolbarSearchField();
            m_SearchField.AddToClassList("mui-selection-search-bar");
            m_SearchField.RegisterCallback<KeyUpEvent>(_ => CheckAndRefilterSearchResults(true));
            m_SearchField.RegisterCallback<InputEvent>(_ => CheckAndRefilterSearchResults(true));
            searchFieldRoot.Add(m_SearchField);

            m_ListView = new()
            {
                EnableDelayedElements = true,
                EnableVirtualization = false,
                EnableScrollLock = true,
                EnableHorizontalScroll = false
            };
            m_ListView.Initialize(Context);
            m_AdaptiveListViewContainer.Add(m_ListView);

            ScheduleSelectionRefresh();

            m_LastConsoleCheckTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += DetectLogChanges;
        }

        public override void Show(bool sendVisibilityChanged = true)
        {
            base.Show(sendVisibilityChanged);

            RefreshSelectionCount();

            m_SearchField.value = string.Empty;

            m_SelectionTabView.Clear();

            InitializeTabs(m_SelectionTabView);

            CheckAndRefilterSearchResults(true);
        }

        void AddObjectToListView(Object obj, bool addSelection = false)
        {
            if (IsSupportedAsset(obj))
            {
                m_ListView?.AddData(
                    new ListEntry
                    {
                        Object = obj,
                        Owner = this,
                        IsSelected = ObjectSelection.Contains(obj) || addSelection
                    }
                );
            }
        }

        void AddObjectsToListView(IList<Object> objs, bool addSelection = false)
        {
            m_ListView.BeginUpdate();
            for (int i = 0; i < objs.Count; i++)
            {
                var obj = objs[i];

                AddObjectToListView(obj, addSelection);

                if (addSelection && !ObjectSelection.Contains(obj))
                    AddObjectToSelection(obj, true);
            }

            m_ListView.EndUpdate();
        }

        public void PopulateSearchListView()
        {
            m_ListView.ClearData();

            if (m_SelectedTab == null)
            {
                return;
            }

            var results = m_SelectedTab.TabSearchResults;

            if (m_SearchActive || m_SelectedTab.TabSearchResults.Count > 0)
            {
                AddLogsToListView();
                AddObjectsToListView(m_SelectedTab.TabSearchResults);
            }

            ShowResultsWindow(results.Count != 0);
            ShowNoResultsText(results.Count == 0 && !string.IsNullOrEmpty(m_ActiveSearchFilter));
        }

        void AddLogsToListView()
        {
            ConsoleUtils.GetConsoleLogs(m_LastUpdatedLogReferences);

            m_ListView.BeginUpdate();
            // Add console log entries
            foreach (var logRef in m_LastUpdatedLogReferences)
            {
                var entry = new ListEntry()
                {
                    Object = null,
                    Owner = this,
                    LogData = logRef,
                    IsSelected = ConsoleUtils.FindLogEntry(ConsoleSelection, logRef)
                };
                m_ListView.AddData(entry);
            }
            m_ListView.EndUpdate();
        }

        void PopulateSelectionListView()
        {
            m_ListView.ClearData();

            if (m_SelectedTab is EditorSelectionTab tab)
            {
                switch (tab.Type)
                {
                    case EditorSelectionTab.SelectionType.Console:
                    {
                        AddLogsToListView();
                        break;
                    }
                    case EditorSelectionTab.SelectionType.UnityObject:
                    {
                        // Add selected objects
                        ValidateObjectSelection();
                        if (Selection.objects.Length > 0)
                        {
                            AddObjectsToListView(Selection.objects.ToList());
                        }
                        break;
                    }

                }
            }

            RefreshSelectionCount();
        }

        bool IsSupportedAsset(Object obj)
        {
            if (obj == null || obj is DefaultAsset)
                return false;

            var objType = obj.GetType();

            return AssetDatabase.Contains(obj) ||
                   typeof(Component).IsAssignableFrom(objType) ||
                   typeof(GameObject).IsAssignableFrom(objType) ||
                   typeof(Transform).IsAssignableFrom(objType);
        }

        void RefreshSelectionCount()
        {
            foreach (var tab in m_EditorSelectionTabs)
            {
                int resultCount = 0;
                switch (tab.Type)
                {
                    case EditorSelectionTab.SelectionType.Console:
                    {
                        var logs = new List<LogData>();
                        resultCount = ConsoleUtils.GetConsoleLogs(logs);
                        break;
                    }
                    case EditorSelectionTab.SelectionType.UnityObject:
                    {
                        resultCount = Selection.objects.Count(IsSupportedAsset);
                        break;
                    }
                }

                tab.SetNumberOfResults(resultCount);

                if (tab.IsSelected)
                    ShowResultsWindow(resultCount != 0);
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
            if (!ConsoleUtils.HasEqualLogEntry(ConsoleSelection, logRef))
            {
                AddLogReferenceToSelection(logRef);
                e.SetSelected(true);
            }
            else
            {
                ConsoleSelection.RemoveAll(e => e.Equals(logRef));
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
            if (EditorApplication.timeSinceStartup < m_LastConsoleCheckTime + k_ConsoleCheckInterval)
                return;

            List<LogData> logs = new();
            ConsoleUtils.GetConsoleLogs(logs);

            if (m_LastUpdatedLogReferences.Count != logs.Count
                || m_LastUpdatedLogReferences.Any(log => !ConsoleUtils.HasEqualLogEntry(logs, log))
                || logs.Any(log => !ConsoleUtils.HasEqualLogEntry(m_LastUpdatedLogReferences, log)) )
            {
                PopulateSelectionListView();
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
    }
}
