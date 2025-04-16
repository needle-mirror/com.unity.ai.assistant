using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Plugins;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        public IEnumerable<IAssistantProvider.RegisteredPlugin> GetRegisteredPlugins()
        {
            return SystemToolboxes.PluginToolbox.Tools.Select(tool => new IAssistantProvider.RegisteredPlugin()
            {
                FunctionId = tool.FunctionDefinition.FunctionId,
                BlockText = tool.MetaData[PluginToolbox.BlockTextMetadataKey],
                ButtonText = tool.MetaData[PluginToolbox.ButtonTextMetadataKey],
                PromptText = tool.MetaData[PluginToolbox.PromptTextMetadataKey],
            });
        }
    }
}
