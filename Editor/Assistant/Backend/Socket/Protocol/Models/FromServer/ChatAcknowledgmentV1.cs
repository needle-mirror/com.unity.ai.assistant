using Newtonsoft.Json;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromServer
{
    #pragma warning disable // Disable all warnings

    class ChatAcknowledgmentV1 : IModel
    {
        [JsonProperty("$type")]
        public const string Type = "CHAT_ACKNOWLEDGMENT_V1";
        public string GetModelType() => Type;

        [JsonProperty("message_id", Required = Required.Always)]
        public string MessageId { get; set; }
    }
}
