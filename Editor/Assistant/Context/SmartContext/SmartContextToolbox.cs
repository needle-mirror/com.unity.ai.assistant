using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Context.SmartContext
{
    partial class SmartContextToolbox : FunctionToolbox
    {
        internal static int SmartContextLimit { get; set; }

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
        /// <param name="maxContextLength">Context character limit</param>
        /// <param name="output">Output from the tool function</param>
        public bool TryRunToolByName(string name, string[] args, int maxContextLength, out IContextSelection output)
        {
            SmartContextLimit = maxContextLength;
            try
            {
                if (TryGetSelectorAndConvertArgs(name, args, out var tool, out var convertedArgs))
                {
                    var result = (ExtractedContext)tool.Invoke(convertedArgs);
                    output = result != null ? new ContextSelection(tool, result) : null;

                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            output = default;
            return false;
        }

        public bool TryRunToolByID(string id, string[] args, int maxContextLength, out IContextSelection output)
        {
            SmartContextLimit = maxContextLength;
            try
            {
                if (TryGetSelectorIdAndConvertArgs(id, args, out var tool, out var convertedArgs))
                {
                    var result = (ExtractedContext)tool.Invoke(convertedArgs);
                    output = result != null ? new ContextSelection(tool, result) : null;

                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            output = default;
            return false;
        }

        public async Task<(bool, IContextSelection)> TryRunToolByIDAsync(string id, string[] args, int maxContextLength)
        {
            IContextSelection output = default;
            SmartContextLimit = maxContextLength;
            try
            {
                if (TryGetSelectorIdAndConvertArgs(id, args, out var tool, out var convertedArgs))
                {
                    var result = (ExtractedContext)await tool.InvokeAsync(convertedArgs);
                    output = result != null ? new ContextSelection(tool, result) : null;

                    // TODO: Null results are reported as errors. Null results are not always errors, they can mean that an object could not be found for example. https://jira.unity3d.com/browse/ASST-778
                    return (output != null, output);
                }
            }
            catch (Exception e)
            {
                InternalLog.LogException(e);
            }

            return (false, default);
        }
    }
}
