using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Toolkit.Chat;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.WhatsNew.Pages
{
    class PluginFunction
    {
        public MethodInfo Method;

        public void Invoke(object[] parameters)
        {
            if (Method == null)
            {
                InternalLog.LogError("Trying to invoke a null function!");
                return;
            }

            var isAsync = Method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;

            if (isAsync)
            {
                InternalLog.LogWarning($"{Method.Name} is an async function - call it through InvokeAsync.  Skipping.");
                return;
            }

            Method.Invoke(null, parameters);
        }
    }

    class WhatsNewContentGenerators : WhatsNewContent
    {
        const string k_VideoFile = "Video 3 - Replace with Attached Object";

        public override string Title => "Generators";
        public override string Description => "Create textures, sprites, animations, and audio in the style of your production";

        IEnumerable<PluginFunction> m_PluginFunctions;

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

        public override void Initialize(AssistantUIContext context, bool autoShowControl = true)
        {
            base.Initialize(context, autoShowControl);

            InitPluginFunctions();
        }

        void OnOpenAudioGenerator(PointerUpEvent evt)
        {
            InvokeGeneratorFunction("GenerateSound");
        }

        void OnOpenAnimationGenerator(PointerUpEvent evt)
        {
            InvokeGeneratorFunction("GenerateAnimation");
        }

        void OnOpenSpriteGenerator(PointerUpEvent evt)
        {
            InvokeGeneratorFunction("GenerateSprite");
        }

        void OnOpenMaterialGenerator(PointerUpEvent evt)
        {
            InvokeGeneratorFunction("GenerateMaterial");
        }

        void OnOpenTextureGenerator(PointerUpEvent evt)
        {
            InvokeGeneratorFunction("GenerateTexture");
        }

        public void InitPluginFunctions()
        {
            m_PluginFunctions = CacheGeneratorFunctions();

            if (m_PluginFunctions == null || !m_PluginFunctions.Any())
            {
                InternalLog.LogWarning("No generator functions found.");
            }
        }

        void InvokeGeneratorFunction(string functionName)
        {
            foreach (var function in m_PluginFunctions)
            {
                if (function.Method.Name == functionName)
                {
                    function.Invoke( new object[] { "" });
                    return;
                }
            }
        }

        internal static IEnumerable<PluginFunction> CacheGeneratorFunctions()
        {
            return TypeCache.GetMethodsWithAttribute<PluginAttribute>()
                .Where(methodInfo =>
                {
                    if (!methodInfo.IsStatic || methodInfo.ReturnType != typeof(void))
                    {
                        InternalLog.LogWarning(
                            $"Method \"{methodInfo.Name}\" in \"{methodInfo.DeclaringType?.FullName}\" failed" +
                            $"validation. This means it does not have the appropriate function signature for" +
                            $"the given attribute {typeof(PluginAttribute).Name}");
                        return false;
                    }

                    return true;
                })
                .Where(method => method.GetCustomAttribute<PluginAttribute>() != null)
                .Select(method => new PluginFunction { Method = method });
        }
    }
}
