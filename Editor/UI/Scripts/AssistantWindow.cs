using System;
using System.IO;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    class AssistantWindow : EditorWindow, IAssistantHostWindow
    {
        const string k_WindowName = "Assistant";

        static Vector2 k_MinSize = new(400, 400);

        internal AssistantUIContext m_Context;
        internal AssistantView m_View;

        public Action FocusLost { get; set; }

        [MenuItem("Window/AI/Assistant")]
        public static AssistantWindow ShowWindow()
        {
            var editor = GetWindow<AssistantWindow>();

            editor.Show();
            editor.minSize = k_MinSize;

            return editor;
        }

        void CreateGUI()
        {
            var iconPath =
                EditorGUIUtility.isProSkin
                    ? "Sparkle.png"
                    : "Sparkle_dark.png";

            var path = Path.Combine(AssistantUIConstants.BasePath, AssistantUIConstants.UIEditorPath,
                AssistantUIConstants.AssetFolder, "icons", iconPath);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            titleContent = new GUIContent(k_WindowName, icon);

            // Create and initialize a context for this window, will be unique for every active set of assistant UI / elements
            m_Context = new AssistantUIContext();

            m_View = new AssistantView(this);

            m_View.Initialize(m_Context);
            m_View.style.flexGrow = 1;
            m_View.style.minWidth = 400;
            rootVisualElement.Add(m_View);

            m_View.InitializeThemeAndStyle();
        }

        void OnDestroy()
        {
            m_View?.Deinit();
        }

        void OnLostFocus()
        {
            FocusLost?.Invoke();
        }
    }
}
