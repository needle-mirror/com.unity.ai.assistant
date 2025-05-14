using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{

    class ContextDropdown: ManagedTemplate
    {
        public ContextDropdown()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        internal class ContextDropdownListEntry
        {
            public AssistantContextEntry ContextEntry;
            [CanBeNull] public AssistantView Owner;
        }

        VisualElement m_Root;
        VisualElement m_ContextAdaptiveListViewContainer;
        AdaptiveListView<ContextDropdownListEntry, ContextElement> m_ContextListView;

        internal void ClearData()
        {
            m_ContextListView.ClearData();
        }

        internal void AddChoicesToDropdown(IList<AssistantContextEntry> entries, AssistantView owner = null)
        {
            m_ContextListView.BeginUpdate();

            foreach (var t in entries)
            {
                m_ContextListView?.AddData(
                    new ContextDropdownListEntry
                    {
                        ContextEntry = t,
                        Owner = owner
                    }
                );
            }

            m_ContextListView?.EndUpdate(false, false);
        }

        internal IList<ContextDropdownListEntry> GetEntries()
        {
            return m_ContextListView.Data;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("dropdownRoot");
            m_ContextAdaptiveListViewContainer = view.Q<VisualElement>("contextDropDownViewContainer");

            m_ContextListView = new()
            {
                EnableDelayedElements = false,
                EnableVirtualization = false,
                EnableScrollLock = true,
                EnableHorizontalScroll = false
            };

            m_ContextListView.Initialize(Context);
            m_ContextAdaptiveListViewContainer.Add(m_ContextListView);
        }
    }
}
