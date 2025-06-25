using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient
{
#pragma warning disable // Disable all warnings

    /// <summary>
    /// A documented list of the functions that are available to be called on the editor.
    /// <br/>
    /// <br/>The descriptions of the function and parameters are important because backend LLMs
    /// <br/>will decide to call (or not call) the functions using the info provided here.
    /// <br/>
    /// </summary>
    class CapabilitiesResponseV1 : IModel
    {
        [JsonProperty("$type")] public const string Type = "CAPABILITIES_RESPONSE_V1";

        public string GetModelType() => Type;

        /// <summary>
        /// The output formats that the client supports.
        /// <br/>
        /// <br/>Examples:
        /// <br/>  markdown,
        /// <br/>  code,
        /// <br/>  action,
        /// <br/>  plugins (Animate, Texture, Sprite),
        /// <br/>  match3
        /// <br/>
        /// </summary>
        [JsonProperty("outputs", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public List<LaunchButtonObject> Outputs { get; set; }

        [JsonProperty("functions", Required = Required.Always)]
        public List<FunctionsObject> Functions { get; set; } = new();


        public partial class FunctionsObject
        {
            /// <summary>
            /// Must uniquely identify the function within this function array. The client must be able to receive
            /// this string and locate the function call it is attached too. It is up to the client to choose the
            /// shape of this identifier manage the binding between this id and the function.
            ///
            /// Examples: 1, b75cb780-d943-4a42-bc9f-9780b883016a, Namespace.Tools::Class.Function_arg1_return
            /// <br/>
            /// <br/>Example: ContextExtraction
            /// <br/>
            /// </summary>
            [JsonProperty("function_id", Required = Required.DisallowNull,
                NullValueHandling = NullValueHandling.Ignore)]
            public string FunctionId { get; set; }

            /// <summary>
            /// Groups functions together.
            /// <br/>
            /// <br/>Example: ContextExtraction
            /// <br/>
            /// </summary>
            [JsonProperty("function_tag", Required = Required.DisallowNull,
                NullValueHandling = NullValueHandling.Ignore)]
            public List<string> FunctionTag { get; set; }

            /// <summary>
            /// This value MAY be overridden by the backend.
            /// <br/>
            /// <br/>We are allowing the client to specify them, so that we can dynamically add functions.
            /// <br/>
            /// </summary>
            [JsonProperty("function_description", Required = Required.DisallowNull,
                NullValueHandling = NullValueHandling.Ignore)]
            public string FunctionDescription { get; set; }

            /// <summary>
            /// Example - Unity.Muse.Chat.Context.SmartContext.ContextRetrievalTools:ProjectStructureExtractor
            /// <br/>Warning - Function namespace + Function names must be unique.
            /// <br/>
            /// </summary>
            [JsonProperty("function_namespace", Required = Required.DisallowNull,
                NullValueHandling = NullValueHandling.Ignore)]
            public string FunctionNamespace { get; set; }

            /// <summary>
            /// Example - ProjectStructureExtractor
            /// <br/>Warning - Function namespace + Function names must be unique.
            /// <br/>
            /// </summary>
            [JsonProperty("function_name", Required = Required.DisallowNull,
                NullValueHandling = NullValueHandling.Ignore)]
            public string FunctionName { get; set; }

            /// <summary>
            /// The parameters that are required to call the function - order is important,
            /// <br/>name is maybe not used for function calling.
            /// <br/>
            /// </summary>
            [JsonProperty("function_parameters", Required = Required.DisallowNull,
                NullValueHandling = NullValueHandling.Ignore)]
            public List<FunctionParametersObject> FunctionParameters { get; set; }


            public partial class FunctionParametersObject
            {
                /// <summary>
                /// The name of the parameter
                /// <br/>
                /// </summary>
                [JsonProperty("parameter_name", Required = Required.Always)]
                public string ParameterName { get; set; }

                /// <summary>
                /// The parameters type, in the form of the origin language. I.E.
                /// <br/>functions originating from Unity should be C# types.
                /// <br/>
                /// </summary>
                [JsonProperty("parameter_type", Required = Required.Always)]
                public string ParameterType { get; set; }

                /// <summary>
                /// A description of the parameter used by the LLM
                /// <br/>
                /// </summary>
                [JsonProperty("parameter_description", Required = Required.Always)]
                public string ParameterDescription { get; set; }

                /// <summary>
                /// Whether this parameter is optional or not
                /// </summary>
                [JsonProperty("parameter_is_optional")]
                public bool ParameterIsOptional { get; set; } = false;
            }
        }

        public class LaunchButtonObject
        {
            [JsonProperty("output_name", Required = Required.Always)]
            public const string OutputName = "launch-button";

            [JsonProperty("functions", Required = Required.Always)]
            public List<LaunchButtonAction> Functions { get; set; }

            public partial class LaunchButtonAction
            {
                [JsonProperty("function_id", Required = Required.DisallowNull,
                    NullValueHandling = NullValueHandling.Ignore)]
                public string FunctionId { get; set; }

                [JsonProperty("function_namespace", NullValueHandling = NullValueHandling.Ignore)]
                public string FunctionNamespace { get; set; }

                [JsonProperty("function_name", NullValueHandling = NullValueHandling.Ignore)]
                public string FunctionName { get; set; }

                [JsonProperty("function_description", Required = Required.DisallowNull,
                    NullValueHandling = NullValueHandling.Ignore)]
                public string FunctionDescription { get; set; }

                [JsonProperty("function_parameters", Required = Required.DisallowNull,
                    NullValueHandling = NullValueHandling.Ignore)]
                public List<FunctionParametersObject> FunctionParameters { get; set; }

                public partial class FunctionParametersObject
                {
                    [JsonProperty("parameter_name", Required = Required.Always)]
                    public string ParameterName { get; set; }

                    [JsonProperty("parameter_type", Required = Required.Always)]
                    public string ParameterType { get; set; }

                    [JsonProperty("parameter_description", Required = Required.Always)]
                    public string ParameterDescription { get; set; }

                    [JsonProperty("parameter_is_optional")]
                    public bool ParameterIsOptional { get; set; } = false;
                }
            }
        }
    }
}
