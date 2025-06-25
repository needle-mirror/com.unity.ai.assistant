using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class GetUnityVersionTool
    {
        internal const string k_FunctionId = "Unity.Muse.Chat.Backend.Socket.Tools.GetUnityVersion";

        internal static JToken Run()
        {
            return JToken.FromObject(UnityDataUtils.GetProjectVersion(UnityDataUtils.VersionDetail.Revision));
        }

        internal static CapabilitiesResponseV1.FunctionsObject ToolDeclaration()
        {
            return new CapabilitiesResponseV1.FunctionsObject()
            {
                FunctionId = k_FunctionId,
                FunctionDescription = "Returns the version of the Unity Editor as a string.",
                FunctionName = "GetUnityVersion",
                FunctionNamespace = "Unity.Muse.Chat.Backend.Socket.Tools",
                FunctionParameters = new List<CapabilitiesResponseV1.FunctionsObject.FunctionParametersObject>() { },
                FunctionTag = new List<string>() { "static-context" }
            };
        }

        public static JToken Call(JObject arg)
        {
            // TODO: This converts to a string because all smart context functions should return a string. We should think about supporting arbitrary json instead
            return Run().ToString();
        }
    }
}
