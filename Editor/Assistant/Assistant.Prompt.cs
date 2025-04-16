using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket;
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
            None,
            GatheringContext,
            Musing,
            Streaming,
            RepairCode,
            Canceling
        }

        // TODO: this single prompt state is not sufficient, we can have multiple active conversations running and this is not indicative of their progress
        internal PromptState CurrentPromptState { get; private set; }

        // TODO: this only exists to support the ObjectDataExtractor tool
        internal readonly Dictionary<string, AssistantPrompt> k_TEMP_ActivePromptMap = new();

        public event Action<AssistantConversationId, PromptState> PromptStateChanged;

        void ChangePromptState(AssistantConversationId conversationId, PromptState newState)
        {
            if (CurrentPromptState == newState)
            {
                return;
            }

            CurrentPromptState = newState;
            PromptStateChanged?.Invoke(conversationId, newState);
        }

        public void AbortPrompt(AssistantConversationId conversationId)
        {
            if (CurrentPromptState is PromptState.Canceling or PromptState.None)
            {
                InternalLog.LogWarning($"AbortPrompt: Ignored in state {CurrentPromptState}");
                return;
            }

            m_ContextCancelToken?.Cancel();

            ChangePromptState(conversationId, PromptState.Canceling);

            // Orchestration uses workflows to manage the connection to the backend rather than the stream object. When
            // orchestration is the only system, the stream objects will be removed.
            if (m_Backend is AssistantWebSocketBackend webSocketBackend)
            {
                if (webSocketBackend.TryGetWorkflow(conversationId.Value, out ChatWorkflow workflow))
                    workflow.CancelCurrentChatRequest();

                return;
            }

            // Cancel any active operation to ensure no additional messages arrive while or after we're deleting:
            var stream = GetStreamForConversation(conversationId);

            if (stream != null)
                stream.CancellationTokenSource.Cancel();
        }

        // This should be the new way of doing things. The original ProcessPrompt will be removed. All backends will
        // need to be updated to work with the new process prompt.
        public async Task ProcessPrompt(AssistantConversationId conversationId, AssistantPrompt prompt,
            CancellationToken ct = default)
        {
            // Create a temporary mapping to allow one of the function calls to work
            k_TEMP_ActivePromptMap.TryAdd(prompt.Value, prompt);

            // cast the backend to access some functions
            var castBackend = m_Backend as AssistantWebSocketBackend;

            // get the appropriate workflow
            ChatWorkflow workflow = null;
            bool isNewConversation = !conversationId.IsValid;
            workflow = !isNewConversation
                ? castBackend.GetOrCreateWorkflow(FunctionCaller, conversationId.Value)
                : castBackend.GetOrCreateWorkflow(FunctionCaller);

            bool result = await workflow.AwaitDiscussionInitialization();
            prompt.ConversationId = new AssistantConversationId(workflow.ConversationId);

            if (result == false)
            {
                ConversationCreationFailed?.Invoke(conversationId);

                Debug.LogError("NEEDS UX: Discussion Init did not happen");
                return;
            }

            if (CurrentPromptState == PromptState.Canceling)
            {
                InternalLog.LogWarning("ProcessPrompt: Early out due to user cancelation - so far no valid conversation Id (necessary)!");
                ChangePromptState(conversationId, PromptState.None);
                return;
            }

            conversationId = prompt.ConversationId;

            // Setup the conversation object that bridges the data and the UI
            // TODO: Deal with the conversation title

            if (!m_ConversationCache.TryGetValue(prompt.ConversationId, out var conversation))
            {
                conversation = new AssistantConversation
                {
                    Title = "Not Implemented",
                    Id = prompt.ConversationId,
                    StartTime = EditorApplication.timeSinceStartup
                };

                m_ConversationCache.Add(prompt.ConversationId, conversation);
            }

            // Add the messages needed to start rendering the response
            var promptMessage = AddInternalMessage(conversation, prompt.Value, role: k_UserRole, sendUpdate: true);
            promptMessage.Context = ContextSerializationHelper.BuildPromptSelectionContext(prompt.ObjectAttachments, prompt.ConsoleAttachments).m_ContextList.ToArray();

            var assistantMessage = AddIncompleteMessage(conversation, string.Empty, k_AssistantRole, sendUpdate: false);
            ConversationCreated?.Invoke(conversation);

            // Make the progress bar indicate musing
            conversation.StartTime = EditorApplication.timeSinceStartup;
            ChangePromptState(conversationId, PromptState.Musing);

            // listen to the appropriate events from the workflow
            StringBuilder sb = new();

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
            AIAssistantAnalytics.ReportSendUserMessageEvent(originalPrompt, command,
                prompt.ConversationId.Value);

            var context = await GetContextModel(prompt.ConversationId, AssistantSettings.PromptContextLimit - prompt.Value.Length, prompt, default); // TODO: Cancelation
            workflow.SendChatRequest($"/{command} {prompt.Value}", context, ct).WithExceptionLogging();


            return;

            void HandleClose(CloseReason reason)
            {
                Debug.LogError($"Client Disconnected ({reason.Reason}): NEED UX FOR THIS");

                ChangePromptState(conversationId, PromptState.None);

                CleanupEvents();

                if (reason.Reason != CloseReason.ReasonType.ServerDisconnected)
                {
                    ConversationCreationFailed?.Invoke(conversationId);
                }
            }

            void HandleChatAcknowledgment(AcknowledgePromptInfo info)
            {
                // TODO: We are waiting for a protocol change that allows the AcknowledgePromptInfo object to be
                // populated with author content and context. When that information comes through, below we should
                // populate the promptMessage with all info provided by the server.
                promptMessage.Id =
                    new AssistantMessageId(conversation.Id, info.Id, AssistantMessageIdType.External);

                NotifyConversationChange(conversation);
            }

            void HandleChatResponse(ChatResponseFragment fragment)
            {
                sb.Append(fragment.Fragment);

                if(assistantMessage.Id.FragmentId != fragment.Id)
                    assistantMessage.Id = new AssistantMessageId(conversation.Id, fragment.Id, AssistantMessageIdType.External);

                assistantMessage.Content = sb.ToString();
                assistantMessage.IsComplete = fragment.IsLastFragment;

                if (fragment.IsLastFragment)
                {
                    assistantMessage.IsComplete = true;
                    assistantMessage.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    assistantMessage.MessageIndex = conversation.Messages.Count - 1;

                    ChangePromptState(new AssistantConversationId(workflow.ConversationId), PromptState.None);
                    CleanupEvents();

                    if (isNewConversation)
                    {
                        _ = GenerateConversationTitle();
                        async Task GenerateConversationTitle()
                        {
                            string title = await m_Backend.ConversationGenerateTitle(workflow.ConversationId, ct);
                            conversation.Title = title;
                            NotifyConversationChange(conversation);
                        }
                    }
                }

                NotifyConversationChange(conversation);
            }

            void CleanupEvents()
            {
                workflow.OnClose -= HandleClose;
                workflow.OnChatResponse -= HandleChatResponse;
                workflow.OnAcknowledgeChat -= HandleChatAcknowledgment;
            }
        }
    }
}
