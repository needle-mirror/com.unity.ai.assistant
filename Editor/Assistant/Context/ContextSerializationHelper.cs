using System.Collections.Generic;
using Unity.AI.Assistant.Bridge;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.Context
{
    internal class ContextSerializationHelper
    {
        internal static AssistantContextList BuildPromptSelectionContext(List<UnityEngine.Object> objectAttachments, List<LogData> consoleAttachments)
        {
            // workaround to allow serialization of entire list - not just individual items
            var serializableContext = new AssistantContextList();
            var result = serializableContext.m_ContextList;

            foreach (var attachment in objectAttachments)
            {
                var entry = attachment.GetContextEntry();
                result.Add(entry);
            }

            foreach (var entry in consoleAttachments)
            {
                result.Add(new AssistantContextEntry
                {
                    Value = entry.Message,
                    EntryType = AssistantContextType.ConsoleMessage,
                    ValueType = entry.Type.ToString()
                });
            }

            return serializableContext;
        }
    }
}
