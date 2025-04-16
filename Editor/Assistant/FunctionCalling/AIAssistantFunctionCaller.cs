using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;
using Unity.AI.Assistant.Editor.Plugins;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    class AIAssistantFunctionCaller : IFunctionCaller
    {
        Dictionary<string, Func<JObject, Task<JToken>>> m_RegisteredCallsByLLM;

        public AIAssistantFunctionCaller()
        {
            m_RegisteredCallsByLLM = new()
            {
                { CompilerTool.k_FunctionId, OrchestrationUtilities.WrapAsAsync(CompilerTool.Call) },
                { RunCommandValidatorTool.k_FunctionId, OrchestrationUtilities.WrapAsAsync(RunCommandValidatorTool.Call) },
                { GetStaticProjectSettingsTool.k_FunctionId, OrchestrationUtilities.WrapAsAsync(GetStaticProjectSettingsTool.Call) },
                { GetUnityDependenciesTool.k_FunctionId, GetUnityDependenciesTool.Call },
                { GetUnityVersionTool.k_FunctionId, OrchestrationUtilities.WrapAsAsync(GetUnityVersionTool.Call) },
            };
        }

        /// <inheritdoc />
        public async Task<IFunctionCaller.CallResult> CallByLLM(string functionId, JObject parameters)
        {
            // Attempt to call as a registered function, if a registered function will the call does not exist, assume
            // that the function is a smart context function instead.
            if (m_RegisteredCallsByLLM.TryGetValue(functionId, out Func<JObject, Task<JToken>> call))
                return await CallBasicFunction(call, parameters);

            // Attempt to call as a smart context function, if this does not exist, then the function has not been
            // located by the system and cannot be called
            if (SystemToolboxes.SmartContextToolbox.ContainsFunctionById(functionId))
                return await CallSmartContextFunction(functionId, parameters);

            return IFunctionCaller.CallResult.FailedResult();
        }

        /// <inheritdoc />
        public Task CallPlugin(string functionId, string[] parameters, object context = null)
        {
            if (!SystemToolboxes.PluginToolbox.ContainsFunctionById(functionId))
                InternalLog.LogError($"Plugin with the id {functionId} does not exist");

            try
            {
                // TODO: Support Instance functions as plugins and remove the FunctionCallingContextBridge
                // Some plugin functions require the use of contextual access to the current conversation. To achieve
                // this while maintaining a proper separation of responsibilities, context is provided by the plugin
                // caller and posted to a static place that the function implementation must pull from to receive the
                // context. This works because after posting, the function is called immediately. There is a danger
                // that in async situations the order causes bugs here, but in practice plugins are sync.
                using var contextTracker = FunctionCallingContextBridge.GetCallContext(context);

                if (!SystemToolboxes.PluginToolbox.TryRunToolByID(functionId, parameters))
                    InternalLog.LogError($"Plugin with the id {functionId} failed, but did not throw an exception");
            }
            catch (Exception e )
            {
                InternalLog.LogError($"Plugin with the id {functionId} failed, and threw a {e.GetType().Name} Exception. {e.Message} {e.StackTrace} ");
            }

            return Task.CompletedTask;
        }

        async Task<IFunctionCaller.CallResult> CallBasicFunction(Func<JObject, Task<JToken>> func, JObject functionParameters)
        {
            try
            {
                JToken result = await func(functionParameters);
                return IFunctionCaller.CallResult.SuccessfulResult(result);
            }
            catch (Exception)
            {
                return IFunctionCaller.CallResult.FailedResult();
            }
        }

        async Task<IFunctionCaller.CallResult> CallSmartContextFunction(string functionId, JObject functionParameters)
        {
            try
            {
                InternalLog.Log($"Calling tool {functionId} ({functionParameters})");

                // Because of lack of time, we are still using a legacy way to pass arguments to Toolbox functions, so
                // we convert the functionParameters to the old format before calling the function.
                var argList = new List<string>();

                foreach (var item in functionParameters)
                {
                    if (item.Value.Type == JTokenType.Array)
                    {
                        List<string> values = new();

                        foreach (var element in item.Value)
                            values.Add(element.ToString());

                        argList.Add($"{item.Key}:[{string.Join(",", values)}]");
                    }
                    else
                    {
                        argList.Add($"{item.Key}:{item.Value.ToString()}");
                    }
                }

                var (functionSuccess, result)
                    = await SystemToolboxes.SmartContextToolbox.TryRunToolByIDAsync(functionId, argList.ToArray(),
                        AssistantSettings.PromptContextLimit);

                if (!functionSuccess)
                    return IFunctionCaller.CallResult.FailedResult();
                else
                {
                    var responseObj = new JObject();

                    var payload = result.Payload;

                    if (payload?.Length > AssistantSettings.PromptContextLimit)
                    {
                        payload = payload.Substring(0, AssistantSettings.PromptContextLimit);
                        InternalLog.LogError(
                            $"The context returned by the function was too long and was truncated. This should not happen, update {functionId} to return less data.");
                    }

                    responseObj.Add("payload", payload);
                    responseObj.Add("truncated", result.Truncated);
                    responseObj.Add("type", result.ContextType);
                    return IFunctionCaller.CallResult.SuccessfulResult(responseObj);
                }
            }
            catch (Exception)
            {
                return IFunctionCaller.CallResult.FailedResult();
            }
        }
    }
}
