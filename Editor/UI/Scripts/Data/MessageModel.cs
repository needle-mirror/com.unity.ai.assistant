using Unity.AI.Assistant.Editor.Data;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Data
{
    internal struct MessageModel
    {
        public AssistantMessageId Id;
        public string Content;

        public bool IsComplete;
        public MessageModelRole Role;

        // Note: For now we re-use the same data as the API layer for simplicity, there are several helper methods attached to this
        //       If major changes happen on it we will move to a distinct model for the UI
        public AssistantContextEntry[] Context;

        public int ErrorCode;
        public string ErrorText;
    }
}
