using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementPluginButton : ManagedTemplate
    {
        Button m_Button;
        Label m_Text;

        public PluginButtonData PluginCallArguments { get; private set; }

        /// <summary>
        /// Create a new shared chat element
        /// </summary>
        public ChatElementPluginButton()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        /// <summary>
        /// Set the data for this source element
        /// </summary>
        /// <param name="index">the index of the source</param>
        /// <param name="callArguments">the source block defining the URL and title</param>
        public void SetData(PluginButtonData callArguments)
        {
            PluginCallArguments = callArguments;
            RefreshDisplay();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Text = view.Q<Label>("pluginButtonLabel");

            m_Button = view.SetupButton("pluginButton", OnSourceClicked);
            m_Button.style.width = new StyleLength(StyleKeyword.Auto);
        }

        void OnSourceClicked(PointerUpEvent evt)
        {
            Context.API.HandleFunctionCall(
                PluginCallArguments.RegisteredPlugin.FunctionId,
                PluginCallArguments.CallArguments.Parameters,
                Context);

            AIAssistantAnalytics.ReportPluginEvent(PluginSubType.CallPlugin, d =>
            {
                d.PluginLabel = PluginCallArguments.RegisteredPlugin.FunctionId;
            });
        }

        void RefreshDisplay()
        {
            m_Text.text = string.IsNullOrEmpty(PluginCallArguments.RegisteredPlugin.PromptText)
                ? string.Join(", ", PluginCallArguments.CallArguments.Parameters)
                : $"{PluginCallArguments.RegisteredPlugin.PromptText}: ({ string.Join(", ", PluginCallArguments.CallArguments.Parameters) })";

            m_Button.text = string.IsNullOrEmpty(PluginCallArguments.RegisteredPlugin.ButtonText)
                ? "Perform Action"
                : PluginCallArguments.RegisteredPlugin.ButtonText;
        }
    }
}
