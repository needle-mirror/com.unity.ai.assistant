using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.ApplicationModels;

namespace Unity.AI.Assistant.Editor
{
    internal struct MessageFeedback
    {
        public AssistantMessageId MessageId;
        public bool FlagInappropriate;
        public Category Type;
        public string Message;
        public Sentiment Sentiment;
    }
}
