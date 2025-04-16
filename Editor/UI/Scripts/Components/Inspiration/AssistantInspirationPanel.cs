using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Inspiration
{
    class AssistantInspirationPanel : ManagedTemplate
    {
        Button m_RefreshButton;

        VisualElement m_Content;

        static readonly List<InspirationModel> k_InspirationTempList = new();

        readonly IDictionary<string, IList<InspirationModel>> k_InspirationCache = new Dictionary<string, IList<InspirationModel>>();

        public AssistantInspirationPanel()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public event Action<InspirationModel> InspirationSelected;

        protected override void InitializeView(TemplateContainer view)
        {
            m_RefreshButton = view.SetupButton("refreshButton", _ =>
            {
                RefreshEntries();
                AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.RefreshInspirationalPrompt);
            });
            m_Content = view.Q<VisualElement>("inspirationSectionContent");

            Context.API.RefreshInspirations();

            Context.API.InspirationsRefreshed += RefreshEntries;
        }

        public void RefreshEntries()
        {
            RefreshCache();

            var modes = new[] { AskCommand.k_CommandName, RunCommand.k_CommandName, CodeCommand.k_CommandName };
            RefreshEntriesForCategory(m_Content, modes, maxEntries: 1);
        }

        void RefreshCache()
        {
            k_InspirationCache.Clear();
            var entries = Context.Blackboard.Inspirations;
            foreach (InspirationModel inspiration in entries)
            {
                if (!k_InspirationCache.TryGetValue(inspiration.Command, out var entryList))
                {
                    entryList = new List<InspirationModel>();
                    k_InspirationCache.Add(inspiration.Command, entryList);
                }

                entryList.Add(inspiration);
            }
        }

        private void RefreshEntriesForCategory(VisualElement targetRoot, string[] modes, int maxEntries = 1)
        {
            targetRoot.Clear();

            k_InspirationTempList.Clear();
            for (var i = 0; i < modes.Length; i++)
            {
                if (!k_InspirationCache.TryGetValue(modes[i], out var inspirations))
                {
                    continue;
                }

                k_InspirationTempList.AddRange(inspirations);
            }

            if (k_InspirationTempList.Count == 0)
            {
                return;
            }

            int entriesToSelect = Mathf.Min(maxEntries, k_InspirationTempList.Count);
            var shuffledIndices = Enumerable.Range(0, k_InspirationTempList.Count)
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(entriesToSelect);

            foreach (var inspiration in shuffledIndices.Select(index => k_InspirationTempList[index]))
            {
                var entry = new AssistantInspirationEntry();
                entry.Initialize(Context);
                entry.Value = inspiration;
                entry.Clicked += OnInspirationSelected;
                targetRoot.Add(entry);
            }
        }

        void OnInspirationSelected(InspirationModel value)
        {
            InspirationSelected?.Invoke(value);
        }
    }
}
