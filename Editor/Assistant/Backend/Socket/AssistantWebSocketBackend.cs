using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.ErrorHandling;
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
        // Below this line are functions separate from the old interface, that allow managing the Chat Workflow in
        // a new way, instead of trying to force it into the old way. This was how the old hacky version was created,
        // but it id not suitable for a production system.

        // This line here is again for speed. It lets me set the websocket factory for testing purposes.
        internal static WebSocketFactory s_WebSocketFactoryForNextRequest;

        private readonly Dictionary<AssistantMessageId, FeedbackData?> k_FeedbackCache = new();

        internal ChatWorkflow m_ActiveWorkflow;

        /// <summary>
        /// Retrieves the workflow being used for the most current conversation
        /// </summary>
        public ChatWorkflow ActiveWorkflow
        {
            get { return m_ActiveWorkflow; }
        }

        /// <summary>
        /// Gets an existing workflow, or creates a new one and calls the Start() function on it.
        /// </summary>
        /// <param name="caller"></param>
        /// <param name="conversationId"></param>
        /// <param name="credentialsContext"></param>
        /// <returns></returns>
        public ChatWorkflow GetOrCreateWorkflow(
            CredentialsContext credentialsContext,
            IFunctionCaller caller,
            AssistantConversationId conversationId = default)
        {
            ChatWorkflow workflow = null;

            if (conversationId.IsValid && m_ActiveWorkflow != null && m_ActiveWorkflow.ConversationId == conversationId.Value)
            {
                workflow = m_ActiveWorkflow;
            }
            else
            {
                // If there is an existing active workflow, destroy it!
                if (m_ActiveWorkflow != null)
                {
                    InternalLog.Log("Disconnecting existing workflow for new workflow.");
                    m_ActiveWorkflow.LocalDisconnect();
                }
                workflow = conversationId.IsValid ? new(conversationId.Value, s_WebSocketFactoryForNextRequest, caller) : new(websocketFactory: s_WebSocketFactoryForNextRequest, functionCaller: caller);
                m_ActiveWorkflow = workflow;
            }

            s_WebSocketFactoryForNextRequest = null;

            workflow.OnClose -= HandleOnClose;
            workflow.OnClose += HandleOnClose;

            if (workflow.WorkflowState == State.NotStarted)
            {
                workflow.Start(
                    AssistantEnvironment.WebSocketApiUrl,
                    credentialsContext).WithExceptionLogging();
            }

            return workflow;

            void HandleOnClose(CloseReason reason)
            {
                if (m_ActiveWorkflow == workflow)
                    m_ActiveWorkflow = null;

            }
        }

        public void ForceDisconnectWorkflow(string conversationId)
        {
            if (m_ActiveWorkflow != null && m_ActiveWorkflow.ConversationId == conversationId)
            {
                m_ActiveWorkflow.LocalDisconnect();
            }
        }

        // Needed for testing:
        private IAiAssistantApi m_ApiOverride;

        internal AssistantWebSocketBackend(IAiAssistantApi api = null)
        {
            m_ApiOverride = api;
        }

        internal IAiAssistantApi GetApi(CredentialsContext credentialsContext)
        {
            if (m_ApiOverride != null)
            {
                return m_ApiOverride;
            }

            Configuration config = new()
            {
                BasePath = AssistantEnvironment.ApiUrl,
                DynamicHeaders =
                {
                    ["Authorization"] = () => $"Bearer {credentialsContext.AccessToken}"
                }
            };

            return new AiAssistantApi(config)
            {
                CredentialsContext = credentialsContext
            };
        }

        #region IAssistantBackend

        public bool SessionStatusTrackingEnabled => true;
        public async Task<BackendResult<IEnumerable<ConversationInfo>>> ConversationRefresh(CredentialsContext credentialsContext, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<IEnumerable<ConversationInfo>>.FailOnCancellation();
            try
            {
                var convosBuilder = GetApi(credentialsContext)
                    .GetConversationInfoV1RequestBuilderWithAnalytics()
                    .SetLimit(AssistantConstants.MaxConversationHistory);

                var response = await convosBuilder.BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult<IEnumerable<ConversationInfo>>.FailOnServerResponse(
                        new(ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                            (int)response.StatusCode,
                            response.RawContent,
                            GetServerErrorMessage("refreshing conversations")), FormatApiResponse(response)));
                }

                List<ConversationInfoV1> data = response.Data;

                var cis = data.Select(c => new ConversationInfo()
                {
                    ConversationId = c.ConversationId.ToString(),
                    IsFavorite = c.IsFavorite,
                    LastMessageTimestamp = c.LastMessageTimestamp,
                    Title = c.Title
                });

                return BackendResult<IEnumerable<ConversationInfo>>.Success(cis);
            }
            catch (Exception e)
            {
                return BackendResult<IEnumerable<ConversationInfo>>.FailOnException(GetExceptionErrorMessage("refreshing conversations"), e);
            }
        }

        public async Task<BackendResult<string>> ConversationGenerateTitle(CredentialsContext credentialsContext,
            string conversationId,
            CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<string>.FailOnCancellation();

            try
            {
                var convosBuilder = GetApi(credentialsContext)
                    .PutAssistantConversationInfoGenerateTitleUsingConversationIdV1BuilderWithAnalytics(
                        Guid.Parse(conversationId)
                    );

                var response = await convosBuilder.BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult<string>.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("generating title")), FormatApiResponse(response)));
                }

                ConversationTitleResponseV1 data = response.Data;
                return BackendResult<string>.Success(data.Title);
            }
            catch (Exception e)
            {
                return BackendResult<string>.FailOnException(GetExceptionErrorMessage("generating title"), e);
            }
        }

        public async Task<BackendResult<ClientConversation>> ConversationLoad(CredentialsContext credentialsContext,
            string conversationUid,
            CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<ClientConversation>.FailOnCancellation();

            try
            {
                var response = await GetApi(credentialsContext)
                    .GetAssistantConversationUsingConversationIdV1RequestBuilderWithAnalytics(
                        Guid.Parse(conversationUid)
                    )
                    .BuildAndSendAsync(ct);

                var data = response.Data;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult<ClientConversation>.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("loading conversation")), FormatApiResponse(response)));
                }

                ClientConversation cliConvo = new ClientConversation()
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
                            SelectedContextMetadata = h.AttachedContextMetadata?.Select(a =>
                                new SelectedContextMetadataItems()
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

                return BackendResult<ClientConversation>.Success(cliConvo);
            }
            catch
                (Exception e)
            {
                return BackendResult<ClientConversation>.FailOnException(GetExceptionErrorMessage("loading conversation"), e);
            }
        }

        public async Task<BackendResult> ConversationFavoriteToggle(CredentialsContext credentialsContext,
            string conversationUid,
            bool isFavorite,
            CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<IEnumerable<Inspiration>>.FailOnCancellation();
            try
            {
                var response = await GetApi(credentialsContext)
                    .PatchAssistantConversationInfoUsingConversationIdV1RequestBuilderWithAnalytics(
                        Guid.Parse(conversationUid),
                        new ConversationInfoUpdateV1 { IsFavorite = isFavorite, }
                    ).BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("toggling favorite")), FormatApiResponse(response)));
                }

                return BackendResult.Success();
            }
            catch (Exception e)
            {
                return BackendResult.FailOnException(GetExceptionErrorMessage("toggling favorite"), e);
            }
        }

        public async Task<BackendResult> ConversationRename(
            CredentialsContext credentialsContext,
            string conversationUid,
            string newName,
            CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult.FailOnCancellation();

            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                    throw new ArgumentNullException(nameof(newName));

                var response = await GetApi(credentialsContext)
                    .PatchAssistantConversationInfoUsingConversationIdV1RequestBuilderWithAnalytics(
                        Guid.Parse(conversationUid),
                        new ConversationInfoUpdateV1 { Title = newName }
                    ).BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("renaming conversation")), FormatApiResponse(response)));
                }

                return BackendResult.Success();
            }
            catch (Exception e)
            {
                return BackendResult.FailOnException(GetExceptionErrorMessage("renaming conversation"), e);
            }
        }

        public async Task<BackendResult> ConversationDelete(CredentialsContext credentialsContext, string conversationUid, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult.FailOnCancellation();

            try
            {
                var conversationId = Guid.Parse(conversationUid);
                var responseBuilder = GetApi(credentialsContext)
                    .DeleteAssistantConversationUsingConversationIdV1RequestBuilderWithAnalytics(
                        conversationId);
                var response = await responseBuilder.BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult.FailOnServerResponse(new(ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("deleting a conversation")), FormatApiResponse(response)));
                }

                return BackendResult.Success();
            }
            catch (Exception e)
            {
                return BackendResult.FailOnException(GetExceptionErrorMessage("deleting a conversation"), e);
            }
        }

        public async Task<BackendResult<IEnumerable<Inspiration>>> InspirationRefresh(CredentialsContext credentialsContext, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<IEnumerable<Inspiration>>.FailOnCancellation();

            try
            {
                var response = await GetApi(credentialsContext).GetAssistantInspirationV1RequestBuilderWithAnalytics()
                    .BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult<IEnumerable<Inspiration>>.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("refreshing inspirations")), FormatApiResponse(response)));
                }

                if (response.Data == null)
                    return BackendResult<IEnumerable<Inspiration>>.Success(new List<Inspiration>());

                var data = response.Data.Select(i => new Inspiration()
                {
                    Description = i.Description,
                    Id = i.Id.ToString(),
                    Mode = (Inspiration.ModeEnum)(i.Mode),
                    Value = i.Value
                });

                return BackendResult<IEnumerable<Inspiration>>.Success(data);
            }
            catch (Exception e)
            {
                return BackendResult<IEnumerable<Inspiration>>.FailOnException(GetExceptionErrorMessage("refreshing inspirations"), e);
            }
        }

        public async Task<BackendResult<int>> PointCostRequest(CredentialsContext credentialsContext, string conversationUid, int? contextItems, string prompt,
            CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<int>.FailOnCancellation();

            try
            {
                var response = await GetApi(credentialsContext).GetAssistantMessagePointsV1RequestBuilderWithAnalytics()
                    .SetConversationId(conversationUid).SetContextItems(contextItems).SetPrompt(prompt)
                    .BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult<int>.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("getting point cost")), FormatApiResponse(response)));
                }

                return BackendResult<int>.Success(response.Data.MessagePoints.GetInt());
            }
            catch (Exception e)
            {
                return BackendResult<int>.FailOnException(GetExceptionErrorMessage("getting point cost"), e);
            }
        }

        public async Task<BackendResult> SendFeedback(CredentialsContext credentialsContext,
            string conversationUid,
            MessageFeedback feedback,
            CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<List<VersionSupportInfo>>.FailOnCancellation();

            try
            {
                var response = await GetApi(credentialsContext).PostAssistantFeedbackV1RequestBuilderWithAnalytics(
                        new FeedbackCreationV1(
                            (CategoryV1)feedback.Type,
                            Guid.Parse(conversationUid),
                            feedback.Message,
                            Guid.Parse(feedback.MessageId.FragmentId),
                            (SentimentV1)feedback.Sentiment
                        )
                    )
                    .BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("sending feedback")), FormatApiResponse(response)));
                }

                var feedbackData = new FeedbackData(feedback.Sentiment, feedback.Message);
                k_FeedbackCache[feedback.MessageId] = feedbackData;
                return BackendResult.Success();
            }
            catch (Exception e)
            {
                return BackendResult<List<VersionSupportInfo>>.FailOnException(GetExceptionErrorMessage("sending feedback"), e);
            }
        }

        public async Task<BackendResult<FeedbackData?>> LoadFeedback(CredentialsContext credentialsContext, AssistantMessageId messageId, CancellationToken ct = default)
        {
            if (k_FeedbackCache.TryGetValue(messageId, out var cachedData))
            {
                return BackendResult<FeedbackData?>.Success(cachedData);
            }

            if (ct.IsCancellationRequested)
                return BackendResult<FeedbackData?>.FailOnCancellation();

            try
            {
                var response = await GetApi(credentialsContext)
                    .GetAssistantFeedbackUsingConversationIdAndMessageIdV1RequestBuilderWithAnalytics(
                        messageId.ConversationId.Value,
                        messageId.FragmentId
                    )
                    .BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult<FeedbackData?>.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("loading feedback")), FormatApiResponse(response)));
                }

                var feedbackData = response.Data != null
                    ? new FeedbackData((Sentiment)response.Data.Sentiment, response.Data.Details)
                    : (FeedbackData?)null;

                k_FeedbackCache[messageId] = feedbackData;

                return BackendResult<FeedbackData?>.Success(feedbackData);
            }
            catch (Exception e)
            {
                return BackendResult<FeedbackData?>.FailOnException(GetExceptionErrorMessage("loading feedback"), e);
            }
        }

        /// <inheritdoc />
        public async Task<BackendResult<List<VersionSupportInfo>>> GetVersionSupportInfo(CredentialsContext credentialsContext, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return BackendResult<List<VersionSupportInfo>>.FailOnCancellation();

            try
            {
                ApiResponse<List<Ai.Assistant.Protocol.Model.VersionSupportInfo>> response = null;
                response = await GetApi(credentialsContext).GetVersionsBuilder().BuildAndSendAsync(ct);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    CheckApiResponseForTokenRefreshIssue(response);
                    return BackendResult<List<VersionSupportInfo>>.FailOnServerResponse(new( ErrorHandlingUtility.GetErrorMessageFromHttpResult(
                        (int)response.StatusCode,
                        response.RawContent,
                        GetServerErrorMessage("getting version info")), FormatApiResponse(response)));
                }

                List<VersionSupportInfo> list = response.Data.Select(v => new VersionSupportInfo()
                {
                    RoutePrefix = v.RoutePrefix,
                    SupportStatus = (VersionSupportInfo.SupportStatusEnum)v.SupportStatus
                }).ToList();

                return BackendResult<List<VersionSupportInfo>>.Success(list);
            }
            catch (Exception e)
            {
                return BackendResult<List<VersionSupportInfo>>.FailOnException(GetExceptionErrorMessage("getting version info"), e);
            }
        }

        #endregion

        string GetServerErrorMessage(string action) => $"There was an issue {action} from the server.";
        string GetExceptionErrorMessage(string action) => $"There was an unexpected error when {action}. {ErrorHandlingUtility.ErrorMessageNotNetworkedSuffix}";
        string FormatApiResponse<T>(ApiResponse<T> response) => $"ApiResponse [Status Code: {(int)response.StatusCode} {response.StatusCode}, Content: {response.RawContent}, Data:{response.Data}]";

        void CheckApiResponseForTokenRefreshIssue<T>(ApiResponse<T> response)
        {
            // According to the backend team, when we have a failure due to an expired token, a 401 Unauthorized is
            // reported to the frontend. If this happens we can force a refresh.
            if(response.StatusCode == HttpStatusCode.Unauthorized)
                AccessTokenRefreshUtility.IndicateRefreshMayBeRequired();
        }
    }
}
