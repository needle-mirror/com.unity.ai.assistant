using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromServer;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.Editor.FunctionCalling;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Utilities
{
    static class OrchestrationUtilities
    {
        /// <summary>
        /// Get the functions currently exposed to MuseChat as valid function objects that can be used as a response to
        /// <see cref="CapabilitiesRequestV1"/>
        /// </summary>
        /// <returns></returns>
        internal static List<CapabilitiesResponseV1.FunctionsObject> GetFunctions()
        {
            List<FunctionDefinition> functions = SystemToolboxes
                .SmartContextToolbox
                .Tools
                .Select(c => c.FunctionDefinition).ToList();

            List<CapabilitiesResponseV1.FunctionsObject> FunctionObjects = functions.Select(f =>
            {
                return new CapabilitiesResponseV1.FunctionsObject()
                {
                    FunctionTag = f.Tags,
                    FunctionName = f.Name,
                    FunctionNamespace = f.Namespace,
                    FunctionDescription = f.Description,
                    FunctionId = f.FunctionId,
                    FunctionParameters = f.Parameters.Select(p => new CapabilitiesResponseV1.FunctionsObject.FunctionParametersObject()
                    {
                        ParameterName = p.Name,
                        ParameterType = p.Type,
                        ParameterDescription = p.Description,
                        ParameterIsOptional = p.Optional
                    }).ToList()
                };
            }).ToList();

            FunctionObjects.Add(CompilerTool.ToolDeclaration());
            FunctionObjects.Add(RunCommandValidatorTool.ToolDeclaration());
            FunctionObjects.Add(GetStaticProjectSettingsTool.ToolDeclaration());
            FunctionObjects.Add(GetUnityDependenciesTool.ToolDeclaration());
            FunctionObjects.Add(GetUnityVersionTool.ToolDeclaration());

            return FunctionObjects;
        }

        internal static CapabilitiesResponseV1.LaunchButtonObject GetPlugins()
        {
            List<FunctionDefinition> plugins = SystemToolboxes
                .PluginToolbox
                .Tools
                .Select(c => c.FunctionDefinition).ToList();

            IEnumerable<CapabilitiesResponseV1.LaunchButtonObject.LaunchButtonAction> pluginFunctions =
                plugins.Select(f =>
                {
                    return new CapabilitiesResponseV1.LaunchButtonObject.LaunchButtonAction()
                    {
                        FunctionId = f.FunctionId,
                        FunctionName = f.Name,
                        FunctionNamespace = f.Namespace,
                        FunctionDescription = f.Description,
                        FunctionParameters = f.Parameters.Select(p =>
                            new CapabilitiesResponseV1.LaunchButtonObject.LaunchButtonAction.
                                FunctionParametersObject()
                                {
                                    ParameterName = p.Name,
                                    ParameterType = p.Type,
                                    ParameterDescription = p.Description,
                                    ParameterIsOptional = p.Optional
                                }).ToList()
                    };
                });

            return new CapabilitiesResponseV1.LaunchButtonObject()
            {
                Functions = pluginFunctions.ToList()
            };
        }

        internal static Func<JObject, Task<JToken>> WrapAsAsync(Func<JObject, JToken> syncFunc)
        {
            return x => Task.FromResult(syncFunc(x));
        }
    }
}
