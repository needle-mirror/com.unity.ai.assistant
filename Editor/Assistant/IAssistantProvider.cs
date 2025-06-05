using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.CodeAnalyze;
using Unity.AI.Assistant.Editor.Agent;
using Unity.AI.Assistant.Editor.Backend;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;

namespace Unity.AI.Assistant.Editor
{
    internal interface IAssistantProvider
    {
        // Callbacks
        event Action<string, bool> OnConnectionChanged;
        event Action<IEnumerable<AssistantConversationInfo>> ConversationsRefreshed;
        event Action<AssistantConversationId, Assistant.PromptState> PromptStateChanged;
        event Action<AssistantConversation> ConversationLoaded;
        event Action<AssistantConversation> ConversationChanged;
        event Action<AssistantConversation> ConversationCreated;
        event Action<AssistantConversationId> ConversationDeleted;
        event Action<IEnumerable<AssistantInspiration>> InspirationsRefreshed;

        /// <summary>
        /// Invoked when an error occurs during an active conversation. If this is invoked and the conversation is
        /// active, this error indicates that conversation has stopped. All errors are critical errors and the
        /// conversation will cease to perform work.
        /// </summary>
        event Action<AssistantConversationId, ErrorInfo> ConversationErrorOccured;

        event Action<PointCostRequestId, int> PointCostReceived;

        event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;

        // Methods
        void InitializeDriver(IAssistantBackend backend, IFunctionCaller functionCaller);

        Task RefreshInspirations(CancellationToken ct = default);
        Task ConversationLoad(AssistantConversationId conversationId, CancellationToken ct = default);
        Task ConversationFavoriteToggle(AssistantConversationId conversationId, bool isFavorite);
        Task ConversationDeleteAsync(AssistantConversationId conversationId, CancellationToken ct = default);
        Task ConversationRename(AssistantConversationId conversationId, string newName, CancellationToken ct = default);
        Task RefreshConversationsAsync(CancellationToken ct = default);

        Task PointCostRequest(PointCostRequestId requestId, PointCostRequestData data, CancellationToken ct = default);

        Task ProcessPrompt(AssistantConversationId conversationId, AssistantPrompt prompt, CancellationToken ct = default);
        Task SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote);
        Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default);

        Task SendEditRunCommand(AssistantMessageId messageId, string updatedCode);

        void SuspendConversationRefresh();
        void ResumeConversationRefresh();

        void AbortPrompt(AssistantConversationId conversationId);

        void GetAttachedContextString(AssistantPrompt prompt, ref ContextBuilder contextBuilder, bool stopAtLimit = false);

        // Run Command and Related to it
        bool ValidateCode(string code, out string localFixedCode, out CompilationErrors compilationErrors);
        AgentRunCommand BuildAgentRunCommand(string script, List<UnityEngine.Object> contextAttachments);
        void RunAgentCommand(AssistantConversationId conversationId, AgentRunCommand command, string fencedTag);
        ExecutionResult GetRunCommandExecution(int executionId);

        // Function Calling
        IFunctionCaller FunctionCaller { get; }

        IEnumerable<RegisteredPlugin> GetRegisteredPlugins();

        class RegisteredPlugin
        {
            public string FunctionId;
            public string ButtonText;
            public string BlockText;
            public string PromptText;
        }
    }
}
