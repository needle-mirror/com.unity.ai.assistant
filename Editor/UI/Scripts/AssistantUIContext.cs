using System;
using Unity.AI.Assistant.Editor;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal class AssistantUIContext
    {
        public AssistantUIContext()
        {
            // NOTE: For now we just default to the previous singleton, later we will divert into separate `Assistant` instances for open windows
            Blackboard = new AssistantBlackboard();
            API = new AssistantUIAPIInterpreter(AssistantInstance.instance.Value, Blackboard);
        }

        public readonly AssistantBlackboard Blackboard;
        public readonly AssistantUIAPIInterpreter API;

        public Action ConversationScrollToEndRequested;

        public void Initialize()
        {
            API.Initialize();

            Blackboard.ClearActiveConversation();
        }

        public void Deinitialize()
        {
            API.Deinitialize();
        }

        public void SendScrollToEndRequest()
        {
            ConversationScrollToEndRequested?.Invoke();
        }
    }
}
