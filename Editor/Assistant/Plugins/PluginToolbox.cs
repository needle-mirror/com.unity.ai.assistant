using System;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Plugins
{
    internal partial class PluginToolbox : FunctionToolbox
    {
        const string k_ButtonTitleKey = "button-text";
        const string k_BlockTitleKey = "block-title";

        public const string ButtonTextMetadataKey = "PluginButtonText";
        public const string PromptTextMetadataKey = "PluginPromptText";
        public const string BlockTextMetadataKey = "PluginBlockText";

        public PluginToolbox(FunctionCache functionCache) : base(functionCache, "plugin")
        {
        }

        /// <summary>
        /// Executes a plugin tool by name with the given arguments.
        /// </summary>
        /// <param name="name">Name of the tool function.</param>
        /// <param name="args">Arguments to pass to the tool function.</param>
        public void RunToolByName(string name, string[] args)
        {
            GetSelectorAndConvertArgs(name, args, out var plugin, out var convertedArgs);
            plugin.Invoke(convertedArgs);
        }

        public bool TryGetToolButtonTextByID(string id, out string actionText)
            => TryGetToolMetadataByID(id, k_ButtonTitleKey, out actionText);

        public bool TryGetToolBlockTitleByID(string id, out string actionText)
            => TryGetToolMetadataByID(id, k_BlockTitleKey, out actionText);

        public void RunToolByID(string id, string[] args)
        {
            GetSelectorIdAndConvertArgs(id, args, out var tool, out var convertedArgs);
            tool.Invoke(convertedArgs);
        }
    }
}
