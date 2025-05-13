using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AssistantSettingsPage : ManagedTemplate
    {
        const string k_WindowsSendPromptText = "Use <b>Ctrl+Enter</b> to send a prompt";
        const string k_OSXSendPromptText = "Use <b>\u2318Return</b> to send a prompt";

        const string k_WindowsSendPromptTooltip = "Instead of using <b>Enter</b> to send a prompt, you can use <b>Ctrl+Enter</b>";
        const string k_OSXSendPromptTooltip = "Instead of using <b>Return</b> to send a prompt, you can use <b>\u2318Return</b>";

        public AssistantSettingsPage() :
            base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            LoadStyle(view, AssistantUIConstants.AssistantBaseStyle, true);

            var label = view.Q<Label>("promptModifierLabel");
            var toggle = view.Q<Toggle>("promptModifierToggle");

            toggle.value = EditorPrefs.GetBool(AssistantSettingsProvider.k_SendPromptModifierKey, false);

            toggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(AssistantSettingsProvider.k_SendPromptModifierKey, evt.newValue);
            });

            label.text = Application.platform == RuntimePlatform.OSXEditor
                ? k_OSXSendPromptText
                : k_WindowsSendPromptText;

            label.tooltip = Application.platform == RuntimePlatform.OSXEditor
                ? k_OSXSendPromptTooltip
                : k_WindowsSendPromptTooltip;
        }
    }
}
