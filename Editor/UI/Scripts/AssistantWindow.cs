using System;
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

        [SerializeField]
        bool m_IsRestoredWindow;

        [MenuItem("Window/AI/Assistant")]
        public static AssistantWindow ShowWindow()
        {
            var editor = GetWindow<AssistantWindow>();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssistantUIConstants.BasePath + AssistantUIConstants.UIEditorPath + AssistantUIConstants.AssetFolder + "icons/Sparkle.png");
            editor.titleContent = new GUIContent(k_WindowName, icon);
            editor.Show();
            editor.minSize = k_MinSize;

            return editor;
        }

        void CreateGUI()
        {
            // Create and initialize a context for this window, will be unique for every active set of assistant UI / elements
            m_Context = new AssistantUIContext();

            m_View = new AssistantView(this);

            if (!m_IsRestoredWindow)
            {
                m_View.ClearPersistentState();
            }

            m_View.Initialize(m_Context);
            m_View.style.flexGrow = 1;
            m_View.style.minWidth = 400;
            rootVisualElement.Add(m_View);

            m_View.InitializeThemeAndStyle();

            m_IsRestoredWindow = true;
        }

        void OnDestroy()
        {
            m_View?.StorePersistentState();
            m_View?.Deinit();
        }

        void OnLostFocus()
        {
            FocusLost?.Invoke();
        }
    }
}
