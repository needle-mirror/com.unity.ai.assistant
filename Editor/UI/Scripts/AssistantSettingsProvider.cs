using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal static class AssistantSettingsProvider
    {
        const string k_SettingsPrefix = "AIAssistant.";
        public const string k_SendPromptModifierKey = k_SettingsPrefix + "SendPromptUseModifierKey";

        [SettingsProvider]
        public static SettingsProvider CreateAISettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/AI/Assistant", SettingsScope.User)
            {
                label = "Assistant",
                activateHandler = (searchContext, rootElement) =>
                {
                    var page = new AssistantSettingsPage();
                    page.Initialize(null);

                    rootElement.Add(page);
                }
            };

            return provider;
        }
    }
}
