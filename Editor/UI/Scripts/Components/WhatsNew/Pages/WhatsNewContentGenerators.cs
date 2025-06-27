using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Plugins;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
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
        const string k_GeneratorsPackageName = "com.unity.ai.generators";

        public override string Title => "Generators";
        public override string Description => "Generate Sprite, Material, Animation, and Sound Assets.";

        IEnumerable<PluginFunction> m_PluginFunctions;

        TemplateContainer m_View;
        ListRequest m_ListRequest;
        List<Button> m_GeneratorButtons = new();
        List<Button> m_InstallButtons = new();

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            RegisterPage(view.Q<VisualElement>("page1"), "Generators1 - NewSprite");
            RegisterPage(view.Q<VisualElement>("page2"), "Generators2 - Material");
            RegisterPage(view.Q<VisualElement>("page3"), "Generators3 - Sprite");
            RegisterPage(view.Q<VisualElement>("page4"), "Generators4 - Animate");
            RegisterPage(view.Q<VisualElement>("page5"), "Generators5 - Sound");

            m_View = view;

            CheckGeneratorsPackage();
        }

        void CheckGeneratorsPackage()
        {
            m_ListRequest = Client.List();
            EditorApplication.update += WaitForPackageList;
        }

        void WaitForPackageList()
        {
            if (m_ListRequest.IsCompleted)
            {
                if (m_ListRequest.Status == StatusCode.Success)
                {
                    bool areGeneratorsInstalled = false;

                    foreach (var package in m_ListRequest.Result)
                    {
                        if (package.name == k_GeneratorsPackageName)
                        {
                            areGeneratorsInstalled = true;
                            break;
                        }
                    }

                    SetupButtons(areGeneratorsInstalled);
                }
                else if (m_ListRequest.Status == StatusCode.Failure)
                {
                    SetupButtons(true);
                }

                EditorApplication.update -= WaitForPackageList;
            }
            else
            {
                if (m_ListRequest.Error != null && !string.IsNullOrEmpty(m_ListRequest.Error.message))
                {
                    SetupButtons(true);

                    EditorApplication.update -= WaitForPackageList;
                }
            }
        }

        void SetupButtons(bool generatorsExist)
        {
            m_GeneratorButtons.Add(m_View.SetupButton("generateMaterialButton", OnOpenMaterialGenerator));
            m_GeneratorButtons.Add(m_View.SetupButton("generateSpriteButton", OnOpenSpriteGenerator));
            m_GeneratorButtons.Add(m_View.SetupButton("generateAnimationButton", OnOpenAnimationGenerator));
            m_GeneratorButtons.Add(m_View.SetupButton("generateAudioButton", OnOpenAudioGenerator));

            m_InstallButtons.Add(m_View.SetupButton("installGeneratorsPage2", OnInstallGenerators));
            m_InstallButtons.Add(m_View.SetupButton("installGeneratorsPage3", OnInstallGenerators));
            m_InstallButtons.Add(m_View.SetupButton("installGeneratorsPage4", OnInstallGenerators));
            m_InstallButtons.Add(m_View.SetupButton("installGeneratorsPage5", OnInstallGenerators));

            // If generators exist show buttons to open them, otherwise hide, and show install buttons instead
            foreach (var button in m_GeneratorButtons)
            {
                button?.SetDisplay(generatorsExist);
            }
            foreach (var button in m_InstallButtons)
            {
                button?.SetDisplay(!generatorsExist);
            }
        }

        void OnInstallGenerators(PointerUpEvent evt)
        {
            // Disable as feedback, and to avoid clicking while asynchronously install is running
            foreach (var button in m_InstallButtons)
            {
                button?.SetEnabled(false);
            }

            Debug.Log("Install AI Packages.\n" + k_GeneratorsPackageName);
            Client.Add(k_GeneratorsPackageName);
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
