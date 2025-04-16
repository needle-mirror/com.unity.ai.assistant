using System;

namespace Unity.AI.Assistant.Editor.Data
{
    [Serializable]
    class AssistantMessage
    {
        public AssistantMessageId Id;
        public string Author;
        public string Role;
        public string Content;
        public AssistantContextEntry[] Context;
        public bool IsComplete;
        public int ErrorCode;
        public string ErrorText;
        public bool IsError => ErrorCode != 0;
        public long Timestamp;
        public int MessageIndex;
    }
}
