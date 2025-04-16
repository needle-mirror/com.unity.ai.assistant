using System.Collections.Generic;
using Unity.AI.Assistant.Bridge;
using Unity.AI.Assistant.Bridge.Editor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Data
{
    internal class AssistantPrompt
    {
        public AssistantPrompt(string prompt)
        {
            Value = prompt;
        }

        public string Value;
        public AssistantConversationId ConversationId;

        public readonly List<Object> ObjectAttachments = new();
        public readonly List<LogData> ConsoleAttachments = new();
    }
}
