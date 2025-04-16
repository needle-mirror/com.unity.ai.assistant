using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows;
using Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.Ai.Assistant.Protocol.Api;
using Unity.Ai.Assistant.Protocol.Client;
using Unity.Ai.Assistant.Protocol.Model;
using UnityEditor;
using UnityEngine;
using Inspiration = Unity.AI.Assistant.Editor.ApplicationModels.Inspiration;
using VersionSupportInfo = Unity.AI.Assistant.Editor.ApplicationModels.VersionSupportInfo;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.Backend.Socket
{
    class AssistantWebSocketBackend : IAssistantBackend
    {
        public Dictionary<string, object> Configuration { get; } = new();

        string GetAccessToken()
        {
            if (!Configuration.TryGetValue("access_token", out object value) || value is not string s)
                return CloudProjectSettings.accessToken;

            return s;
        }

        string GetOrganizationId()
        {
            if (!Configuration.TryGetValue("organization_id", out object value) || value is not string s)
                return CloudProjectSettings.organizationKey;

            return s;
        }


        // Below this line are functions separate from the old interface, that allow managing the Chat Workflow in
        // a new way, instead of trying to force it into the old way. This was how the old hacky version was created,
        // but it id not suitable for a production system.

        // The real implementation is hidden hear for now so that an IAssistantBackend can still be registered to the
        // Assistant. In the end though, the above interface should be changed to reflect the code below.
        internal static Dictionary<string, ChatWorkflow> m_ChatWorkflows = new();

        // This line here is again for speed. It lets me set the websocket factory for testing purposes.
        internal static WebSocketFactory s_WebSocketFactoryForNextRequest;

        private readonly Dictionary<AssistantMessageId, FeedbackData?> k_FeedbackCache = new();

        /// <summary>
        /// Try to get the workflow associated with the given conversationId.
        /// </summary>
        /// <param name="conversationId"></param>
        /// <param name="workflow"></param>
        /// <returns></returns>
        public bool TryGetWorkflow(string conversationId, out ChatWorkflow workflow)
        {
            if (conversationId == null)
            {
                workflow = null;
                return false;
            }

            return m_ChatWorkflows.TryGetValue(conversationId, out workflow);
        }

        /// <summary>
        /// Gets an existing workflow, or creates a new one and calls the Start() function on it.
        /// </summary>
        /// <param name="conversationId"></param>
        /// <returns></returns>
        public ChatWorkflow GetOrCreateWorkflow(IFunctionCaller caller, string conversationId = null)
        {
            ChatWorkflow workflow = null;

            if (conversationId != null)
            {
                if(!m_ChatWorkflows.TryGetValue(conversationId, out workflow))
                    workflow = new(conversationId, s_WebSocketFactoryForNextRequest, caller);
            }
            else
                workflow = new(websocketFactory: s_WebSocketFactoryForNextRequest, functionCaller: caller);

            s_WebSocketFactoryForNextRequest = null;

            workflow.OnClose -= HandleOnClose;
            workflow.OnClose += HandleOnClose;

            workflow.OnConversationId -= HandleOnConversationId;
            workflow.OnConversationId += HandleOnConversationId;

            if (workflow.WorkflowState == State.NotStarted)
            {
                workflow.Start(
                    AssistantEnvironment.instance.WebSocketApiUrl,
                    GetAccessToken(),
                    GetOrganizationId()).WithExceptionLogging();
            }


            return workflow;

            void HandleOnClose(CloseReason reason)
            {
                if(workflow.ConversationId != null)
                    m_ChatWorkflows.Remove(workflow.ConversationId);

                if(reason.Reason == CloseReason.ReasonType.CouldNotConnect)
                    Debug.LogError(reason.Info);
            }

            void HandleOnConversationId(string cid)
            {
                m_ChatWorkflows[cid] = workflow;
            }
        }

        /*
         * This section is the actual implementation of the interface
         */
        IAiAssistantApi m_Api;

        static readonly TimeSpan k_CancellationTimeout = TimeSpan.FromSeconds(30);

        static CancellationToken GetTimeoutToken(CancellationToken token) =>
            CancellationTokenSource.CreateLinkedTokenSource(
                    token, new CancellationTokenSource(k_CancellationTimeout).Token)
                .Token;

        internal static Configuration GetDefaultConfig(Func<string> getAccessToken)
        {
            Configuration config = new() { BasePath = AssistantEnvironment.instance.ApiUrl };
            SetDynamicAccessToken(config,true, getAccessToken);
            return config;
        }

        static void SetDynamicAccessToken(Configuration @this, bool active, Func<string> getAccessToken)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (active)
                @this.DynamicHeaders["Authorization"] = () => $"Bearer {getAccessToken()}";
            else
                @this.DynamicHeaders.Remove("Authorization");
        }

        internal AssistantWebSocketBackend(Configuration config = null, IAiAssistantApi api = null)
        {
            config ??= GetDefaultConfig(GetAccessToken);
            m_Api = api ?? new AiAssistantApi(config);
        }

        public bool SessionStatusTrackingEnabled => true;
        public async Task<IEnumerable<ConversationInfo>> ConversationRefresh(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var convosBuilder = m_Api
                .GetConversationInfoV1RequestBuilderWithAnalytics(GetOrganizationId())
                .SetLimit(AssistantConstants.MaxConversationHistory);

            var response = await convosBuilder.BuildAndSendAsync(ct);

            // TODO: REST Error Handling
            var data = response.Data;

            return data.Select(c => new ConversationInfo()
            {
                ConversationId = c.ConversationId.ToString(),
                IsFavorite = c.IsFavorite,
                LastMessageTimestamp = c.LastMessageTimestamp,
                Title = c.Title
            });
        }

        public async Task<string> ConversationGenerateTitle(string conversationId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var convosBuilder = m_Api
                .PutAssistantConversationInfoGenerateTitleUsingConversationIdV1BuilderWithAnalytics(GetOrganizationId(), Guid.Parse(conversationId));

            var response = await convosBuilder.BuildAndSendAsync(ct);

            // TODO: REST Error Handling
            ConversationTitleResponseV1 data = response.Data;
            return data.Title;
        }

        public async Task<ClientConversation> ConversationLoad(string conversationUid, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var response = await m_Api
                .GetAssistantConversationUsingConversationIdV1RequestBuilderWithAnalytics(GetOrganizationId(),Guid.Parse(conversationUid))
                .BuildAndSendAsync(ct);

            // TODO: REST Error Handling

            var data = response.Data;

            return new ClientConversation()
            {
                Owners = data.Owners,
                Title = data.Title,
                Context = "", // TODO: Get the backend to return the context
                History = data.History.Select(h =>
                {
                    return new ConversationFragment("", h.Markdown, h.Role.ToString())
                    {
                        ContextId = "", // No more context id
                        Id = h.Id.ToString(),
                        Preferred = false, // Where is prefered
                        RequestId = "", // where is request id
                        SelectedContextMetadata = h.AttachedContextMetadata?.Select(a => new SelectedContextMetadataItems()
                        {
                            DisplayValue = a.DisplayValue,
                            EntryType = a.EntryType,
                            Value = a.Value,
                            ValueIndex = a.ValueIndex,
                            ValueType = a.ValueType
                        }).ToList(), // where is select context metadata
                        Tags = new(),
                        Timestamp = h.Timestamp
                    };
                }).ToList(),
                Id = data.Id.ToString(),
                IsFavorite = data.IsFavorite,
                Tags = new() // no more tags
            };
        }

        public async Task ConversationFavoriteToggle(string conversationUid, bool isFavorite, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var response = await m_Api.PatchAssistantConversationInfoUsingConversationIdV1RequestBuilderWithAnalytics(GetOrganizationId(), Guid.Parse(conversationUid),
                new ConversationInfoUpdateV1 { IsFavorite = isFavorite}).
                BuildAndSendAsync(ct);
        }

        public async Task ConversationRename(string conversationUid, string newName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentNullException(nameof(newName));

            var response = await m_Api
                .PatchAssistantConversationInfoUsingConversationIdV1RequestBuilderWithAnalytics(GetOrganizationId(), Guid.Parse(conversationUid),
                new ConversationInfoUpdateV1 { Title = newName }).
                BuildAndSendAsync(ct);
        }

        public async Task ConversationDelete(string conversationUid, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var response = await m_Api
                .DeleteAssistantConversationUsingConversationIdV1RequestBuilderWithAnalytics(GetOrganizationId(),Guid.Parse(conversationUid))
                .BuildAndSendAsync(ct);

            if (!string.IsNullOrWhiteSpace(response.ErrorText))
            {
                throw new Exception(response.ErrorText);
            }
        }

        public async Task<IEnumerable<Inspiration>> InspirationRefresh(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var response = await m_Api.
                GetAssistantInspirationV1RequestBuilderWithAnalytics(GetOrganizationId()).
                BuildAndSendAsync(ct);

            // Just return response.Data when legacy routes are removed and everything is using Protocol Models
            if (response.StatusCode != HttpStatusCode.OK)
            {
                // TODO: Error Handling for inspirations
                return Array.Empty<Inspiration>();
            }

            if(response.Data == null)
                return Array.Empty<Inspiration>();

            return response.Data.Select(i => new Inspiration()
            {
                Description = i.Description,
                Id = i.Id.ToString(),
                Mode = (Inspiration.ModeEnum)(i.Mode),
                Value = i.Value
            });
        }

        public async Task<int> PointCostRequest(string conversationUid, int? contextItems, string prompt, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var orgID = GetOrganizationId();

            var response = await m_Api.
                GetAssistantMessagePointsV1RequestBuilderWithAnalytics(orgID).
                SetConversationId(conversationUid).
                SetContextItems(contextItems).
                SetPrompt(prompt).
                BuildAndSendAsync(ct);

            return response.Data.MessagePoints.GetInt();
        }

        public async Task SendFeedback(string conversationUid, MessageFeedback feedback, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var orgID = GetOrganizationId();

            var response = await m_Api.PostAssistantFeedbackV1RequestBuilderWithAnalytics(
                orgID,
                new FeedbackCreationV1(
                    (CategoryV1)feedback.Type,
                    Guid.Parse(conversationUid),
                    feedback.Message,
                    Guid.Parse(feedback.MessageId.FragmentId),
                    (SentimentV1)feedback.Sentiment
                )
            )
            .BuildAndSendAsync(ct);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var feedbackData = new FeedbackData(feedback.Sentiment, feedback.Message);
                k_FeedbackCache[feedback.MessageId] = feedbackData;
            }
        }

        public async Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default)
        {
            if (k_FeedbackCache.TryGetValue(messageId, out var cachedData))
            {
                return cachedData;
            }

            ct.ThrowIfCancellationRequested();

            var response = await m_Api.GetAssistantFeedbackUsingConversationIdAndMessageIdV1RequestBuilderWithAnalytics(
                    GetOrganizationId(),
                    messageId.ConversationId.Value,
                    messageId.FragmentId
            )
            .BuildAndSendAsync(ct);

            var feedbackData = response.Data != null ?
                new FeedbackData((Sentiment)response.Data.Sentiment, response.Data.Details) : (FeedbackData?)null;

            k_FeedbackCache[messageId] = feedbackData;

            return feedbackData;
        }

        public async Task<List<VersionSupportInfo>> GetVersionSupportInfo(string version, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var response = await m_Api.
                GetVersionsBuilder().
                BuildAndSendAsync(ct);

            return response.Data.Select(v => new VersionSupportInfo()
            {
                RoutePrefix = v.RoutePrefix,
                SupportStatus = (VersionSupportInfo.SupportStatusEnum)v.SupportStatus
            }).ToList();
        }
    }
}
