using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Context.SmartContext;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend;
using Unity.AI.Assistant.Editor.Backend.Socket.ErrorHandling;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Plugins;
using Unity.AI.Assistant.Editor.Utils;
using Unity.Ai.Assistant.Protocol.Client;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant : IAssistantProvider
    {
        public const string k_UserRole = "user";
        public const string k_AssistantRole = "assistant";
        public const string k_SystemRole = "system";

        static float s_LastRefreshTokenTime;

        IAssistantBackend m_Backend;
        public IFunctionCaller FunctionCaller { get; private set; }

#pragma warning disable CS0067 // Event is never used
        public event Action<string, bool> OnConnectionChanged;
#pragma warning restore CS0067

        async Task<CredentialsContext> GetCredentialsContext(CancellationToken ct = default)
        {
            var orgId = await GetOrganizationIdAsync(ct);

            return new(GetAccessToken(), orgId);
        }

        public event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;

        public bool SessionStatusTrackingEnabled => m_Backend == null || m_Backend.SessionStatusTrackingEnabled;

        public void InitializeDriver(IAssistantBackend backend, IFunctionCaller functionCaller = null)
        {
            m_Backend = backend;
            FunctionCaller = functionCaller ?? new AIAssistantFunctionCaller();
            ServerCompatibility.ServerCompatibility.SetBackend(backend);
        }

        AssistantMessage AddInternalMessage(AssistantConversation conversation, string text, string role = null, bool musing = true, bool sendUpdate = true)
        {
            var message = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextInternalId(conversation.Id),
                IsComplete = true,
                Content = text,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            conversation.Messages.Add(message);

            if (sendUpdate)
            {
                NotifyConversationChange(conversation);
            }

            return message;
        }

        AssistantMessage AddIncompleteMessage(AssistantConversation conversation, string text, string role = null, bool musing = true, bool sendUpdate = true)
        {
            var message = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextIncompleteId(conversation.Id),
                IsComplete = false,
                Content = text,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            conversation.Messages.Add(message);
            if (sendUpdate)
            {
                NotifyConversationChange(conversation);
            }

            return message;
        }


        public async Task SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote)
        {
            var feedback = new MessageFeedback
            {
                MessageId = messageId,
                FlagInappropriate = flagMessage,
                Type = Category.ResponseQuality,
                Message = feedbackText,
                Sentiment = upVote ? Sentiment.Positive : Sentiment.Negative
            };

            // Failing to send feedback is non-critical. UX for failures here can be improved in a QOL pass if necessary.
            BackendResult result = await m_Backend.SendFeedback(await GetCredentialsContext(), messageId.ConversationId.Value, feedback);

            if(result.Status != BackendResult.ResultStatus.Success)
                ErrorHandlingUtility.InternalLogBackendResult(result);
        }

        public async Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default)
        {
            if (!messageId.ConversationId.IsValid || messageId.Type != AssistantMessageIdType.External)
            {
                // Whatever we are asking for is not valid to be asked for
                return null;
            }

            var result =  await m_Backend.LoadFeedback(await GetCredentialsContext(ct), messageId, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                // if feedback fails to load, silently fail
                ErrorHandlingUtility.InternalLogBackendResult(result);
                return null;
            }

            FeedbackLoaded?.Invoke(messageId, result.Value);

            return result.Value;
        }

        public static void FixBASST266(AssistantMessage message)
        {
            // https://jira.unity3d.com/browse/BASST-266 is an important bug that should be solved by the server. Our
            // open-beta requires it though, and the server side solution should be designed correctly. So, as temporary
            // fix, we will strip the attribution on the frontend and put it in the correct place.

            // This solution is extremely hardcoded, and assumes the `<sub>Powered by` will be present on the line of
            // the attribution. It will strip this line out, and forward it to the sources visualizer so that it can
            // render the line instead.
            StringBuilder messageWithoutSourceAttribution = new();
            string sourceAttribution = string.Empty;

            foreach (string line in message.Content.Split("\n"))
            {
                if (line.Contains("<sub>Powered by"))
                {
                    sourceAttribution = line;
                    continue;
                }

                messageWithoutSourceAttribution.Append($"{line}\n");
            }

            message.SourceAttribution = sourceAttribution;
            message.Content = messageWithoutSourceAttribution.ToString();
        }
    }
}
