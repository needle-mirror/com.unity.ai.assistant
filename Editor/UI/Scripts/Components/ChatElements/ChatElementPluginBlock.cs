using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementPluginBlock : ManagedTemplate
    {
        VisualElement m_BlockRoot;
        Label m_TitleText;

        public PluginButtonData[] PluginCalls { get; private set; }
        public string BlockTitle { get; private set; }

        /// <summary>
        /// Create a new shared chat element
        /// </summary>
        public ChatElementPluginBlock()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        /// <summary>
        /// Set the data for this source element
        /// </summary>
        /// <param name="blockTitle">The title of the block</param>
        /// <param name="pluginCalls">the source block defining the URL and title</param>
        public void SetData(string blockTitle, PluginButtonData[] pluginCalls)
        {
            PluginCalls = pluginCalls;
            BlockTitle = blockTitle;
            RefreshDisplay();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_TitleText = view.Q("titleText") as Label;
            m_BlockRoot = view.Q("blockRoot");
        }

        void RefreshDisplay()
        {
            m_TitleText.text = BlockTitle;
            RenderPluginCalls();
        }

        void RenderPluginCalls()
        {
            RemoveNonTitleElements();

            foreach (PluginButtonData call in PluginCalls)
            {
                ChatElementPluginButton button = new();
                button.Initialize(Context);
                button.SetData(call);
                m_BlockRoot.Add(button);
            }
        }

        void RemoveNonTitleElements()
        {
            foreach (var element in m_BlockRoot.Children().Where(element => element != m_TitleText).ToArray())
                m_BlockRoot.Remove(element);
        }
    }
}
