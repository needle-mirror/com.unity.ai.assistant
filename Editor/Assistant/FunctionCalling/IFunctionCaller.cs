using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    /// <summary>
    /// Handles the calling of functions in the AI Assistant system via string arguments provided by LLM. Requires
    /// access to system functions identifiable by a functionID. LLMs provide function arguments as arbitrary JSON
    /// objects, the shape of which depends on the purpose of the function being called.
    /// </summary>
    interface IFunctionCaller
    {
        /// <summary>
        /// Responsible for calling and constructing a response by looking up a function and calling it using
        /// parameters provided by an LLM in JObject format.
        /// </summary>
        /// <param name="functionId">The key used to look up the function</param>
        /// <param name="functionParameters">The JSON Object that contains the functions parameters</param>
        /// <returns></returns>
        void CallByLLM(ChatWorkflow workFlow, string functionId, JObject functionParameters, Guid callId);

        // TODO: The difference in signatures between CallByLLM and CallPlugin is strange. Plugins don't return, but should really be callable in the same way. This is a point in the code that could do with some thought. Despite this, it's still better to have both be part of this interface, so that the function calling system is centralized.
        /// <summary>
        /// Responsible for looking up plugins by functionId and calling them.
        /// </summary>
        /// <param name="functionId"></param>
        /// <param name="functionParameters"></param>
        /// <param name="context">Contextual data that can be passed to a plugin call through the <see cref="FunctionCallingContextBridge"/> system</param>
        /// <returns></returns>
        IFunctionCaller.CallResult CallPlugin(string functionId, string[] functionParameters, object context = null);


        public struct CallResult
        {
            /// <summary>
            /// Did the function call succeed
            /// </summary>
            public bool IsFunctionCallSucceeded;

            /// <summary>
            /// The result. Only present if the call was a success
            /// </summary>
            public JToken Result;

            public static CallResult SuccessfulResult(JToken response) => new()
            {
                IsFunctionCallSucceeded = true,
                Result = response
            };

            public static CallResult FailedResult(string error) => new()
            {
                IsFunctionCallSucceeded = false,
                Result = JToken.FromObject(error)
            };
        }
    }
}
