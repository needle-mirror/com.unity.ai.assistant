using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.ErrorHandling;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        static AssistantContextEntry[] ConvertSelectionContextToInternal(List<SelectedContextMetadataItems> context)
        {
            if (context == null || context.Count == 0)
            {
                return Array.Empty<AssistantContextEntry>();
            }

            var result = new AssistantContextEntry[context.Count];
            for (var i = 0; i < context.Count; i++)
            {
                var entry = context[i];
                if (entry.EntryType == null)
                {
                    // Invalid entry
                    UnityEngine.Debug.LogError("Invalid Selection Context Entry");
                    continue;
                }

                var entryType = (AssistantContextType)entry.EntryType;
                switch (entryType)
                {
                    case AssistantContextType.ConsoleMessage:
                    {
                        result[i] = new AssistantContextEntry
                        {
                            EntryType = AssistantContextType.ConsoleMessage,
                            Value = entry.Value,
                            ValueType = entry.ValueType
                        };

                        break;
                    }

                    default:
                    {
                        result[i] = new()
                        {
                            Value = entry.Value,
                            DisplayValue = entry.DisplayValue,
                            EntryType = entryType,
                            ValueType = entry.ValueType,
                            ValueIndex = entry.ValueIndex ?? 0
                        };

                        break;
                    }
                }
            }

            return result;
        }

        const int k_MaxInternalConversationTitleLength = 30;

        bool m_ConversationRefreshSuspended;

        /// <summary>
        /// Indicates that the conversations have been refreshed
        /// </summary>
        public event Action<IEnumerable<AssistantConversationInfo>> ConversationsRefreshed;

        /// <summary>
        /// The callback when a conversation has been loaded
        /// </summary>
        public event Action<AssistantConversation> ConversationLoaded;

        /// <summary>
        /// The callback when a conversation has changed in any way
        /// TODO: later on we will listen to a change event on the conversation itself, for now this replaces the update queue
        /// </summary>
        public event Action<AssistantConversation> ConversationChanged;

        /// <summary>
        /// Callback when a new conversation has been created
        /// </summary>
        public event Action<AssistantConversation> ConversationCreated;

        /// <summary>
        /// Callback when a conversation has been deleted
        /// </summary>
        public event Action<AssistantConversationId> ConversationDeleted;

        /// <inheritdoc />
        public event Action<AssistantConversationId, ErrorInfo> ConversationErrorOccured;

        public void SuspendConversationRefresh()
        {
            m_ConversationRefreshSuspended = true;
        }

        public void ResumeConversationRefresh()
        {
            m_ConversationRefreshSuspended = false;
        }

        private void NotifyConversationChange(AssistantConversation conversation)
        {
            ConversationChanged?.Invoke(conversation);
        }

        public async Task RefreshConversationsAsync(CancellationToken ct = default)
        {
            if (m_ConversationRefreshSuspended)
                return;

            var tag = UnityDataUtils.GetProjectId();

            var infosResult = await m_Backend.ConversationRefresh(await GetCredentialsContext(ct), ct);

            if (infosResult.Status != BackendResult.ResultStatus.Success)
            {
                ErrorHandlingUtility.PublicLogBackendResultError(infosResult);
                return;
            }

            var conversations = infosResult.Value.Select(
                info => new AssistantConversationInfo()
                {
                    Id = new(info.ConversationId),
                    Title = info.Title,
                    LastMessageTimestamp = info.LastMessageTimestamp,
                    IsContextual = IsContextual(info, tag),
                    IsFavorite = info.IsFavorite != null && info.IsFavorite.Value
                });

            ConversationsRefreshed?.Invoke(conversations);

            return;

            bool IsContextual(ConversationInfo c, string projectTag)
            {
                if (c.Tags == null)
                {
                    return false;
                }

                var projectId = c.Tags.FirstOrDefault(tag => tag.StartsWith(AssistantConstants.ProjectIdTagPrefix));
                return projectId is null || projectId == projectTag;
            }
        }

        public async Task ConversationLoad(AssistantConversationId conversationId, CancellationToken ct = default)
        {
            if(!conversationId.IsValid)
                throw new ArgumentException("Invalid conversation id");

            var result = await m_Backend.ConversationLoad(await GetCredentialsContext(ct), conversationId.Value, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                string errorMessage = "Failed to load the conversation.";
                ConversationErrorOccured?.Invoke(conversationId, new ErrorInfo(errorMessage, result.ToString()));
                return;
            }

            var conversation = ConvertConversation(result.Value);

            if (!m_ConversationCache.TryAdd(conversationId, conversation))
            {
                m_ConversationCache[conversationId] = conversation;
            }

            foreach (var message in conversation.Messages)
            {
                FixBASST266(message);
            }

            ConversationLoaded?.Invoke(conversation);
        }

        public async Task ConversationFavoriteToggle(AssistantConversationId conversationId, bool isFavorite)
        {
            if(!conversationId.IsValid)
                throw new ArgumentException("Invalid conversation id");

            BackendResult result = await m_Backend.ConversationFavoriteToggle(await GetCredentialsContext(), conversationId.Value, isFavorite);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                ErrorHandlingUtility.PublicLogBackendResultError(result);
                return;
            }
        }

        public async Task ConversationRename(AssistantConversationId conversationId, [NotNull] string newName, CancellationToken ct = default)
        {
            if (!conversationId.IsValid)
            {
                return;
            }

            BackendResult result = await m_Backend.ConversationRename(await GetCredentialsContext(ct), conversationId.Value, newName, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                ErrorHandlingUtility.PublicLogBackendResultError(result);
                return;
            }

            await RefreshConversationsAsync(ct);
        }

        public async Task ConversationDeleteAsync(AssistantConversationId conversationId, CancellationToken ct = default)
        {
            if (!conversationId.IsValid)
            {
                return;
            }

            BackendResult result = await m_Backend.ConversationDelete(await GetCredentialsContext(ct), conversationId.Value, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                ErrorHandlingUtility.PublicLogBackendResultError(result);
                return;
            }

            ConversationDeleted?.Invoke(conversationId);
        }

        AssistantConversation ConvertConversation(ClientConversation remoteConversation)
        {
            var conversationId = new AssistantConversationId(remoteConversation.Id);
            AssistantConversation localConversation = new()
            {
                Id = conversationId,
                Title = remoteConversation.Title
            };

            for (var i = 0; i < remoteConversation.History.Count; i++)
            {
                var fragment = remoteConversation.History[i];
                var message = new AssistantMessage
                {
                    Id = new(conversationId, fragment.Id, AssistantMessageIdType.External),
                    IsComplete = true,
                    Role = fragment.Role,
                    Content = fragment.Content,
                    Timestamp = fragment.Timestamp,
                    Context = ConvertSelectionContextToInternal(fragment.SelectedContextMetadata),
                    MessageIndex = i
                };

                localConversation.Messages.Add(message);
            }

            return localConversation;
        }
    }
}
