using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.WhatsNew.Pages
{
    class WhatsNewContentInferenceEngine : WhatsNewContent
    {
        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            RegisterPage(view.Q<VisualElement>("page1"), "InferenceEngine1");
            RegisterPage(view.Q<VisualElement>("page2"), "InferenceEngine2");
            RegisterPage(view.Q<VisualElement>("page3"), "InferenceEngine3");
        }

        public override string Title => "Inference Engine";
        public override string Description => "Integrate AI models running on your local machine in the Unity Editor or end-user runtime app.";
    }
}
