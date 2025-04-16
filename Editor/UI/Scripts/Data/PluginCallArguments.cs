using Newtonsoft.Json;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Data
{
    [System.Serializable]
    internal struct PluginCallArguments
    {
        [JsonProperty("function_id")]
        public string FunctionId { get; set; }

        [JsonProperty("parameters")]
        public string[] Parameters { get; set; }
    }
}
