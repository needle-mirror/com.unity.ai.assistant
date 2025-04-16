using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.WhatsNew.Pages
{
    class WhatsNewContentInferenceEngine : WhatsNewContent
    {
        const string k_VideoFile = "Video 3 - Replace with Attached Object";

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            RegisterPage(view.Q<VisualElement>("page1"), k_VideoFile);
            RegisterPage(view.Q<VisualElement>("page2"), k_VideoFile);
            RegisterPage(view.Q<VisualElement>("page3"), k_VideoFile);
        }

        public override string Title => "Inference Engine";
        public override string Description => "Integrate AI models running on your local machine in the Unity Editor or end-user runtime app";
    }
}
