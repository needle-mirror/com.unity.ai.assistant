using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal class AssistantUISessionState : ScriptableSingleton<AssistantUISessionState>
    {
        const string k_HistoryOpen = "AssistantUserSession_HistoryOpen";
        const string k_LastActiveConversationId = "AssistantUserSession_LastActiveConversationId";
        const string k_Prompt = "AssistantUserSession_Prompt";
        const string k_Command = "AssistantUserSession_Command";
        const string k_Context = "AssistantUserSession_Context";

        public bool IsHistoryOpen
        {
            get => SessionState.GetBool(k_HistoryOpen, false);
            set => SessionState.SetBool(k_HistoryOpen, value);
        }

        public string LastActiveConversationId
        {
            get => SessionState.GetString(k_LastActiveConversationId, null);
            set => SessionState.SetString(k_LastActiveConversationId, value);
        }

        public string Context
        {
            get => SessionState.GetString(k_Context, null);
            set => SessionState.SetString(k_Context, value);
        }

        public string Prompt
        {
            get => SessionState.GetString(k_Command, null);
            set => SessionState.SetString(k_Command, value);
        }

        public string Command
        {
            get => SessionState.GetString(k_Prompt, null);
            set => SessionState.SetString(k_Prompt, value);
        }
    }
}
