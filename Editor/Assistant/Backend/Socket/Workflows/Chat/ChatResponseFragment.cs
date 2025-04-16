namespace Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat
{
    struct ChatResponseFragment
    {
        public string Fragment { get; set; }
        public string Id { get; set; }
        public bool IsLastFragment { get; set; }
    }
}
