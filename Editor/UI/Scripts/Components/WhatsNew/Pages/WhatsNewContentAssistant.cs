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

            RegisterPage(view.Q<VisualElement>("page1"), "Video 3 - Replace with Attached Object");
            RegisterPage(view.Q<VisualElement>("page2"), "Video 4 - Mesh to Sphere Colliders");
            RegisterPage(view.Q<VisualElement>("page3"), "Video 4 - Mesh to Sphere Colliders");
            RegisterPage(view.Q<VisualElement>("page4"), "Video 4 - Mesh to Sphere Colliders");
            RegisterPage(view.Q<VisualElement>("page5"));
        }

        public override string Title => "Assistant";
        public override string Description => "Boost productivity by automating tasks and unblocking obstacles";

        void OnOpenAssistant(PointerUpEvent evt)
        {
            AssistantWindow.ShowWindow();
        }
    }
}
