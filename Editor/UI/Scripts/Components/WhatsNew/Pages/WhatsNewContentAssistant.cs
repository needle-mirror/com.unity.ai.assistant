using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.WhatsNew.Pages
{
    class WhatsNewContentAssistant : WhatsNewContent
    {
        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            view.SetupButton("openAssistantButton", OnOpenAssistant);

            RegisterPage(view.Q<VisualElement>("page1"), "Assistant1 - SlashCommand");
            RegisterPage(view.Q<VisualElement>("page2"), "Assistant2 - TroubleshootingConsole");
            RegisterPage(view.Q<VisualElement>("page3"), "Assistant3 - RunCommands");
            RegisterPage(view.Q<VisualElement>("page4"), "Assistant4 - ModifyScript");
        }

        public override string Title => "Assistant";
        public override string Description => "Boost productivity by automating tasks and unblocking obstacles.";

        void OnOpenAssistant(PointerUpEvent evt)
        {
            AssistantWindow.ShowWindow();
        }
    }
}
