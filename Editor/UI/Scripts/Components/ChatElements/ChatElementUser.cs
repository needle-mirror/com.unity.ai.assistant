using System.Collections.Generic;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementUser : ChatElementBase
    {
        readonly IList<VisualElement> k_TextFields = new List<VisualElement>();

        VisualElement m_TextFieldRoot;
        AssistantImage m_UserIcon;
        VisualElement m_UserIconFrame;
        Label m_UserName;
        Label m_CommandLabel;
        Foldout m_ContextFoldout;
        VisualElement m_ContextContent;

        /// <summary>
        /// Set the user data used by this element
        /// </summary>
        /// <param name="message">the message to display</param>
        public override void SetData(MessageModel message)
        {
            base.SetData(message);

            m_CommandLabel.text = string.IsNullOrEmpty(message.Command)
                ? string.Empty
                : $"/{message.Command}";

            RefreshText(m_TextFieldRoot, k_TextFields);
            RefreshContext();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ContextFoldout = view.Q<Foldout>("contextFoldout");
            m_ContextFoldout.SetValueWithoutNotify(false);

            m_ContextContent = view.Q<VisualElement>("contextContent");

            m_TextFieldRoot = view.Q<VisualElement>("userMessageTextFieldRoot");

            // Hide the icon until we find a way to display that:
            m_UserIcon = view.SetupImage("userIcon");
            m_UserIconFrame = view.Q<VisualElement>("userIconFrame");
            if (UserInfoCache.Avatar != null)
            {
                m_UserIcon.SetTexture(UserInfoCache.Avatar);
            }
            else
            {
                m_UserIconFrame.SetDisplay(false);
            }

            m_UserName = view.Q<Label>("userName");
            m_UserName.text = UserInfoCache.DisplayName;

            m_CommandLabel = view.Q<Label>("userCommandLabel");
        }

        void RefreshContext()
        {
            if (ContextEntries == null || ContextEntries.Count == 0)
            {
                m_ContextFoldout.style.display = DisplayStyle.None;
                return;
            }

            m_ContextFoldout.style.display = DisplayStyle.Flex;

            m_ContextContent.Clear();
            for (var index = 0; index < ContextEntries.Count; index++)
            {
                var contextEntry = ContextEntries[index];
                var entry = new ContextElement();
                entry.Initialize(Context);
                entry.SetData(contextEntry);
                entry.AddChatElementUserStyling();
                m_ContextContent.Add(entry);
            }
        }
    }
}
