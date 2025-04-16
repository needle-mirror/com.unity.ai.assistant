using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    class CachedFunction
    {
        /// <summary>
        /// This is metadata associated with the function that can be provided by a <see cref="IFunctionSource"/>. The
        /// keys and values of function metadata depends on the FunctionSource.
        /// </summary>
        public Dictionary<string, string> MetaData;
        public MethodInfo Method;
        public FunctionDefinition FunctionDefinition;

        public object Invoke(object[] parameters)
        {
            if (Method == null)
            {
                InternalLog.LogError("Trying to invoke a null function!");
                return null;
            }

            // Is this an async function?  Then log a warning and return null
            var isAsync = Method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;

            if (isAsync)
            {
                InternalLog.LogWarning($"{Method.Name} is an async function - call it through InvokeAsync.  Skipping.");
                return null;
            }
            return Method.Invoke(null, parameters);
        }

        public async Task<object> InvokeAsync(object[] parameters)
        {
            if (Method == null)
            {
                InternalLog.LogError("Trying to invoke a null function!");
                return null;
            }

            var isAsync = Method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;

            if (isAsync)
            {
                var task = (Task)Method.Invoke(null, parameters);
                await task;
                var resultProperty = task.GetType().GetProperty("result");
                var result = resultProperty.GetValue(task);
                return result;
            }

            return Method.Invoke(null, parameters);
        }
    }
}
