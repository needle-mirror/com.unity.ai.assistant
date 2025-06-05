using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Commands;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    internal class RoutesPopup : ManagedTemplate
    {
        VisualElement m_Root;
        VisualElement m_PopupEntriesRoot;
        VisualElement m_NoResultsEntry;
        VisualElement m_Disclaimer;
        PreviewChip m_PreviewChip;
        const string k_FirstPopupItem = "mui-first-popup-item";
        const string k_LastPopupItem = "mui-last-popup-item";
        const string k_DisclaimerName = "commandItemExperimentalDisclaimer";

        List<string> m_RouteLabels;

        internal Action<string> m_OnSelectionChanged;

        public RoutesPopup()
            : base(AssistantUIConstants.UIModulePath)
        {
            VisibleEntries = new List<RoutesPopupEntry>();
        }

        internal IList<RoutesPopupEntry> VisibleEntries { get; private set; }

        RoutesPopupEntry InitializeRoutesPopupEntry(ChatCommandHandler commandHandler)
        {
            var newPopupItem = new RoutesPopupEntry();
            newPopupItem.Initialize(Context);
            newPopupItem.SetCommand(commandHandler);

            if (commandHandler.IsPreview)
            {
                m_Disclaimer = newPopupItem.Q(k_DisclaimerName);
                m_PreviewChip = new PreviewChip();
                m_PreviewChip.Initialize(Context);
                m_Disclaimer.Add(m_PreviewChip);
            }

            return newPopupItem;
        }

        void BuildRoutesEntries()
        {
            var commands = ChatCommands.GetCommands();

            for (int i = 0; i < commands.Count; i++)
            {
                ChatCommands.TryGetCommandHandler(commands[i], out var commandHandler);
                var newPopupItem = InitializeRoutesPopupEntry(commandHandler);
                newPopupItem.name = commandHandler.Command;
                VisibleEntries.Add(newPopupItem);
                DisplayRoutes();
            }
        }

        internal void DisplayRoutes()
        {
            m_PopupEntriesRoot.Clear();
            foreach (var entry in VisibleEntries)
            {
                m_PopupEntriesRoot.Add(entry);
            }
        }

        internal bool UpdateFilteredRoutes(string routeFilter = "", bool bypassFilter = false)
        {
            int lastIndex = -1;
            var commands = ChatCommands.GetCommands();
            VisibleEntries.Clear();

            if (routeFilter.Length > 0 || bypassFilter)
            {
                foreach (var command in commands)
                {
                    ChatCommands.TryGetCommandHandler(command, out var commandHandler);
                    var commandShown = commandHandler.Label.StartsWith(routeFilter) && commandHandler.ShowInList;
                    if (commandShown)
                    {
                        var newPopupItem = InitializeRoutesPopupEntry(commandHandler);
                        newPopupItem.RemoveFromClassList(k_FirstPopupItem);
                        newPopupItem.RemoveFromClassList(k_LastPopupItem);
                        newPopupItem.RegisterCallback<ClickEvent>(_ => ChangeRoute(commandHandler.Command));
                        VisibleEntries.Add(newPopupItem);
                        lastIndex += 1;
                    }
                }
            }

            if (VisibleEntries.Count > 0)
            {
                VisibleEntries[0].AddToClassList(k_FirstPopupItem);
            }

            if (lastIndex >= 0)
            {
                VisibleEntries[lastIndex].AddToClassList(k_LastPopupItem);
            }

            m_NoResultsEntry.style.display = VisibleEntries.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (lastIndex > -1)
            {
                base.Show();
            }
            else
            {
                base.Hide();
            }

            DisplayRoutes();

            return lastIndex > -1;
        }

        internal void HideNoResultsEntry()
        {
            base.Hide();
            m_NoResultsEntry.style.display = DisplayStyle.None;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("popupRoot");
            m_PopupEntriesRoot = view.Q<VisualElement>("popupEntriesRoot");
            m_NoResultsEntry = view.Q<VisualElement>("noResultsEntry");
            BuildRoutesEntries();
        }

        void ChangeRoute (string command)
        {
            m_OnSelectionChanged.Invoke(command);

            // Report the command type change through shortcuts
            AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.ChooseModeFromShortcut, d =>
            {
                d.ChosenMode = command;
            });
        }
    }
}
