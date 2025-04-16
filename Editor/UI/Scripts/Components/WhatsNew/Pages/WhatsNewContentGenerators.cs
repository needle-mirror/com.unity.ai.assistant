using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.WhatsNew.Pages
{
    class WhatsNewContentGenerators : WhatsNewContent
    {
        const string k_VideoFile = "Video 3 - Replace with Attached Object";

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            view.SetupButton("generateTextureButton", OnOpenTextureGenerator);
            view.SetupButton("generateMaterialButton", OnOpenMaterialGenerator);
            view.SetupButton("generateSpriteButton", OnOpenSpriteGenerator);
            view.SetupButton("generateAnimationButton", OnOpenAnimationGenerator);
            view.SetupButton("generateAudioButton", OnOpenAudioGenerator);

            RegisterPage(view.Q<VisualElement>("page1"), k_VideoFile);
            RegisterPage(view.Q<VisualElement>("page2"), k_VideoFile);
            RegisterPage(view.Q<VisualElement>("page3"), k_VideoFile);
            RegisterPage(view.Q<VisualElement>("page4"), k_VideoFile);
            RegisterPage(view.Q<VisualElement>("page5"), k_VideoFile);
            RegisterPage(view.Q<VisualElement>("page6"), k_VideoFile);
        }

        public override string Title => "Generators";
        public override string Description => "Create textures, sprites, animations, and audio in the style of your production";

        void OnOpenAudioGenerator(PointerUpEvent evt)
        {
            EditorApplication.ExecuteMenuItem("Assets/Create/Audio/Generate Audio Clip");
        }

        void OnOpenAnimationGenerator(PointerUpEvent evt)
        {
            EditorApplication.ExecuteMenuItem("Assets/Create/Animation/Generate Animation Clip");
        }

        void OnOpenSpriteGenerator(PointerUpEvent evt)
        {
        }

        void OnOpenMaterialGenerator(PointerUpEvent evt)
        {
            EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/Generate Material");
        }

        void OnOpenTextureGenerator(PointerUpEvent evt)
        {
            EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/Generate Texture 2D");
        }
    }
}
