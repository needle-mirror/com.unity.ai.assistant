using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat;
using Unity.AI.Assistant.Editor.Plugins;
using Unity.AI.Assistant.Editor.Utils;
using UnityEngine;

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
        public void CallByLLM(ChatWorkflow workFlow, string functionId, JObject parameters, Guid callId)
        {
            TaskUtils.DispatchToMainThread(async () =>
            {
                IFunctionCaller.CallResult result;

                // Attempt to call as a registered function, if a registered function will the call does not exist, assume
                // that the function is a smart context function instead.
                if (m_RegisteredCallsByLLM.TryGetValue(functionId, out Func<JObject, Task<JToken>> call))
                    result = await CallBasicFunction(call, parameters);
                // Attempt to call as a smart context function, if this does not exist, then the function has not been
                // located by the system and cannot be called
                else if (SystemToolboxes.SmartContextToolbox.ContainsFunctionById(functionId))
                    result = await CallSmartContextFunction(functionId, parameters);
                else
                    result = IFunctionCaller.CallResult.FailedResult($"Could not find function '{functionId}'");

                workFlow.SendFunctionCallResponse(result, callId);
            });
        }

        /// <inheritdoc />
       public IFunctionCaller.CallResult CallPlugin(string functionId, string[] parameters, object context = null)
        {
            try
            {
                SystemToolboxes.PluginToolbox.RunToolByID(functionId, parameters);
                return IFunctionCaller.CallResult.SuccessfulResult(null);
            }
            catch (Exception e)
            {
                return IFunctionCaller.CallResult.FailedResult(GetExceptionErrorMessage(e));
            }
        }

        async Task<IFunctionCaller.CallResult> CallBasicFunction(Func<JObject, Task<JToken>> func, JObject functionParameters)
        {
            try
            {
                JToken result = await func(functionParameters);
                return IFunctionCaller.CallResult.SuccessfulResult(result);
            }
            catch (Exception e)
            {
                return IFunctionCaller.CallResult.FailedResult(GetExceptionErrorMessage(e));
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

                var result = await SystemToolboxes.SmartContextToolbox.RunToolByIDAsync(functionId, argList.ToArray());

                var responseObj = new JObject();

                var payload = result.Payload;
                if (payload?.Length > AssistantMessageSizeConstraints.ContextLimit)
                {
                    payload = payload.Substring(0, AssistantMessageSizeConstraints.ContextLimit);
                    InternalLog.LogError(
                        $"The context returned by the function was too long and was truncated. This should not happen, update {functionId} to return less data.");
                }

                responseObj.Add("payload", payload);
                responseObj.Add("truncated", result.Truncated);
                responseObj.Add("type", result.ContextType);

                return IFunctionCaller.CallResult.SuccessfulResult(responseObj);
            }
            catch (Exception e)
            {
                return IFunctionCaller.CallResult.FailedResult(GetExceptionErrorMessage(e));
            }
        }

        static string GetExceptionErrorMessage(Exception e)
        {
            return e.InnerException?.Message ?? e.Message;
        }
    }
}
