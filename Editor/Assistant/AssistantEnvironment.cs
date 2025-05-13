using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    [FilePath("AssistantEnv", FilePathAttribute.Location.PreferencesFolder)]
    internal class AssistantEnvironment : ScriptableSingleton<AssistantEnvironment>
    {
        const string k_DefaultApiUrl = "https://api-beta.prd.azure.muse.unity.com";
        const string k_DefaultWebSocketApiUrl = "wss://api-beta.prd.azure.muse.unity.com/v1/assistant/ws";

        [SerializeField]
        public string ApiUrl = k_DefaultApiUrl;

        [SerializeField]
        public string WebSocketApiUrl = k_DefaultWebSocketApiUrl;

        [SerializeField]
        public bool DebugModeEnabled;

        internal void SetApi(string apiUrl, string backend)
        {
            ApiUrl = apiUrl;
            Save(true);
        }

        internal void SetWebSocketApi(string apiUrl)
        {
            WebSocketApiUrl = apiUrl;
            Save(true);
        }

        internal void Reset()
        {
            ApiUrl = k_DefaultApiUrl;
            DebugModeEnabled = false;
            Save(true);
        }
    }
}
