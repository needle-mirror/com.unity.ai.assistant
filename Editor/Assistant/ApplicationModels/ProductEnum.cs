using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Unity.AI.Assistant.Editor.ApplicationModels
{
    /// <summary>
    /// Defines ProductEnum
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum ProductEnum
    {
        /// <summary>
        /// Enum AiAssistant for value: ai-assistant
        /// </summary>
        [EnumMember(Value = "ai-assistant")]
        AiAssistant = 1,

        /// <summary>
        /// Enum MuseBehavior for value: muse-behavior
        /// </summary>
        [EnumMember(Value = "muse-behavior")]
        MuseBehavior = 2,

        /// <summary>
        /// Enum WorldAi for value: world-ai
        /// </summary>
        [EnumMember(Value = "world-ai")]
        WorldAi = 3,

        /// <summary>
        /// Enum CodeGen for value: code_gen
        /// </summary>
        [EnumMember(Value = "code_gen")]
        CodeGen = 4,

        /// <summary>
        /// Enum Match3 for value: match3
        /// </summary>
        [EnumMember(Value = "match3")]
        Match3 = 5
    }
}
