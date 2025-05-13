using System.Collections.Generic;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
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

        public readonly List<Object> ObjectAttachments = new();
        public readonly List<LogData> ConsoleAttachments = new();
    }
}
