using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Agent;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class RunCommandValidatorTool
    {
        internal const string k_FunctionId = "Unity.Muse.Chat.Backend.Socket.Tools.RunCommandValidator";

        internal static JObject Compile(string code)
        {
            var assembly = new DynamicAssemblyBuilder(RunCommandInterpreter.k_DynamicAssemblyName, RunCommandInterpreter.k_DynamicCommandNamespace)
                .CompileAndLoadAssembly(code, out var compilationErrors, out string localFixedCode);

            return new JObject()
            {
                { "isCompilationSuccessful" , assembly != null },
                { "compilationLogs", compilationErrors.ToString() },
                { "localFixedCode", localFixedCode }
            };
        }

        internal static CapabilitiesResponseV1.FunctionsObject ToolDeclaration()
        {
            return new CapabilitiesResponseV1.FunctionsObject()
            {
                FunctionId = k_FunctionId,
                FunctionDescription = "Validate that a run command is valid. Can be compiled and executed.",
                FunctionName = "RunCommandValidator",
                FunctionNamespace = "Unity.Muse.Chat.Backend.Socket.Tools",
                FunctionParameters = new List<CapabilitiesResponseV1.FunctionsObject.FunctionParametersObject>()
                {
                    new()
                    {
                        ParameterName = "code",
                        ParameterType = "string",
                        ParameterDescription = "The code to attempt to compile"
                    }
                },
                FunctionTag = new List<string>() { "code-correction" }
            };
        }

        public static JToken Call(JObject arg)
        {
            if (arg.TryGetValue("code", out JToken value))
                return Compile(value.ToString());

            return OrchestrationUtilities.GetFunctionCallFailureValue();
        }
    }
}
