using System.IO;
using JetBrains.Annotations;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [UsedImplicitly]
    class ChatElementWrapper : AdaptiveListViewEntry
    {
        VisualElement m_Root;

        ChatElementBase m_ChatElement;

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("wrapperRoot");
        }

        public override void SetData(int index, object data, bool isSelected = false)
        {
            base.SetData(index, data);

            var message = (MessageModel)data;
            SetupChatElement(ref m_ChatElement, message);
        }

        void SetupChatElement(ref ChatElementBase element, MessageModel message)
        {
            bool hideIfEmpty = false;
            switch (message.Role)
            {
                case MessageModelRole.System:
                case MessageModelRole.Error:
                case MessageModelRole.Assistant:
                {
                    hideIfEmpty = true;
                    break;
                }
            }

            if (element == null)
            {
                switch (message.Role)
                {
                    case MessageModelRole.User:
                        element = new ChatElementUser();
                        break;

                    case MessageModelRole.System:
                        element = new ChatElementSystem();
                        break;

                    case MessageModelRole.Error:
                    case MessageModelRole.Assistant:
                        element = new ChatElementResponse();
                        break;

                    default:
                    {
                        throw new InvalidDataException("Unknown Role: " + message.Role);
                    }
                }

                element.Initialize(Context);
            }

            if (hideIfEmpty && string.IsNullOrEmpty(message.Content))
            {
                element.SetDisplay(false);
            }
            else
            {
                element.SetDisplay(true);
            }

            var contentEquals = element.Message.Content == message.Content;
            if (!string.IsNullOrEmpty(message.Content) && !string.IsNullOrEmpty(element.Message.Content))
            {
                contentEquals = element.Message.Content.Trim() == message.Content.Trim();
            }

            var feedbackEquals = element.Message.Feedback.Equals(message.Feedback);
            var contextEquals = ArrayUtils.ArrayEquals(element.Message.Context, message.Context);
            var completeEquals = element.Message.IsComplete == message.IsComplete;
            if (contentEquals &&
                feedbackEquals &&
                contextEquals &&
                completeEquals)   // complete flag removes last word when false.
            {
                // No change to content, no need to update
                return;
            }

            element.SetData(message);

            m_Root.Add(element);
        }
    }
}
