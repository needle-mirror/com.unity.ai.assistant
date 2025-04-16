using System;
using Unity.AI.Assistant.Editor.Backend;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.FunctionCalling;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Legacy support for a single instance Assistant, to be removed at earliest convenience
    /// </summary>
    internal class AssistantInstance : ScriptableSingleton<AssistantInstance>
    {
        Func<IAssistantBackend> m_BackendInitializer = () => new AssistantWebSocketBackend();

        void OnEnable()
        {
            SystemToolboxes.InitializeState();

            if (Value == null)
            {
                Value = new Assistant();
                Value.InitializeDriver(BackendInitializer());
            }
        }

        public Assistant Value;

        public Func<IAssistantBackend> BackendInitializer
        {
            get => m_BackendInitializer;
            set
            {
                m_BackendInitializer = value;
                if (Value != null)
                {
                    Value.InitializeDriver(m_BackendInitializer());
                }
            }
        }
    }
}
