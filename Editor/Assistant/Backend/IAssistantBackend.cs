using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.ErrorHandling;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.FunctionCalling;
using VersionSupportInfo = Unity.AI.Assistant.Editor.ApplicationModels.VersionSupportInfo;

namespace Unity.AI.Assistant.Editor.Backend
{
    interface IAssistantBackend
    {
        bool SessionStatusTrackingEnabled { get; }
        Task<BackendResult<IEnumerable<ConversationInfo>>> ConversationRefresh(CredentialsContext credentialsContext, CancellationToken ct = default);
        Task<BackendResult<ClientConversation>> ConversationLoad(CredentialsContext credentialsContext, string conversationUid, CancellationToken ct = default);
        Task<BackendResult> ConversationFavoriteToggle(CredentialsContext credentialsContext, string conversationUid, bool isFavorite, CancellationToken ct = default);
        Task<BackendResult> ConversationRename(CredentialsContext credentialsContext, string conversationUid, string newName, CancellationToken ct = default);
        Task<BackendResult> ConversationDelete(CredentialsContext credentialsContext, string conversationUid, CancellationToken ct = default);
        Task<BackendResult<string>> ConversationGenerateTitle(CredentialsContext credentialsContext, string conversationId, CancellationToken ct = default);
        Task<BackendResult<IEnumerable<Inspiration>>> InspirationRefresh(CredentialsContext credentialsContext, CancellationToken ct = default);
        Task<BackendResult> SendFeedback(CredentialsContext credentialsContext, string conversationUid, MessageFeedback feedback, CancellationToken ct = default);
        Task<BackendResult<FeedbackData?>> LoadFeedback(CredentialsContext credentialsContext, AssistantMessageId messageId, CancellationToken ct = default);
        Task<BackendResult<int>> PointCostRequest(CredentialsContext credentialsContext, string conversationUid, int? contextItems, string prompt, CancellationToken ct = default);

        /// <summary>
        /// Returns version support info that can used to check if the version of the server the client wants to
        /// communicate with is supported. Returns null if the version support info could not be retrieved or the
        /// request was cancelled
        /// </summary>
        Task<BackendResult<List<VersionSupportInfo>>> GetVersionSupportInfo(CredentialsContext credentialsContext, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the workflow being used for the most current conversation
        /// </summary>
        ChatWorkflow ActiveWorkflow { get; }

        ChatWorkflow GetOrCreateWorkflow(CredentialsContext credentialsContext, IFunctionCaller caller, AssistantConversationId conversationId = default);
    }
}
