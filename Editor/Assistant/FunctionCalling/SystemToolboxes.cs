using Unity.AI.Assistant.Editor.Context.CommandContext;
using Unity.AI.Assistant.Editor.Context.SmartContext;
using Unity.AI.Assistant.Editor.Plugins;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    /// <summary>
    /// The AIAssistantToolboxCollector collects functions on the system that can be called via function calling and
    /// arranges them into toolboxes. Between instances of AI Assistant, toolboxes never differ from one another. They
    /// can be instantiated statically and accessible anywhere.
    /// </summary>
    static class SystemToolboxes
    {
        public static FunctionCache FunctionCache { get; } = new(new AttributeBasedFunctionSource());

        static SmartContextToolbox _smartContextToolbox;

        public static SmartContextToolbox SmartContextToolbox
        {
            get
            {
                if(_smartContextToolbox == null)
                    InitializeState();

                return _smartContextToolbox;
            }
        }

        static PluginToolbox _pluginToolbox;
        public static PluginToolbox PluginToolbox {
            get
            {
                if(_pluginToolbox == null)
                    InitializeState();

                return _pluginToolbox;
            }
        }

        static CommandContextToolbox _commandToolbox;
        public static CommandContextToolbox CommandToolbox{
            get
            {
                if(_commandToolbox == null)
                    InitializeState();

                return _commandToolbox;
            }
        }

        public static void InitializeState()
        {
            _pluginToolbox = new PluginToolbox(FunctionCache);

            _smartContextToolbox = new SmartContextToolbox(FunctionCache);
            _commandToolbox = new CommandContextToolbox(FunctionCache);
        }
    }
}
