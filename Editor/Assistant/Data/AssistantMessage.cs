using System;

namespace Unity.AI.Assistant.Editor.Data
{
    [Serializable]
    class AssistantMessage
    {
        /// <summary>
        /// Indicates that this is an error message and should be displayed as such
        /// </summary>
        public bool IsError;

        /// <summary>
        /// Indicates that the message is complete and no longer streaming in
        /// </summary>
        public bool IsComplete;

        /// <summary>
        /// The content of the message that should be displayed to the user
        /// </summary>
        public string Content;

        public AssistantMessageId Id;
        public string Role;
        public string SourceAttribution;
        public AssistantContextEntry[] Context;

        public long Timestamp;
        public int MessageIndex;

        public static AssistantMessage AsError(AssistantMessageId id, string message)
        {
            return new AssistantMessage()
            {
                Id = id,
                IsError = true,
                IsComplete = true,
                Content = message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }
}
