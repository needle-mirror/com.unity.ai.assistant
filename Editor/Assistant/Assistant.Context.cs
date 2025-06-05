using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Utils;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        CancellationTokenSource m_ContextCancelToken;

        /// <summary>
        /// Get the context string from the selected objects and selected console logs.
        /// </summary>
        /// <param name="prompt">The prompt to get the context string for</param>
        /// <param name="contextBuilder"> The context builder reference for temporary context string creation. </param>
        /// <param name="stopAtLimit">Stop processing context once the limit has reached</param>
        /// <returns></returns>
        public void GetAttachedContextString(AssistantPrompt prompt, ref ContextBuilder contextBuilder, bool stopAtLimit = false)
        {
            if (prompt == null)
            {
                return;
            }

            // Grab any selected objects
            var attachment = GetValidAttachment(prompt.ObjectAttachments);
            if (attachment.Count > 0)
            {
                foreach (var currentObject in attachment)
                {
                    var objectContext = new UnityObjectContextSelection();
                    objectContext.SetTarget(currentObject);

                    contextBuilder.InjectContext(objectContext);

                    if (stopAtLimit && contextBuilder.PredictedLength > AssistantMessageSizeConstraints.ContextLimit)
                    {
                        break;
                    }
                }
            }

            // Grab any console logs
            if (prompt.ConsoleAttachments != null)
            {
                foreach (var currentLog in prompt.ConsoleAttachments)
                {
                    var consoleContext = new ConsoleContextSelection();
                    consoleContext.SetTarget(currentLog);
                    contextBuilder.InjectContext(consoleContext);

                    if (stopAtLimit && contextBuilder.PredictedLength > AssistantMessageSizeConstraints.ContextLimit)
                    {
                        break;
                    }
                }
            }
        }

        internal EditorContextReport GetContextModel(int maxLength, AssistantPrompt prompt)
        {
            // Initialize all context, if any context has changed, add it all
            var contextBuilder = new ContextBuilder();
            GetAttachedContextString(prompt, ref contextBuilder);

            var finalContext = contextBuilder.BuildContext(maxLength);

            InternalLog.Log($"Final Context ({contextBuilder.PredictedLength} character):\n\n {finalContext.ToJson()}");

            return finalContext;
        }

        internal List<Object> GetValidAttachment(List<Object> contextAttachments)
        {
            if (contextAttachments == null)
                return new List<Object>();

            if (contextAttachments.Any(obj => obj == null))
                return contextAttachments.Where(obj => obj != null).ToList();

            return contextAttachments;
        }
    }
}
