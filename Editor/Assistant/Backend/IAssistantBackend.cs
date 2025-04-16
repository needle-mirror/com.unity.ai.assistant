using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.FunctionCalling;
using VersionSupportInfo = Unity.AI.Assistant.Editor.ApplicationModels.VersionSupportInfo;

namespace Unity.AI.Assistant.Editor.Backend
{
    interface IAssistantBackend
    {
        Dictionary<string, object> Configuration { get; }

        bool SessionStatusTrackingEnabled { get; }
        Task<IEnumerable<ConversationInfo>> ConversationRefresh(CancellationToken ct = default);
        Task<ClientConversation> ConversationLoad(string conversationUid, CancellationToken ct = default);
        Task ConversationFavoriteToggle(string conversationUid, bool isFavorite, CancellationToken ct = default);
        Task ConversationRename(string conversationUid, string newName, CancellationToken ct = default);
        Task ConversationDelete(string conversationUid, CancellationToken ct = default);
        Task<string> ConversationGenerateTitle(string conversationId, CancellationToken ct = default);
        Task<IEnumerable<Inspiration>> InspirationRefresh(CancellationToken ct = default);
        Task SendFeedback(string conversationUid, MessageFeedback feedback, CancellationToken ct = default);
        Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default);
        Task<int> PointCostRequest(string conversationUid, int? contextItems, string prompt, CancellationToken ct = default);

        /// <summary>
        /// Returns version support info that can used to check if the version of the server the client wants to
        /// communicate with is supported. Returns null if the version support info could not be retrieved.
        /// </summary>
        /// <param name="version">Server version the client wants to hit expressed as the url name. Example: v1</param>
        Task<List<VersionSupportInfo>> GetVersionSupportInfo(string version, CancellationToken ct = default);

        /// <summary>
        /// Try to get the workflow associated with the given conversationId.
        /// </summary>
        /// <param name="conversationId"></param>
        /// <param name="workflow"></param>
        /// <returns></returns>
        bool TryGetWorkflow(string conversationId, out ChatWorkflow workflow);

        ChatWorkflow GetOrCreateWorkflow(IFunctionCaller caller, string conversationId = null);
    }
}
