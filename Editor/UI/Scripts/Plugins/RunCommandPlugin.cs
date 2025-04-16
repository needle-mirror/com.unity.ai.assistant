//#define RUN_COMMAND_PLUGIN_ENABLED

using System.Diagnostics;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Toolkit.Chat;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Plugins
{
    /// <summary>
    /// Static class which exposes Agent to the plugin system.
    /// </summary>
    static class RunCommandPlugin
    {
        /// <summary>
        /// Triggers Agent to conduct in-editor action based on the original user message.
        /// </summary>
        /// <param name="userMessage">The original user message to send to Agent.</param>
        [Plugin("Plugin to trigger Agent to execute in-editor actions based on the original user message. The in-editor actions include manipulating the scene, changing project settings, organizing the project, creating primitive game object, etc.", actionText: "Run", toolName: "Agent", displayText: "Run with Agent")]
        [Conditional("RUN_COMMAND_PLUGIN_ENABLED")]
        static void TriggerRunCommandFromPrompt([Parameter("The original user message to instruct Agent to conduct in-editor action.")] string userMessage)
        {
            object context = FunctionCallingContextBridge.LastPostedContext;

            if (context is AssistantUIContext uiContext)
            {
                userMessage = "/" + ChatCommandType.Run.ToString().ToLower() + " " + userMessage;
                uiContext.API.SendPrompt(userMessage);
            }
        }
    }
}
