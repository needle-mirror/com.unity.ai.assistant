using System;

namespace Unity.AI.Assistant.Editor.Data
{
    [Serializable]
    internal struct AssistantInspiration
    {
        public AssistantInspirationId Id;
        public string Command;
        public string Description;
        public string Value;
    }
}
