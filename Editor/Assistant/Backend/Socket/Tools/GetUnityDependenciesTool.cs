using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class GetUnityDependenciesTool
    {
        internal const string k_FunctionId = "Unity.Muse.Chat.Backend.Socket.Tools.GetUnityDependenciesTool";

        internal static JToken Run()
        {
            return JToken.FromObject(UnityDataUtils.GetPackageMap()) as JObject;
        }

        internal async static Task CachePackageData()
        {
            UnityDataUtils.CachePackageData(false);

            // Wait for package data to get ready.
            // Do not do this when the editor is paused, because the delay will leave this thread and cause issues:
            if (!(UnityEditor.EditorApplication.isPlaying && UnityEditor.EditorApplication.isPaused))
            {
                for (var i = 0; !UnityDataUtils.PackageDataReady() && i < 200; i++)
                {
                    await Task.Delay(10);
                }
            }
        }

        internal static CapabilitiesResponseV1.FunctionsObject ToolDeclaration()
        {
            return new CapabilitiesResponseV1.FunctionsObject()
            {
                FunctionId = k_FunctionId,
                FunctionDescription = "Returns an object containing the info in the manifest.json of the Unity Editor, which contains packages and versions used by the editor.",
                FunctionName = "GetUnityDependencies",
                FunctionNamespace = "Unity.Muse.Chat.Backend.Socket.Tools",
                FunctionParameters = new List<CapabilitiesResponseV1.FunctionsObject.FunctionParametersObject>() { },
                FunctionTag = new List<string>() { "static-context" }
            };
        }

        public async static Task<JToken> Call(JObject arg)
        {
            // TODO: This converts to a string because all smart context functions should return a string. We should think about supporting arbitrary json instead
            await CachePackageData();
            return Run().ToString();
        }
    }
}
