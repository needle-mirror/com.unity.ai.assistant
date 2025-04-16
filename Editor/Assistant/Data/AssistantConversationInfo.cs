namespace Unity.AI.Assistant.Editor.Data
{
    internal struct AssistantConversationInfo
    {
        public AssistantConversationId Id;
        public string Title;
        public long LastMessageTimestamp;
        public bool IsContextual;
        public bool IsFavorite;
    }
}
