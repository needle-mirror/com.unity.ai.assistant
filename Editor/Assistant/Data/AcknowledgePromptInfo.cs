using System;

namespace Unity.AI.Assistant.Editor.Data
{
    [Serializable]
    class AcknowledgePromptInfo
    {
        public string Id;
        public string Content;
        public AssistantContextEntry[] Context;
    }
}
