using Unity.AI.Assistant.Editor.Data;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Data
{
    internal struct MessageModel
    {
        public AssistantMessageId Id;
        public string Content;

        // Note: This is purely for display and optional, do NOT use this for any processing
        //       i.e do not depend on this being there at all times!
        public string Command;

        // Note: This is temporary to fix https://jira.unity3d.com/browse/BASST-266
        public string SourceAttribution;

        public bool IsComplete;
        public MessageModelRole Role;

        // Note: For now we re-use the same data as the API layer for simplicity, there are several helper methods attached to this
        //       If major changes happen on it we will move to a distinct model for the UI
        public AssistantContextEntry[] Context;
        public FeedbackData? Feedback;
    }
}
