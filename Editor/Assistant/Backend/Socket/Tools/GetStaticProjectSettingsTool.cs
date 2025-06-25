using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class GetStaticProjectSettingsTool
    {
        internal const string k_FunctionId = "Unity.Muse.Chat.Backend.Socket.Tools.GetStaticProjectSettingsTool";

        internal static JToken Run()
        {
            return JToken.FromObject(UnityDataUtils.GetProjectSettingSummary());
        }

        internal static CapabilitiesResponseV1.FunctionsObject ToolDeclaration()
        {
            return new CapabilitiesResponseV1.FunctionsObject()
            {
                FunctionId = k_FunctionId,
                FunctionDescription = "Returns a object containing the Active Render Pipeline, Target Platform/OS, API Compatibility Level and Input System of the running editor",
                FunctionName = "GetStaticProjectSettings",
                FunctionNamespace = "Unity.Muse.Chat.Backend.Socket.Tools",
                FunctionParameters = new List<CapabilitiesResponseV1.FunctionsObject.FunctionParametersObject>() { },
                FunctionTag = new List<string>() { "static-context" }
            };
        }

        public static JToken Call(JToken arg)
        {
            // TODO: This converts to a string because all smart context functions should return a string. We should think about supporting arbitrary json instead
            return Run().ToString();
        }
    }
}
