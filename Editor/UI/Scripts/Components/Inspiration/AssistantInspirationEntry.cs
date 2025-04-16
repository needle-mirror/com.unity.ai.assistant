using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Inspiration
{
    class AssistantInspirationEntry : ManagedTemplate
    {
        // This cache prevents multiple point cost requests regardless of the implementation of the cost further down
        static readonly IDictionary<string, int> k_PromptPointCostCache = new Dictionary<string, int>();

        InspirationModel m_Value;
        Button m_Button;
        Label m_Label;
        Label m_PointCostLabel;
        VisualElement m_PointCostDisplay;
        PointCostRequestId m_ActivePointCostRequestId = PointCostRequestId.Invalid;

        public AssistantInspirationEntry()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public event Action<InspirationModel> Clicked;

        public InspirationModel Value
        {
            get => m_Value;

            set
            {
                m_Value = value;
                m_Label.text = value.Value;

                RefreshPointCost();
            }
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Button = view.SetupButton("inspirationEntryButton", OnEntryClicked);
            m_Label = view.Q<Label>("inspirationEntryButtonLabel");

            m_PointCostDisplay = view.Q<VisualElement>("inspirationPointCostDisplayElement");
            m_PointCostLabel = view.Q<Label>("inspirationPointCostDisplayLabel");
            m_PointCostDisplay.SetDisplay(false);

            Context.API.PointCostReceived += OnPointCostReceived;
        }

        string GetPointCostPrompt()
        {
            string promptForCost = m_Value.Value;
            if (!string.IsNullOrEmpty(m_Value.Command))
            {
                promptForCost = $"/{m_Value.Command} {promptForCost}";
            }

            return promptForCost;
        }

        void RefreshPointCost()
        {
            string promptForCost = m_Value.Value;
            if (!string.IsNullOrEmpty(m_Value.Command))
            {
                promptForCost = $"/{m_Value.Command} {promptForCost}";
            }

            if (k_PromptPointCostCache.TryGetValue(promptForCost, out int cost))
            {
                // Avoid asking again for non-changing data
                m_PointCostLabel.text = cost.ToString();
                return;
            }

            m_ActivePointCostRequestId = PointCostRequestId.GetNext(AssistantConversationId.Invalid);
            Context.API.GetPointCost(m_ActivePointCostRequestId, new PointCostRequestData(promptForCost));
        }

        void OnPointCostReceived(PointCostRequestId id, int cost)
        {
            if (id != m_ActivePointCostRequestId)
            {
                // Not for us
                return;
            }

            m_PointCostDisplay.SetDisplay(cost != 0);

            string prompt = GetPointCostPrompt();
            k_PromptPointCostCache[prompt] = cost;
            m_ActivePointCostRequestId = PointCostRequestId.Invalid;
            m_PointCostLabel.text = cost.ToString();
        }

        void OnEntryClicked(PointerUpEvent evt)
        {
            Clicked?.Invoke(m_Value);

            AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.UseInspirationalPrompt, d => d.UsedInspirationalPrompt = m_Value.Value);
        }
    }
}
