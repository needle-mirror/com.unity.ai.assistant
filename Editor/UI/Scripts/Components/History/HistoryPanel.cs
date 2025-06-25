using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.History
{
    class HistoryPanel : ManagedTemplate
    {
        static readonly List<ConversationModel> k_ConversationCache = new();
        static readonly IDictionary<string, List<ConversationModel>> k_GroupCache = new Dictionary<string, List<ConversationModel>>();

        readonly IList<object> k_TempList = new List<object>();

        ToolbarSearchField m_SearchBar;
        VisualElement m_ContentRoot;
        AdaptiveListView<object, HistoryPanelEntryWrapper> m_ContentList;

        AssistantConversationId m_SelectedConversation;

        string m_SearchFilter;

        public event Action EntrySelected;

        public HistoryPanel(): base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ContentRoot = view.Q<VisualElement>("historyContentRoot");
            m_ContentList = new AdaptiveListView<object, HistoryPanelEntryWrapper>
            {
                EnableVirtualization = true,
                EnableAutoScroll = false
            };
            m_ContentList.Initialize(Context);
            m_ContentList.SelectionChanged += SelectionChanged;
            m_ContentRoot.Add(m_ContentList);

            m_SearchBar = new ToolbarSearchField();
            m_SearchBar.AddToClassList("mui-history-panel-search-bar");
            view.Q<VisualElement>("historySearchBarRoot").Add(m_SearchBar);
            m_SearchBar.RegisterCallback<KeyUpEvent>(OnSearchTextChanged);
            m_SearchBar.RegisterValueChangedCallback(OnSearchValueChanged);

            Context.API.ConversationsRefreshed += OnReloadRequired;
            Context.API.ConversationDeleted += OnConversationDeleted;
        }

        void OnConversationDeleted(AssistantConversationId obj)
        {
            Reload(fullReload: true);
        }

        void LoadData(IList<object> result, long nowRaw, string searchFilter = null)
        {
            bool searchActive = !string.IsNullOrEmpty(searchFilter);
            k_GroupCache.Clear();
            result.Clear();
            foreach (var conversationInfo in k_ConversationCache)
            {
                if (searchActive && conversationInfo.Title.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string groupKey;
                if (Context.Blackboard.GetFavorite(conversationInfo.Id))
                {
                    groupKey = "000000#Favorites";
                }
                else
                {
                    groupKey = MessageUtils.GetMessageTimestampGroup(conversationInfo.LastMessageTimestamp, nowRaw);
                }

                if (!k_GroupCache.TryGetValue(groupKey, out var groupInfos))
                {
                    groupInfos = new List<ConversationModel>();
                    k_GroupCache.Add(groupKey, groupInfos);
                }

                groupInfos.Add(conversationInfo);
            }

            var orderedKeys = k_GroupCache.Keys.OrderBy(x => x).ToArray();
            for (var i = 0; i < orderedKeys.Length; i++)
            {
                var title = orderedKeys[i].Split('#')[1];
                result.Add(title);

                var groupContent = k_GroupCache[orderedKeys[i]];
                groupContent.Sort((e1, e2) => DateTimeOffset.Compare(DateTimeOffset.FromUnixTimeMilliseconds(e2.LastMessageTimestamp), DateTimeOffset.FromUnixTimeMilliseconds(e1.LastMessageTimestamp)));
                foreach (var info in groupContent)
                {
                    result.Add(info);
                }
            }
        }

        void Reload(bool fullReload = true, bool resetScrollPosition = false)
        {
            if (fullReload)
            {
                // Full reload let's get a fresh list of conversations from the driver
                k_ConversationCache.Clear();
                k_ConversationCache.AddRange(Context.Blackboard.Conversations);

                // Update the cache
                foreach (var conversationInfo in k_ConversationCache)
                {
                    Context.Blackboard.SetFavorite(conversationInfo.Id, conversationInfo.IsFavorite);
                }
            }

            var nowRaw = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var activeConversation = Context.Blackboard.ActiveConversation;

            m_ContentList.BeginUpdate();
            m_ContentList.ClearData();
            m_ContentList.ClearSelection();

            k_TempList.Clear();
            LoadData(k_TempList, nowRaw, m_SearchFilter);

            int selectedIndex = -1;
            for (var i = 0; i < k_TempList.Count; i++)
            {
                var entry = k_TempList[i];
                if (activeConversation != null && entry is ConversationModel info && info.Id == activeConversation.Id)
                {
                    selectedIndex = i;
                }

                m_ContentList.AddData(entry);
            }

            m_ContentList.EndUpdate(false);
            if (selectedIndex >= 0)
            {
                m_ContentList.SetSelectionWithoutNotify(selectedIndex, true);
            }

            m_SelectedConversation = activeConversation?.Id ?? default;

            m_ContentList.SetDisplay(m_ContentList.Data.Count != 0);

            if (resetScrollPosition)
            {
                m_ContentList.ScrollToStartIfNotLocked();
            }
        }

        void OnRefreshRequired()
        {
            Reload(fullReload: false);
        }

        void OnReloadRequired()
        {
            Reload(fullReload: true);
        }

        void OnSearchTextChanged(KeyUpEvent evt)
        {
            SetSearchFilter(m_SearchBar.value);
        }

        void SelectionChanged(int index, object data)
        {
            if (index == -1 || data is string)
            {
                return;
            }

            Context.API.CancelPrompt();

            var conversationInfo = (ConversationModel)data;
            m_SelectedConversation = conversationInfo.Id;

            Context.Blackboard.SetActiveConversation(m_SelectedConversation);
            Context.API.ConversationLoad(m_SelectedConversation);

            AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.LoadConversation, d =>
            {
                d.ConversationId = m_SelectedConversation.Value;
                d.ConversationTitle = conversationInfo.Title;
            });
            EntrySelected?.Invoke();
        }

        void OnSearchValueChanged(ChangeEvent<string> evt)
        {
            SetSearchFilter(evt.newValue);
        }

        void SetSearchFilter(string filter)
        {
            if (m_SearchFilter == filter)
            {
                return;
            }

            m_SearchFilter = filter;
            Reload(false, true);
        }
    }
}
