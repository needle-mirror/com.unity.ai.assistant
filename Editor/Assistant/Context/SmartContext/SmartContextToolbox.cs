using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Context.SmartContext
{
    partial class SmartContextToolbox : FunctionToolbox
    {
#if MUSE_INTERNAL
        internal static event Action<string, object[]> OnCachedFunctionCalled;
#endif

        /// <summary>
        ///     Create a toolbox.
        ///     The Toolbox will use mthods returned by the contextProviderSource to build a list of available tools.
        /// </summary>
        /// <param name="functionCache">Provides context methods</param>
        public SmartContextToolbox(FunctionCache functionCache)
            : base(functionCache, FunctionCallingUtilities.k_SmartContextTag)
        {
        }

        /// <summary>
        /// Executes a context retrieval tool by name with the given arguments.
        /// </summary>
        /// <param name="name">Name of the tool function.</param>
        /// <param name="args">Arguments to pass to the tool function.</param>
        /// <param name="output">Output from the tool function</param>
        public void RunToolByName(string name, string[] args, out IContextSelection output)
        {
            GetSelectorAndConvertArgs(name, args, out var tool, out var convertedArgs);
            var result = (ExtractedContext)tool.Invoke(convertedArgs);
            output = result != null ? new ContextSelection(tool, result) : null;
        }

        public void RunToolByID(string id, string[] args, out IContextSelection output)
        {
            GetSelectorIdAndConvertArgs(id, args, out var tool, out var convertedArgs);
            var result = (ExtractedContext)tool.Invoke(convertedArgs);
            output = result != null ? new ContextSelection(tool, result) : null;
        }

        public async Task<IContextSelection> RunToolByIDAsync(string id, string[] args)
        {
#if MUSE_INTERNAL
                OnCachedFunctionCalled?.Invoke(id, args);
#endif
            GetSelectorIdAndConvertArgs(id, args, out var tool, out var convertedArgs);
            var result = (ExtractedContext)await tool.InvokeAsync(convertedArgs);
            var output = result != null ? new ContextSelection(tool, result) : null;
            return output;
        }
    }
}
