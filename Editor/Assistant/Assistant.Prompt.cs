using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.Backend.Socket.ErrorHandling;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.Data;
using UnityEditor;
using UnityEngine;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        readonly IDictionary<AssistantConversationId, AssistantConversation> m_ConversationCache =
            new Dictionary<AssistantConversationId, AssistantConversation>();

        public enum PromptState
        {
            NotConnected,
            Connecting,
            Connected,
            AwaitingServer,
            AwaitingClient,
            Canceling
        }

        internal PromptState CurrentPromptState { get; private set; }

        // TODO: this only exists to support the ObjectDataExtractor tool
        internal readonly Dictionary<string, AssistantPrompt> k_TEMP_ActivePromptMap = new();

        public event Action<AssistantConversationId, PromptState> PromptStateChanged;

        CancellationTokenSource m_ConnectionCancelToken;

        class PromptContext
        {
            public CredentialsContext Credentials;

            public AssistantContextEntry[] Asset;

            public List<ChatRequestV1.AttachedContextModel> Attached;
        }

        void ChangePromptState(AssistantConversationId conversationId, PromptState newState, string message)
        {
            if (CurrentPromptState == newState)
            {
                return;
            }
            InternalLog.Log($"Changing state from {CurrentPromptState} to {newState} because {message}");
            CurrentPromptState = newState;
            PromptStateChanged?.Invoke(conversationId, newState);
        }

        public void AbortPrompt(AssistantConversationId conversationId)
        {
            if (CurrentPromptState is PromptState.Canceling or PromptState.NotConnected)
            {
                InternalLog.LogWarning($"AbortPrompt: Ignored in state {CurrentPromptState}");
                return;
            }

            m_ContextCancelToken?.Cancel();
            m_ConnectionCancelToken?.Cancel();

            // Orchestration uses workflows to manage the connection to the backend rather than the stream object. When
            // orchestration is the only system, the stream objects will be removed.
            if (m_Backend is AssistantWebSocketBackend webSocketBackend)
            {
                var workflow = webSocketBackend.ActiveWorkflow;
                if (workflow != null && workflow.ConversationId == conversationId.Value)
                    workflow.CancelCurrentChatRequest();

                webSocketBackend.ForceDisconnectWorkflow(conversationId.Value);
                ChangePromptState(conversationId, PromptState.NotConnected, "User cancelled the prompt. Disconnected workflow instantly.");
            }
        }

        string GetAccessToken()
        {
            return CloudProjectSettings.accessToken;
        }

        async Task<string> GetOrganizationIdAsync(CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested &&
                   string.IsNullOrWhiteSpace(CloudProjectSettings.organizationKey))
            {
                await Task.Yield();
            }

            return CloudProjectSettings.organizationKey;
        }

        public async Task ProcessPrompt(
            AssistantConversationId conversationId,
            AssistantPrompt prompt,
            CancellationToken ct = default)
        {
            // Warm up ScriptableSingleton from main thread, or it
            // will throw exceptions later when we access it, and it initializes itself from a thread later on:
            var _ = AssistantEnvironment.WebSocketApiUrl;

            var promptContext = new PromptContext { Credentials = await GetCredentialsContext(ct) };

            // Prepare serialized context, this needs to be on the main thread for asset db checks:
            promptContext.Asset = ContextSerializationHelper
                .BuildPromptSelectionContext(prompt.ObjectAttachments, prompt.ConsoleAttachments).m_ContextList
                .ToArray();

            // Ensure the prompt adheres to the size constraints
            if (prompt.Value.Length > AssistantMessageSizeConstraints.PromptLimit)
            {
                prompt.Value = prompt.Value.Substring(0, AssistantMessageSizeConstraints.PromptLimit);
            }

            var maxMessageSize = AssistantMessageSizeConstraints.GetMessageSizeLimitForV1Request();
            var maxContextSize = Mathf.Max(0, maxMessageSize - prompt.Value.Length);
            var attachedContext = GetContextModel(maxContextSize, prompt);
            promptContext.Attached = OrchestrationDataUtilities.FromEditorContextReport(attachedContext);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() =>
            {
                ProcessPromptInternal(conversationId, prompt, promptContext, ct).WithExceptionLogging();
            });
#pragma warning restore CS4014
        }

        private async Task ProcessPromptInternal(
            AssistantConversationId conversationId,
            AssistantPrompt prompt,
            PromptContext promptContext,
            CancellationToken ct = default)
        {
            m_ConnectionCancelToken = new();
            var connectionCancelToken = m_ConnectionCancelToken.Token;

            // Create a temporary mapping to allow one of the function calls to work
            k_TEMP_ActivePromptMap.TryAdd(prompt.Value, prompt);

            // get the appropriate workflow
            bool isNewConversation = !conversationId.IsValid;

            var workflow = m_Backend.GetOrCreateWorkflow(promptContext.Credentials, FunctionCaller, conversationId);
            workflow.OnWorkflowStateChanged += OnWorkflowStateChange;
            OnWorkflowStateChange(workflow.WorkflowState);

            await workflow.AwaitDiscussionInitialization();

            // If the user has cancelled the prompt, then treat this as an early-out
            if (CurrentPromptState == PromptState.Canceling)
            {
                InternalLog.LogWarning("ProcessPrompt: Early out due to user cancellation");
                return;
            }

            if (workflow.IsCancelled)
            {
                InternalLog.Log("ProcessPrompt: Early out due to workflow cancellation");
                return;
            }

            // if the workflow was closed at any point during it discussion initialization process, this means something
            // went wrong. Either a timeout, or bad internet connection. This is only relevant to the user if they did
            // not cancel by this point.
            if (workflow.WorkflowState == State.Closed)
            {
                ConversationErrorOccured?.Invoke(conversationId, new($"We were unable to establish communication with the AI Assistant server. {ErrorHandlingUtility.ErrorMessageNetworkedSuffix}", workflow.CloseReason.ToString()));
                ChangePromptState(conversationId, PromptState.NotConnected, "Unable to establish communication with the AI Assistant server.");
                return;
            }

            // Create the objects used by the UI code to render the conversation
            conversationId = new AssistantConversationId(workflow.ConversationId);

            if (!m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                conversation = new AssistantConversation
                {
                    Title = "New conversation",
                    Id = conversationId
                };

                m_ConversationCache.Add(conversationId, conversation);
            }

            // Add the messages needed to start rendering the response
            var promptMessage = AddInternalMessage(conversation, prompt.Value, role: k_UserRole, sendUpdate: true);
            promptMessage.Context = promptContext.Asset;

            var assistantMessage = AddIncompleteMessage(conversation, string.Empty, k_AssistantRole, sendUpdate: false);

            if (isNewConversation)
                ConversationCreated?.Invoke(conversation);

            // Make the progress bar indicate musing

            // listen to the appropriate events from the workflow
            StringBuilder assistantResponseStringBuilder = new();

            workflow.OnChatResponse -= HandleChatResponse;
            workflow.OnChatResponse += HandleChatResponse;

            workflow.OnAcknowledgeChat -= HandleChatAcknowledgment;
            workflow.OnAcknowledgeChat += HandleChatAcknowledgment;

            workflow.OnClose -= HandleClose;
            workflow.OnClose += HandleClose;

            var originalPrompt = prompt.Value;

            // Send the prompt to start the process
            var command = ChatCommandParser.IsCommand(prompt.Value)
                ? ChatCommandParser.Parse(prompt)
                : AskCommand.k_CommandName;

            // Report the user message before the prefix/command is removed
            TaskUtils.DispatchToMainThread(() =>
                AIAssistantAnalytics.ReportSendUserMessageEvent(originalPrompt, command,
                    conversationId.Value));

            await workflow.SendChatRequest($"/{command} {prompt.Value}", promptContext.Attached, ct).WithExceptionLogging();

            return;

            void HandleClose(CloseReason reason)
            {
                if (reason.Reason != CloseReason.ReasonType.ServerDisconnected)
                {
                    string message = $"Something went wrong. {ErrorHandlingUtility.ErrorMessageNetworkedSuffix}";

                    // Only send the message if we did not cancel
                    if (!connectionCancelToken.IsCancellationRequested)
                        ConversationErrorOccured?.Invoke(conversationId, new ErrorInfo(message, reason.ToString()));
                }
            }

            void HandleChatAcknowledgment(AcknowledgePromptInfo info)
            {
                promptMessage.Id =
                    new AssistantMessageId(conversation.Id, info.Id, AssistantMessageIdType.External);
                promptMessage.Context = info.Context;
                promptMessage.Content = info.Content;

                NotifyConversationChange(conversation);
            }

            void HandleChatResponse(ChatResponseFragment fragment)
            {
                assistantResponseStringBuilder.Append(fragment.Fragment);

                if(assistantMessage.Id.FragmentId != fragment.Id)
                    assistantMessage.Id = new AssistantMessageId(conversation.Id, fragment.Id, AssistantMessageIdType.External);

                assistantMessage.Content = assistantResponseStringBuilder.ToString();
                assistantMessage.IsComplete = fragment.IsLastFragment;

                if (fragment.IsLastFragment)
                {
                    assistantMessage.IsComplete = true;
                    assistantMessage.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    assistantMessage.MessageIndex = conversation.Messages.Count - 1;

                    CleanupEvents();

                    FixBASST266(assistantMessage);

                    if (isNewConversation)
                    {
                        // TODO: Remove this dispatch when REST is replaced or changed to HttpClient that can be in background threads.
                        // Need to dispatch to main thread for now because ConversationGenerateTitle creates a UnityWebRequest.
                        TaskUtils.DispatchToMainThread(async () =>
                        {
                            var titleResult = await m_Backend.ConversationGenerateTitle(promptContext.Credentials,
                                workflow.ConversationId, ct);

                            // Silently fail when the title fails to generate
                            if (titleResult.Status != BackendResult.ResultStatus.Success)
                            {
                                ErrorHandlingUtility.InternalLogBackendResult(titleResult);
                                return;
                            }

                            conversation.Title = titleResult.Value;
                            NotifyConversationChange(conversation);
                        });
                    }
                }

                NotifyConversationChange(conversation);
            }

            void CleanupEvents()
            {
                workflow.OnClose -= HandleClose;
                workflow.OnChatResponse -= HandleChatResponse;
                workflow.OnAcknowledgeChat -= HandleChatAcknowledgment;
                workflow.OnWorkflowStateChanged -= OnWorkflowStateChange;
            }

            void OnWorkflowStateChange(State newState)
            {
                var conversationID = new AssistantConversationId(workflow.ConversationId);
                switch (newState)
                {
                    case State.NotStarted:
                        ChangePromptState(conversationID, PromptState.NotConnected, $"Conversation {conversationID} has not yet started");
                        break;
                    case State.AwaitingDiscussionInitialization:
                        ChangePromptState(conversationID, PromptState.Connecting, $"Conversation {conversationID} is awaiting discussion initialization");
                        break;
                    case State.Idle:
                        if (!workflow.MessagesSent)
                            ChangePromptState(conversationID, PromptState.AwaitingServer, $"Conversation {conversationID} is waiting for the server to reply to a prompt.");
                        else
                            ChangePromptState(conversationID, PromptState.Connected, $"Conversation {conversationID} is connected and ready.");
                        break;
                    case State.AwaitingChatAcknowledgement:
                        // Waiting client
                        ChangePromptState(conversationID, PromptState.AwaitingServer, $"Conversation {conversationID} is waiting for the server to reply to a prompt.");
                        break;
                    case State.AwaitingChatResponse:
                        ChangePromptState(conversationID, PromptState.AwaitingClient, $"Conversation {conversationID} is constructing context with the server.");
                        break;
                    case State.ProcessingStream:
                        ChangePromptState(conversationID, PromptState.AwaitingServer, $"Conversation {conversationID} is streaming a message from the server.");
                        break;
                    case State.Canceling:
                        ChangePromptState(conversationID, PromptState.Canceling, $"User elected to cancel request on conversation {conversationID}");
                        break;
                    case State.Closed:
                        ChangePromptState(conversationID, PromptState.NotConnected, $"Conversation {conversationID}'s websocket has closed.  A new websocket must be created.");
                        break;
                }
            }
        }
    }
}
