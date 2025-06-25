using JetBrains.Annotations;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.History
{
    [UsedImplicitly]
    class HistoryPanelEntryWrapper : AdaptiveListViewEntry
    {
        const string k_HeaderClassName = "mui-history-panel-header-entry";

        VisualElement m_Root;

        ManagedTemplate m_Element;

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("historyEntryWrapperRoot");

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        public override void SetData(int index, object data, bool isSelected = false)
        {
            base.SetData(index, data);

            if (data is string headerText)
            {
                SetupHeaderElement(headerText);
            }
            else
            {
                var conversation = data as ConversationModel;
                SetupEntryElement(index, conversation, isSelected);
            }
        }

        void SetupHeaderElement(string text)
        {
            if (m_Element is HistoryPanelHeaderEntry headerEntry)
            {
                // Already the right element, just update
                headerEntry.SetText(text);
                return;
            }

            if (m_Element != null)
            {
                DestroyElement();
            }

            var element = new HistoryPanelHeaderEntry();
            element.Initialize(Context);
            AddToClassList(k_HeaderClassName);
            element.SetText(text);
            m_Root.Add(element);
            m_Element = element;
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            DestroyElement();
        }

        void OnAttach(AttachToPanelEvent evt)
        {
        }

        void SetupEntryElement(int index, ConversationModel data, bool isSelected)
        {
            if (m_Element is HistoryPanelConversationEntry headerEntry)
            {
                // Already the right element, just update
                headerEntry.SetData(index, data, isSelected);
                return;
            }

            if (m_Element != null)
            {
                DestroyElement();
            }

            var element = new HistoryPanelConversationEntry();
            element.Initialize(Context);
            element.Selected += OnConversationSelected;
            element.SetData(index, data, isSelected);
            m_Root.Add(element);
            m_Element = element;
        }

        void OnConversationSelected(int index, ConversationModel conversation)
        {
            NotifySelectionChanged();
        }

        void DestroyElement()
        {
            if (m_Element == null)
            {
                return;
            }

            if (m_Element is HistoryPanelConversationEntry conversationEntry)
            {
                conversationEntry.Selected -= OnConversationSelected;
            }
            else
            {
                RemoveFromClassList(k_HeaderClassName);
            }

            m_Root.Remove(m_Element);
            m_Element = null;
        }
    }
}
