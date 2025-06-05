using System.Collections.Generic;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Utilities
{
    static class OrchestrationDataUtilities
    {
        internal static List<ChatRequestV1.AttachedContextModel> FromEditorContextReport(
            EditorContextReport editorContextReport)
        {
            var contextList = new List<ChatRequestV1.AttachedContextModel>();

            if (editorContextReport?.AttachedContext == null)
                return contextList;

            // Go through each context item
            foreach (var contextItem in editorContextReport.AttachedContext)
            {
                var contextModel = new ChatRequestV1.AttachedContextModel();
                var metaDataModel = new ChatRequestV1.AttachedContextModel.MetadataModel();
                var bodyModel = new ChatRequestV1.AttachedContextModel.BodyModel();

                bodyModel.Type = contextItem.Type;
                bodyModel.Payload = contextItem.Payload;
                bodyModel.Truncated = contextItem.Truncated;

                var selection = contextItem.Context as IContextSelection;
                if (selection == null)
                {
                    InternalLog.LogWarning("Context is not an IContextSelection.");
                    continue;
                }

                // There is technically two more of these, ContextSelection and StaticDatabase
                // They don't show up in these scenarios
                var contextEntry = new AssistantContextEntry();
                switch (selection)
                {
                    case UnityObjectContextSelection objectContext:
                        contextEntry = objectContext.Target.GetContextEntry();
                        break;
                    case ConsoleContextSelection consoleContext:
                        contextEntry = consoleContext.Target.GetValueOrDefault().GetContextEntry();
                        break;
                    default:
                        InternalLog.LogWarning("Context is not attached object or console - skipping.");
                        continue;
                }

                metaDataModel.DisplayValue = contextEntry.DisplayValue;
                metaDataModel.Value = contextEntry.Value;
                metaDataModel.ValueType = contextEntry.ValueType;
                metaDataModel.ValueIndex = contextEntry.ValueIndex;
                metaDataModel.EntryType = (int)contextEntry.EntryType;

                contextModel.Body = bodyModel;
                contextModel.Metadata = metaDataModel;
                contextList.Add(contextModel);
            }

            return contextList;
        }
    }
}
