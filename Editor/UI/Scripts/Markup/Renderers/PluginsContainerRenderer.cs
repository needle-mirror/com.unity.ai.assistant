using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdig.Extensions.CustomContainers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Newtonsoft.Json;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Markup.Renderers
{
    class PluginsContainerRenderer : MarkdownObjectRenderer<ChatMarkdownRenderer, CustomContainer>
    {
        AssistantUIContext m_Context;

        public PluginsContainerRenderer(AssistantUIContext context)
        {
            m_Context = context;
        }

        protected override void Write(ChatMarkdownRenderer renderer, CustomContainer obj)
        {
            // plugin contains start with :::plugin
            if (obj.Info == "plugin")
            {
                // Construct the plugin data
                IEnumerable<string> calls = obj
                    .Select(a => a)
                    .Cast<ParagraphBlock>()
                    .SelectMany(p => p.Inline)
                    .Aggregate(new StringBuilder(), (builder, inline) =>
                    {
                        if (inline is LineBreakInline)
                            builder.Append("@!#$");
                        else
                            builder.Append(inline.ToString());
                        return builder;
                    })
                    .ToString()
                    .Split("@!#$");

                // Extract the plugin calls that are valid
                List<PluginCallArguments> parsedCalls = new();

                foreach (string call in calls)
                {
                    try
                    {
                        parsedCalls.Add(JsonConvert.DeserializeObject<PluginCallArguments>(call));
                    }
                    catch (Exception)
                    {
                        // Ignore failures to parse PluginCall. This can happen when calls are partially streamed in
                    }
                }

                // Get registered plugin data from the Assistant instance
                IEnumerable<IAssistantProvider.RegisteredPlugin> registeredPlugins = renderer.GetRegisteredPlugins();

                // Make sure that all plugins that will be rendered actually exist
                List<PluginButtonData> buttons = new();
                foreach (PluginCallArguments arguments in parsedCalls)
                {
                    IAssistantProvider.RegisteredPlugin registeredPlugin
                        = registeredPlugins.FirstOrDefault(plug => plug.FunctionId == arguments.FunctionId);

                    if (registeredPlugin == null)
                    {
                        InternalLog.LogError($"Tried to load plugin with id:{arguments.FunctionId} but this plugin " +
                                             $"has not been registered to the system");
                    }
                    else
                    {
                        buttons.Add(new PluginButtonData
                        {
                            CallArguments = arguments,
                            RegisteredPlugin = registeredPlugin
                        });
                    }
                }

                // Group by the Block text, so that a block can be rendered per plugin type
                IEnumerable<IGrouping<string, PluginButtonData>> blocks = buttons.GroupBy(button => button.RegisteredPlugin.BlockText);

                foreach (IGrouping<string,PluginButtonData> block in blocks)
                {
                    ChatElementPluginBlock pluginBlock = new();
                    pluginBlock.Initialize(m_Context);
                    pluginBlock.SetData(block.Key, block.ToArray());
                    renderer.m_OutputTextElements.Add(pluginBlock);
                }
            }
        }
    }
}
