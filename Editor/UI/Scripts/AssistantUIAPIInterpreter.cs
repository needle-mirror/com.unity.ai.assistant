using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.CodeAnalyze;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Agent;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal class AssistantUIAPIInterpreter
    {
        readonly AssistantBlackboard m_Blackboard;

        public AssistantUIAPIInterpreter(IAssistantProvider provider, AssistantBlackboard blackboard)
        {
            m_Blackboard = blackboard;
            Provider = provider;
        }

        /// <summary>
        /// The current Assistant provider in use, generally try to avoid using this directly, the interpreter functions should suffice
        /// </summary>
        public IAssistantProvider Provider { get; }

        public void Initialize()
        {
            Provider.OnConnectionChanged += OnConnectionChanged;

            Provider.ConversationLoaded += OnConversationLoaded;
            Provider.ConversationCreated += OnConversationCreated;
            Provider.ConversationChanged += OnConversationChanged;
            Provider.ConversationDeleted += OnConversationDeleted;
            Provider.ConversationsRefreshed += OnConversationsRefreshed;
            Provider.ConversationErrorOccured += OnConversationErrorOccured;

            Provider.PointCostReceived += OnPointCostReceived;

            Provider.PromptStateChanged += OnPromptStateChanged;
            Provider.InspirationsRefreshed += OnInspirationsRefreshed;

            Provider.FeedbackLoaded += OnFeedbackLoaded;
        }

        public event Action<string, bool> ConnectionChanged;
        public event Action<AssistantConversationId> ConversationReload;
        public event Action<AssistantConversationId> ConversationChanged;
        public event Action<AssistantConversationId> ConversationDeleted;
        public event Action ConversationsRefreshed;
        public event Action InspirationsRefreshed;
        public event Action APIStateChanged;

        public event Action<PointCostRequestId, int> PointCostReceived;

        public event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;

        void OnConversationChanged(AssistantConversation data)
        {
            var model = ConvertConversationToModel(data);

            TaskUtils.DispatchToMainThread(() =>
            {
                if (!m_Blackboard.ActiveConversationId.IsValid)
                {
                    m_Blackboard.SetActiveConversation(data.Id);
                }

                ConversationChanged?.Invoke(model.Id);
            });
        }

        void NotifyAPIStateChanged(AssistantConversationId conversationId)
        {
            if (m_Blackboard.ActiveConversationId.IsValid && conversationId != m_Blackboard.ActiveConversationId)
            {
                // Only inform changes for the active conversation (or if none set)
                return;
            }

            TaskUtils.DispatchToMainThread(() =>
                APIStateChanged?.Invoke());
        }

        void OnInspirationsRefreshed(IEnumerable<AssistantInspiration> data)
        {
            m_Blackboard.Inspirations.Clear();
            foreach (AssistantInspiration inspiration in data)
            {
                var model = new InspirationModel { Command = inspiration.Command, Value = inspiration.Value };
                m_Blackboard.Inspirations.Add(model);
            }

            TaskUtils.DispatchToMainThread(() =>
                InspirationsRefreshed?.Invoke());
        }

        void OnFeedbackLoaded(AssistantMessageId messageId, FeedbackData? feedback)
        {
            TaskUtils.DispatchToMainThread(() =>
                FeedbackLoaded?.Invoke(messageId, feedback));
        }

        void OnConversationDeleted(AssistantConversationId conversationId)
        {
            if (m_Blackboard.RemoveConversation(conversationId))
            {
                if (m_Blackboard.ActiveConversationId == conversationId)
                {
                    m_Blackboard.SetActiveConversation(AssistantConversationId.Invalid);
                }

                TaskUtils.DispatchToMainThread(() =>
                    ConversationDeleted?.Invoke(conversationId));
            }
        }

        void OnConversationsRefreshed(IEnumerable<AssistantConversationInfo> infos)
        {
            foreach (var conversationInfo in infos)
            {
                var model = m_Blackboard.GetConversation(conversationInfo.Id);
                if (model == null)
                {
                    model = new ConversationModel
                    {
                        Id = conversationInfo.Id,
                        IsLoaded = false,
                    };

                    m_Blackboard.UpdateConversation(model.Id, model);
                }

                model.Title = conversationInfo.Title;
                model.LastMessageTimestamp = conversationInfo.LastMessageTimestamp;
                model.IsFavorite = conversationInfo.IsFavorite;

                m_Blackboard.SetFavorite(conversationInfo.Id, conversationInfo.IsFavorite);
            }

            TaskUtils.DispatchToMainThread(() =>
                ConversationsRefreshed?.Invoke());
        }

        void OnConnectionChanged(string message, bool connected)
        {
            TaskUtils.DispatchToMainThread(() =>
                ConnectionChanged?.Invoke(message, connected));
        }

        void OnPromptStateChanged(AssistantConversationId conversationId, Assistant.Editor.Assistant.PromptState newState)
        {
            if (conversationId != m_Blackboard.ActiveConversationId)
            {
                InternalLog.Log("Ignoring state request change for non-active conversation");
                return;
            }

            m_Blackboard.IsAPIStreaming = false;
            m_Blackboard.IsAPIRepairing = false;
            m_Blackboard.IsAPIReadyForPrompt = false;
            m_Blackboard.IsAPICanceling = false;

            switch (newState)
            {
                case Assistant.Editor.Assistant.PromptState.NotConnected:
                {
                    SetWorkingState(false);
                    m_Blackboard.IsAPIReadyForPrompt = true;
                    break;
                }

                case Assistant.Editor.Assistant.PromptState.Connected:
                {
                    SetWorkingState(false);
                    m_Blackboard.IsAPIReadyForPrompt = true;
                    break;
                }
                case Assistant.Editor.Assistant.PromptState.Connecting:
                case Assistant.Editor.Assistant.PromptState.AwaitingServer:
                case Assistant.Editor.Assistant.PromptState.AwaitingClient:
                {
                    SetWorkingState(true);
                    m_Blackboard.IsAPIStreaming = true;
                    break;
                }

                case Assistant.Editor.Assistant.PromptState.Canceling:
                {
                    m_Blackboard.IsAPICanceling = true;
                    m_Blackboard.IsAPIReadyForPrompt = false;
                    break;
                }
            }

            NotifyAPIStateChanged(conversationId);
        }

        void OnConversationErrorOccured(AssistantConversationId conversationId, ErrorInfo info)
        {
            var conversation = m_Blackboard.GetConversation(conversationId);

            if (conversation == null)
            {
                ErrorHandlingUtility.PublicLogError(info);
                SetWorkingState(false);
                return;
            }

            if (m_Blackboard.ActiveConversationId == conversation.Id)
            {
                ErrorHandlingUtility.InternalLogError(info);
                conversation.Messages.Add(new MessageModel()
                {
                    Role = MessageModelRole.Error,
                    IsComplete = true,
                    Content = info.PublicMessage
                });

                TaskUtils.DispatchToMainThread(() =>
                    ConversationChanged?.Invoke(conversation.Id));
        }

            Provider.AbortPrompt(conversationId);

            if (m_Blackboard.IsAPIWorking)
            {
                SetWorkingState(false);
            }
        }

        public void Deinitialize()
        {
            Provider.OnConnectionChanged -= OnConnectionChanged;
            Provider.ConversationsRefreshed -= OnConversationsRefreshed;
            Provider.ConversationErrorOccured -= OnConversationErrorOccured;
            Provider.PromptStateChanged -= OnPromptStateChanged;
            Provider.ConversationLoaded -= OnConversationLoaded;
            Provider.ConversationChanged -= OnConversationChanged;
            Provider.ConversationDeleted -= OnConversationDeleted;
            Provider.InspirationsRefreshed -= OnInspirationsRefreshed;
        }

        void OnConversationCreated(AssistantConversation conversation)
        {
            var model = ConvertConversationToModel(conversation);
            
            TaskUtils.DispatchToMainThread(() =>
            {
                if (!m_Blackboard.ActiveConversationId.IsValid)
                {
                    m_Blackboard.SetActiveConversation(conversation.Id);
                }

                ConversationReload?.Invoke(model.Id);
            });
        }

        void OnConversationLoaded(AssistantConversation conversation)
        {
            var model = ConvertConversationToModel(conversation);
            OnPromptStateChanged(m_Blackboard.ActiveConversationId, Assistant.Editor.Assistant.PromptState.NotConnected);
                  TaskUtils.DispatchToMainThread(() =>
                ConversationReload?.Invoke(model.Id));
        }

        ConversationModel ConvertConversationToModel(AssistantConversation conversation)
        {
            var model = m_Blackboard.GetConversation(conversation.Id);
            if (model == null)
            {
                model = new ConversationModel { Id = conversation.Id };
                m_Blackboard.UpdateConversation(model.Id, model);
            }

            model.Title = conversation.Title;

            model.Messages.Clear();
            foreach (AssistantMessage message in conversation.Messages)
            {
                var messageModel = ConvertMessageToModel(message);
                model.Messages.Add(messageModel);
            }

            model.IsLoaded = true;
            return model;
        }

        public void RefreshInspirations()
        {
            Provider.RefreshInspirations().WithExceptionLogging();
        }

        public void ConversationLoad(AssistantConversationId conversationId)
        {
            Provider.ConversationLoad(conversationId).WithExceptionLogging();
        }

        public void SetFavorite(AssistantConversationId conversationId, bool isFavorited)
        {
            Provider.ConversationFavoriteToggle(conversationId, isFavorited);

            // Set the local caches so we are in sync until the next server data
            var conversation = m_Blackboard.GetConversation(conversationId);
            if (conversation != null)
            {
                conversation.IsFavorite = isFavorited;
            }

            m_Blackboard.SetFavorite(conversationId, isFavorited);

            TaskUtils.DispatchToMainThread(() =>
                ConversationsRefreshed?.Invoke());
        }

        public void ConversationDelete(AssistantConversationId conversationId)
        {
            Provider.ConversationDeleteAsync(conversationId).WithExceptionLogging();
        }

        public void ConversationRename(AssistantConversationId conversationId, string newName)
        {
            Provider.ConversationRename(conversationId, newName).WithExceptionLogging();
        }

        public void GetPointCost(PointCostRequestId id, PointCostRequestData data)
        {
            Provider.PointCostRequest(id, data).WithExceptionLogging();
        }

        public void SuspendConversationRefresh()
        {
            Provider.SuspendConversationRefresh();
        }

        public void ResumeConversationRefresh()
        {
            Provider.ResumeConversationRefresh();
        }

        public void RefreshConversations()
        {
            Provider.RefreshConversationsAsync().WithExceptionLogging();
        }

        public void CancelAssistant(AssistantConversationId conversationId)
        {
            Provider.AbortPrompt(conversationId);
            Provider.ConversationLoad(conversationId);

            if (m_Blackboard.IsAPIWorking)
            {
                SetWorkingState(false);
            }
        }

        public void Reset()
        {
            OnPromptStateChanged(m_Blackboard.ActiveConversationId, Assistant.Editor.Assistant.PromptState.NotConnected);
        }

        public void CancelPrompt()
        {
            if (m_Blackboard.IsAPIStreaming)
                Provider.AbortPrompt(m_Blackboard.ActiveConversationId);
        }

        public void SendPrompt(string stringPrompt)
        {
            if (!m_Blackboard.IsAPIWorking)
            {
                SetWorkingState(true);
            }

            RemoveErrorFromCurrentConversation();
            Provider.ProcessPrompt(m_Blackboard.ActiveConversationId, BuildPrompt(stringPrompt)).WithExceptionLogging();
        }

        void RemoveErrorFromCurrentConversation()
        {
            // Remove any error messages in the active conversation
            if (m_Blackboard.ActiveConversationId.IsValid)
            {
                var conversation = m_Blackboard.GetConversation(m_Blackboard.ActiveConversationId);

                Queue<MessageModel> errors = new();

                foreach (var message in conversation.Messages)
                {
                    if(message.Role == MessageModelRole.Error)
                        errors.Enqueue(message);
                }

                if (errors.Count > 0)
                {
                    while (errors.Count > 0)
                        conversation.Messages.Remove(errors.Dequeue());

                    TaskUtils.DispatchToMainThread(() =>
                        ConversationChanged?.Invoke(conversation.Id));
                }
            }
        }

        AssistantPrompt BuildPrompt(string stringPrompt)
        {
            var prompt = new AssistantPrompt(stringPrompt);
            prompt.ObjectAttachments.AddRange(m_Blackboard.ObjectAttachments);
            prompt.ConsoleAttachments.AddRange(m_Blackboard.ConsoleAttachments);

            return prompt;
        }

        public void SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote)
        {
            Provider.SendFeedback(messageId, flagMessage, feedbackText, upVote).WithExceptionLogging();
        }

        public void LoadFeedback(AssistantMessageId messageId)
        {
            Provider.LoadFeedback(messageId).WithExceptionLogging();
        }

        public void SendEditRunCommand(AssistantMessageId messageId, string updatedCode)
        {
            Provider.SendEditRunCommand(messageId, updatedCode).WithExceptionLogging();
        }

        public bool ValidateCode(string code, out string localFixedCode, out CompilationErrors compilationErrors)
        {
            return Provider.ValidateCode(code, out localFixedCode, out compilationErrors);
        }

        public int GetAttachedContextLength()
        {
            var contextBuilder = new ContextBuilder();
            Provider.GetAttachedContextString(BuildPrompt(string.Empty), ref contextBuilder, true);
            return contextBuilder.PredictedLength;
        }

        public AgentRunCommand BuildAgentRunCommand(string script, IEnumerable<UnityEngine.Object> contextAttachments)
        {
            return Provider.BuildAgentRunCommand(script, contextAttachments);
        }

        public void RunAgentCommand(AgentRunCommand command)
        {
            Provider.RunAgentCommand(m_Blackboard.ActiveConversationId, command, ChatElementRunExecutionBlock.FencedBlockTag);
        }

        public ExecutionResult GetRunCommandExecution(int executionId)
        {
            return Provider.GetRunCommandExecution(executionId);
        }

        MessageModel ConvertMessageToModel(AssistantMessage message)
        {
            var result = new MessageModel
            {
                Id = message.Id,
                Content = message.Content,
                SourceAttribution = message.SourceAttribution,
                IsComplete = message.IsComplete,
                Context = message.Context,
            };

            if (message.IsError)
            {
                result.Role = MessageModelRole.Error;
                result.IsComplete = true;
            }
            else
            {
                switch (message.Role.ToLower())
                {
                    case Assistant.Editor.Assistant.k_AssistantRole:
                    {
                        result.Role = MessageModelRole.Assistant;
                        break;
                    }

                    case Assistant.Editor.Assistant.k_UserRole:
                    {
                        result.Role = MessageModelRole.User;

                        // Trim out slash commands from user messages
                        if(ChatCommandParser.Parse(result.Content, out var commandHandler))
                        {
                            if (commandHandler.Command != AskCommand.k_CommandName)
                            {
                                result.Command = commandHandler.Command;
                            }

                            int commandLength = 1 + commandHandler.Command.Length;
                            if (result.Content.Length > commandLength)
                            {
                                result.Content = result.Content.Substring(commandLength, result.Content.Length - commandLength).Trim();
                            }
                        }

                        break;
                    }

                    case Assistant.Editor.Assistant.k_SystemRole:
                    {
                        result.Role = MessageModelRole.System;
                        break;
                    }

                    default:
                    {
                        throw new InvalidDataException("Unknown message role: " + message.Role);
                    }
                }
            }

            return result;
        }

        // TODO: this needs to be adjusted to handle multiple active conversations properly
        public void SetWorkingState(bool isWorking)
        {
            if (m_Blackboard.IsAPIWorking == isWorking)
            {
                return;
            }

            m_Blackboard.IsAPIWorking = isWorking;
            if (isWorking && m_Blackboard.ActiveConversation != null)
            {
                m_Blackboard.ActiveConversation.StartTime = 0;
            }

            TaskUtils.DispatchToMainThread(() =>
                APIStateChanged?.Invoke());
        }

        void OnPointCostReceived(PointCostRequestId id, int value)
        {
            TaskUtils.DispatchToMainThread(() =>
                PointCostReceived?.Invoke(id, value));
        }

        public void HandleFunctionCall(string functionId, string[] functionParameters, AssistantUIContext context)
            => Provider.FunctionCaller.CallPlugin(functionId, functionParameters, context);
    }
}
