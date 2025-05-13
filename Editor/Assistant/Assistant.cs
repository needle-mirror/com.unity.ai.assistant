using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using FunctionCall = Unity.AI.Assistant.Editor.ApplicationModels.FunctionCall;
using SmartContextResponse = Unity.AI.Assistant.Editor.ApplicationModels.SmartContextResponse;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant : IAssistantProvider
    {
        public const string k_UserRole = "user";
        public const string k_AssistantRole = "assistant";
        public const string k_SystemRole = "system";

        static float s_LastRefreshTokenTime;

        readonly List<IStreamStatusHook> k_MessageUpdaters = new();
        public int MessageUpdatersCount => k_MessageUpdaters.Count;

        IAssistantBackend m_Backend;
        public IFunctionCaller FunctionCaller { get; private set; }

#pragma warning disable CS0067 // Event is never used
        public event Action<string, bool> OnConnectionChanged;
#pragma warning restore CS0067

#if MUSE_INTERNAL
        internal event Action<TimeSpan, SmartContextResponse> OnSmartContextCallDone;
        internal event Action<TimeSpan, FunctionCall> OnSmartContextExtracted;
        internal event Action<AssistantConversation> OnFinalResponseReceived;
        internal bool IsProcessingConversations => k_MessageUpdaters.Count > 0;
        internal bool SkipChatCall = false; // Used for benchmarking to skip the actual chat call and only call smart context.
#endif

        async Task<CredentialsContext> GetCredentialsContext(CancellationToken ct = default)
        {
            var orgId = await GetOrganizationIdAsync(ct);

            return new(GetAccessToken(), orgId);
        }

        public event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;

        public bool SessionStatusTrackingEnabled => m_Backend == null || m_Backend.SessionStatusTrackingEnabled;

        public List<AssistantConversationId> GetProcessingConversations()
        {
            if (k_MessageUpdaters.Count == 0)
            {
                return null;
            }

            return k_MessageUpdaters.Select(x => new AssistantConversationId(x.ConversationId)).ToList();
        }

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

        /// <summary>
        /// Checks if there are message updaters with an internal conversation id.
        /// </summary>
        /// <returns>True if there is an updater with an internal ID.</returns>
        bool HasInternalIdUpdaters()
        {
            for (var i = 0; i < k_MessageUpdaters.Count; i++)
            {
                var updater = k_MessageUpdaters[i];
                if (!new AssistantConversationId(updater.ConversationId).IsValid)
                {
                    return true;
                }
            }

            return false;
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
    }
}
