using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.FunctionCalling;
using UnityEngine;
using static Unity.AI.Assistant.Editor.Context.SmartContext.SmartContextToolbox;

namespace Unity.AI.Assistant.Editor.Context.CommandContext
{
    class CommandContextToolbox : FunctionToolbox
    {
        static string[] GetCommandTags()
        {
            // Get all chat commands
            var allCommands = ChatCommands.GetCommands();

            var tags = allCommands.Select(e => $"command_{e}").ToArray();

            return tags;
        }

        public CommandContextToolbox(FunctionCache functionCache) : base(functionCache, GetCommandTags())
        {

        }

        /// <summary>
        /// Executes a command context tool by name with the given arguments.
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
    }
}
