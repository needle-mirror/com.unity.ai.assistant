using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class CompilerTool
    {
        internal const string k_FunctionId = "Unity.Muse.Chat.Backend.Socket.Tools.Compiler";

        internal static JObject Compile(string code)
        {
            var assembly = new DynamicAssemblyBuilder("Unity.Muse.CodeGen")
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
                FunctionDescription = "Let's you try to compile code on the frontend from the backend",
                FunctionName = "Compiler",
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

            else
                return OrchestrationUtilities.GetFunctionCallFailureValue();
        }
    }
}
