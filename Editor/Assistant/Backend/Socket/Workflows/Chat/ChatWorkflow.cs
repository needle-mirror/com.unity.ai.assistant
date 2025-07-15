using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend.Socket.Communication;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models.FromServer;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Workflows.Chat
{
    /// <summary>
    /// The ChatWorkflow manages a single web socket connection to the orchestration backend. It manages the flow, by
    /// providing ways to await that certain events have occured and events for errors and closures (to come).
    ///
    /// Basically, a workflow against the server is State Machine and only certain actions are valid at certain times.
    /// I.E. You need a DiscussionInit before sending a prompt.
    /// </summary>
    class ChatWorkflow : IDisposable
    {
        /// <summary>
        /// If the <see cref="DiscussionInitializationV1"/> is not received after connection within this timeout. Assume
        /// there is a problem and disconnect.
        /// </summary>
        public const float DiscussionInitializationTimeoutMillis = 1000;

        /// <summary>
        /// The conversationId that the workflow is actively working on
        /// </summary>
        public string ConversationId { get; private set; }

        /// <summary>
        /// The current state that the workflow is in. This effects the messages that are valid for the workflow to
        /// send.
        /// </summary>
        public State WorkflowState
        {
            get => m_WorkflowState;
            private set
            {
                if (m_WorkflowState != value)
                    OnWorkflowStateChanged?.Invoke(value);

                m_WorkflowState = value;
            }
        }

        public bool IsCancelled => m_InternalCancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// If the workflow is in the <see cref="State.Closed"/> state, indicates the reason for the closure.
        /// </summary>
        public CloseReason CloseReason { get; private set; }

        /// <summary>
        /// Invoked when a chat response is sent by the server. The workflow tracks the entire message as it streams in.
        /// This event is invoked with the entire message collected so far.
        /// </summary>
        public event Action<ChatResponseFragment> OnChatResponse;

        /// <summary>
        /// Invoked when the <see cref="ChatAcknowledgmentV1"/> is received. Provides the prompt that the server is
        /// using that will be saved into the database.
        /// </summary>
        public event Action<AcknowledgePromptInfo> OnAcknowledgeChat;

        /// <summary>
        /// Invoked when the <see cref="DiscussionInitializationV1"/> is received. Provides the conversation id provided
        /// by the server.
        /// </summary>
        public event Action<string> OnConversationId;

        /// <summary>
        /// Invoked when the workflow is closed. Generally, the workflow closes when the websocket is closed. It is
        /// possible for both the workflow, or the server to initiate the close.
        /// </summary>
        public event Action<CloseReason> OnClose;

        /// <summary>
        /// True, when the workflow has sent a message at least once
        /// </summary>
        public bool MessagesSent { get; private set; } = false;

        /// <summary>
        /// Invoked when the workflow's state changes
        /// </summary>
        public event Action<State> OnWorkflowStateChanged;

        WebSocketFactory m_WebSocketFactory;
        IOrchestrationWebSocket m_WebSocket;
        IFunctionCaller m_FunctionCaller;

        OrchestrationStreamStatusHook m_ActiveStreamStatusHook;
        StringBuilder m_ActiveStreamStringBuilder = new();

        CancellationTokenSource m_InternalCancellationTokenSource = new();
        CancellationTokenSource m_ChatRequestCancellationTokenSource;

        float m_ChatTimeoutMilliseconds;
        State m_WorkflowState = State.NotStarted;

        public ChatWorkflow(string conversationId = null, WebSocketFactory websocketFactory = null, IFunctionCaller functionCaller = null)
        {
            ConversationId = conversationId;
            m_WebSocketFactory = websocketFactory ?? DefaultSocketFactory;
            m_FunctionCaller = functionCaller;
            // Make wrapper for non async functions
            IOrchestrationWebSocket DefaultSocketFactory(string uri) => new OrchestrationWebSocket(uri);
        }

        /// <summary>
        /// Start the workflow by connecting to the given uri
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="bearerToken">The bearer token to use when opening the webSocket. If null</param>
        /// <param name="orgId">The org</param>
        /// <exception cref="InvalidOperationException">Workflows can only be started once</exception>
        public async Task Start(string uri, CredentialsContext credentialsContext)
        {
            if (WorkflowState != State.NotStarted)
                throw new InvalidOperationException("The workflow has already been started");
            WorkflowState = State.AwaitingDiscussionInitialization;

            var headers = credentialsContext.Headers;

            Dictionary<string, string> queryParams = new();
            if(!string.IsNullOrEmpty(ConversationId))
                queryParams.Add("conversation_id", ConversationId);

            m_WebSocket = m_WebSocketFactory(uri);
            m_WebSocket.OnClose += HandleWebsocketClosed;
            m_WebSocket.OnMessageReceived += ProcessReceiveResult;

            // Attempt to connect to the websocket and close immediately if this fails
            m_ChatRequestCancellationTokenSource = new();
            var cancelToken = m_ChatRequestCancellationTokenSource.Token;
            IOrchestrationWebSocket.Options options = new() { Headers = headers, QueryParameters = queryParams };
            var result = await m_WebSocket.Connect(options, cancelToken);

            if (!result.IsConnectedSuccessfully)
            {
                if (IsCancelled)
                {
                    InternalLog.Log("Workflow ignores non-successful connection. Workflow was already cancelled.");
                    return;
                }

                AccessTokenRefreshUtility.IndicateRefreshMayBeRequired();
                m_WebSocket.Dispose();
                OnClose?.Invoke(new CloseReason()
                {
                    Reason = cancelToken.IsCancellationRequested ? CloseReason.ReasonType.ClientCanceled : CloseReason.ReasonType.CouldNotConnect,
                    Info = $"Failed to connect to websocket: uri: {options.ConstructUri(uri)}, headers: {headers.Aggregate(new StringBuilder() , (builder, next) => builder.Append($"{next.Key}: {next.Value}, "))}"
                });

                // Make sure AwaitDiscussionInitialization stops polling:
                m_InternalCancellationTokenSource.Cancel();
                return;
            }

            CancellationTokenSource discussionInitTimeout = new(TimeSpan.FromMilliseconds(DiscussionInitializationTimeoutMillis));

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            WaitForDiscussionInit().WithExceptionLogging();
#pragma warning restore CS4014

            async Task WaitForDiscussionInit()
            {
                while (!CheckWorkflowIsOneOfStates(State.Idle, State.AwaitingChatAcknowledgement, State.AwaitingChatResponse, State.ProcessingStream, State.Closed))
                {
                    if (discussionInitTimeout.IsCancellationRequested)
                    {
                        await DisconnectFromServer(new CloseReason
                        {
                            Reason = CloseReason.ReasonType.DiscussionInitializationTimeout,
                        }).WithExceptionLogging();

                        return;
                    }

                    await DelayUtility.ReasonableResponsiveDelay();
                }
            }
        }

        void HandleWebsocketClosed(WebSocketCloseStatus? obj)
        {
            Dispose();

            // Handle cases where the websocket closes and we don't know why. This function should be deregistered if
            // the workflow decides to close the socket.
            OnClose?.Invoke(new CloseReason()
            {
                Reason = CloseReason.ReasonType.UnderlyingWebSocketWasClosed,
                Info = $"Status reported by underlying websocket: {obj}"
            });
        }

        void ProcessReceiveResult(ReceiveResult result)
        {
            if (!result.IsDeserializedSuccessfully)
            {
                DisconnectBecauseMessageIsUnknown();
                return;
            }

            if (result.DeserializedData is ServerDisconnectV1 serverDisconnect)
            {
                HandleServerDisconnect(serverDisconnect);
                return;
            }

            if (m_ChatRequestCancellationTokenSource.IsCancellationRequested)
            {
                InternalLog.LogWarning("Skipping message - cancellation requested.");
                DisconnectBecauseMessageSentAtWrongTime();
                return;
            }
            var message = result.DeserializedData;
            switch (WorkflowState)
            {
                // Before the DiscussionInitializationV1 is received, nothing else is valid from the server
                case State.AwaitingDiscussionInitialization:
                {
                    if (message is not DiscussionInitializationV1 discussionInitializationV1)
                        DisconnectBecauseMessageSentAtWrongTime();
                    else
                        HandleDiscussionInitializationV1(discussionInitializationV1);
                    break;
                }
                // Before the user makes a chat request, the server can still make calls
                case State.Idle:
                {
                    Action action = result.DeserializedData switch
                    {
                        CapabilitiesRequestV1 cr => () => HandleCapabilitiesRequestV1(cr).WithExceptionLogging(),
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
                // Before a chat has been acknowledged, many things are valid
                case State.AwaitingChatAcknowledgement:
                {
                    Action action = result.DeserializedData switch
                    {
                        CapabilitiesRequestV1 cr => () => HandleCapabilitiesRequestV1(cr).WithExceptionLogging(),
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        ChatAcknowledgmentV1 ca => () => HandleChatAcknowledgmentV1(ca).WithExceptionLogging(),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
                // Before a stream response stream starts coming back from the server, many things are valid
                case State.AwaitingChatResponse:
                {
                    Action action = result.DeserializedData switch
                    {
                        CapabilitiesRequestV1 cr => () => HandleCapabilitiesRequestV1(cr).WithExceptionLogging(),
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        ChatResponseV1 crf => () => HandleChatResponseFragmentV1(crf),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
                // Once a response is being streamed, we only support getting function calls or response fragments
                case State.ProcessingStream:
                {
                    Action action = result.DeserializedData switch
                    {
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        ChatResponseV1 crf => () => HandleChatResponseFragmentV1(crf),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
            }

            void DisconnectBecauseMessageSentAtWrongTime()
            {
                DisconnectFromServer(new CloseReason()
                {
                    Reason = CloseReason.ReasonType.ServerSentMessageAtWrongTime,
                    Info = $"State: {WorkflowState}, Model: {result.RawData}, Cancellation: {m_ChatRequestCancellationTokenSource?.IsCancellationRequested}"
                }).WithExceptionLogging();
            }

            void DisconnectBecauseMessageIsUnknown()
            {
                DisconnectFromServer(new CloseReason()
                {
                    Reason = CloseReason.ReasonType.ServerSentUnknownMessage,
                    Info = $"The ChatWorkflow received unknown message. Raw data: {result.RawData}\nThe " +
                           $"workflow was in the state: {WorkflowState}",
                    Exception = result.Exception
                }).WithExceptionLogging();
            }
        }

        public async Task<IStreamStatusHook> SendEditRunCommandRequest(string messageId, string updatedRunCommand)
        {
            if (WorkflowState != State.Idle)
                throw new InvalidOperationException(
                    "A edit request cannot be made if waiting for a discussion initialization or processing a stream");

            await m_WebSocket.Send(
                new EditRunCommandRequestV1()
                {
                    MessageId = messageId,
                    UpdatedRunCommand = updatedRunCommand
                }, CancellationToken.None);

            m_ActiveStreamStatusHook = new(ConversationId);
            return m_ActiveStreamStatusHook;
        }

        /// <summary>
        /// Send a <see cref="ChatRequestV1"/> to the server, so that it being the process of generating a response
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="context"></param>
        /// <param name="ct"></param>
        /// <exception cref="InvalidOperationException">Thrown if a chat request is made when the workflow is not in the <see cref="State.AwaitingChatResponse"/> state</exception>
        /// <returns></returns>
        public async Task<IStreamStatusHook> SendChatRequest(string prompt, List<ChatRequestV1.AttachedContextModel> context,
            CancellationToken ct)
        {
            if (!CheckWorkflowIsOneOfStates(State.Idle))
                throw new InvalidOperationException("A chat request cannot be made unless the workflow is idle or in the initial connection handshake");

            MessagesSent = true;
            // Currently, the server returns fragments, but the UX expects a cumulative response.
            m_ActiveStreamStringBuilder.Clear();

            WorkflowState = State.AwaitingChatAcknowledgement;

            await m_WebSocket.Send(new ChatRequestV1
            {
                Markdown = prompt,
                AttachedContext = context
            }, ct);

            m_ChatRequestCancellationTokenSource = new();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            // Setup a parallel task that will cancel the chat request if the user asks for it
            ParallelCancel().WithExceptionLogging();

            // Setup a parallel task that will cancel the workflow if the time to first response token is too long
            ParallelTimeout().WithExceptionLogging();
#pragma warning restore CS4014

            // TODO: IStreamStatusHook is still part of integrating with the legacy system. This will likely change into something more websocket appropriate
            m_ActiveStreamStatusHook = new(ConversationId);
            return m_ActiveStreamStatusHook;

            async Task ParallelTimeout()
            {
                CancellationTokenSource timeoutCancellation = new(TimeSpan.FromMilliseconds(m_ChatTimeoutMilliseconds));
                CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCancellation.Token,
                    m_InternalCancellationTokenSource.Token,
                    m_ChatRequestCancellationTokenSource.Token
                );

                while (!cancel.IsCancellationRequested && WorkflowState != State.ProcessingStream)
                    await DelayUtility.ReasonableResponsiveDelay();

                // check to see if cancellation was internal (meaning that it was not the timeout, just return)
                if(m_InternalCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // if it's a chat request cancellation, then no need to deal with timeouts anymore.
                if(m_ChatRequestCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // if a timeout occurs, this is a reason to disconnect
                if (timeoutCancellation.IsCancellationRequested)
                    await DisconnectFromServer(new CloseReason()
                    {
                        Reason = CloseReason.ReasonType.ChatResponseTimeout,
                        Info = $"Timeout: {m_ChatTimeoutMilliseconds}"
                    }).WithExceptionLogging();
            }

            async Task ParallelCancel()
            {
                CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(
                    m_InternalCancellationTokenSource.Token,
                    m_ChatRequestCancellationTokenSource.Token
                );

                while (!cancel.IsCancellationRequested)
                    await DelayUtility.ReasonableResponsiveDelay();

                // check to see if cancellation was internal (something else happened and waiting for cancellation
                // isn't necessary anymore)
                if(m_InternalCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // if a cancellation is actually requested, send a cancellation message to the server
                if (m_ChatRequestCancellationTokenSource.IsCancellationRequested)
                {
                    await m_WebSocket.Send(new CancelChatRequestV1(), m_InternalCancellationTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// Try to cancel the current chat request. If there is no current chat request in progress, simply does
        /// nothing. Cancellation will only work after a <see cref="ChatRequestV1"/> has been sent and before the first
        /// <see cref="ChatResponseV1"/> has been received.
        /// </summary>
        public void CancelCurrentChatRequest()
        {
            if (!CheckWorkflowIsOneOfStates(State.AwaitingDiscussionInitialization, State.AwaitingChatAcknowledgement, State.AwaitingChatResponse, State.ProcessingStream))
                return;

            WorkflowState = State.Canceling;

            // This should never happen. It's possible that this should be an exception, but let us be permissive until
            // it's a problem.
            if (m_ChatRequestCancellationTokenSource == null)
                return;

            m_ChatRequestCancellationTokenSource.Cancel();
        }

        // TODO: AwaitDiscussionInitialization should not return a bool. It is trying to give information that can be determined by checking the state of the ChatWorkflow. This bool is redundant, and confusing because it seems like we should react to it. But what we should react to is changes in ChatWorkflow state, which can be determined by checking state immediately after this function is returned.
        /// <summary>
        /// Waits for the <see cref="DiscussionInitializationV1"/> to be received by the workflow.
        /// </summary>
        /// <returns>True if the <see cref="DiscussionInitializationV1"/> is received on time. Returns false if
        /// something goes wrong</returns>
        public async Task<bool> AwaitDiscussionInitialization()
        {
            while (!CheckWorkflowIsOneOfStates(State.Idle, State.AwaitingChatAcknowledgement, State.AwaitingChatResponse, State.ProcessingStream, State.Closed))
            {
                if(m_InternalCancellationTokenSource.IsCancellationRequested)
                    return false;

                if (m_ChatRequestCancellationTokenSource.IsCancellationRequested)
                    return false;

                await DelayUtility.ReasonableResponsiveDelay();
            }

            return !m_InternalCancellationTokenSource.IsCancellationRequested;
        }

        void HandleDiscussionInitializationV1(DiscussionInitializationV1 message)
        {
            ConversationId = message.ConversationId;
            OnConversationId?.Invoke(message.ConversationId);

            m_ChatTimeoutMilliseconds = message.ChatTimeoutSeconds * 1000;

            AssistantMessageSizeConstraints.SetMessageSizeLimit(message.MaxMessageSize);
            WorkflowState = State.Idle;
        }

        async Task HandleChatAcknowledgmentV1(ChatAcknowledgmentV1 ca)
        {
            AcknowledgePromptInfo info = new()
            {
                Id = ca.MessageId,
                Content = ca.Markdown,
                Context = ca.AttachedContextMetadata.Select(c =>
                {
                    return new AssistantContextEntry()
                    {
                        DisplayValue = c.DisplayValue,
                        EntryType = (AssistantContextType)c.EntryType,
                        Value = c.Value,
                        ValueIndex = c.ValueIndex,
                        ValueType = c.ValueType
                    };
                }).ToArray()
            };

            WorkflowState = State.AwaitingChatResponse;
            OnAcknowledgeChat?.Invoke(info);
            await Task.CompletedTask;
        }

        void HandleFunctionCallRequestV1(FunctionCallRequestV1 message)
        {
            if (m_FunctionCaller == null)
            {
                InternalLog.LogError($"ChatWorkflow instance does not have a {m_FunctionCaller} set. The recieved" +
                                     $"FunctionCallRequestV1 cannot be handled");
                return;
            }

            m_FunctionCaller.CallByLLM(this, message.FunctionId, message.FunctionParameters, message.CallId);
        }

        public void SendFunctionCallResponse(IFunctionCaller.CallResult result, Guid callId)
        {
            m_WebSocket.Send(new FunctionCallResponseV1
            {
                CallId = callId,
                FunctionResult = result.Result,
                Success = result.IsFunctionCallSucceeded
            }, m_InternalCancellationTokenSource.Token);
        }

        async Task HandleCapabilitiesRequestV1(CapabilitiesRequestV1 message)
        {
            var launchButtons = OrchestrationUtilities.GetPlugins();

            var buttons = launchButtons.Functions.Count > 0
                ? new List<CapabilitiesResponseV1.LaunchButtonObject>() { launchButtons }
                : new();

            await m_WebSocket.Send(new CapabilitiesResponseV1()
            {
                Functions = OrchestrationUtilities.GetFunctions(),
                Outputs = buttons
            }, default);
        }

        void HandleChatResponseFragmentV1(ChatResponseV1 message)
        {
            m_ActiveStreamStringBuilder.Append(message.Markdown);

            if (message.LastMessage)
                WorkflowState = State.Idle;
            else
                WorkflowState = State.ProcessingStream;

            OnChatResponse?.Invoke(new ChatResponseFragment()
            {
                Fragment = message.Markdown,
                Id = message.MessageId,
                IsLastFragment = message.LastMessage
            });

            m_ActiveStreamStatusHook.ProcessStatusFromResponse(message, m_ActiveStreamStringBuilder.ToString());
        }

        void HandleServerDisconnect(ServerDisconnectV1 serverDisconnect)
        {
            CloseReason reason = new() { Reason = CloseReason.ReasonType.ServerDisconnected };

            if (serverDisconnect.DisconnectReason.IsHappyPathModel)
                reason.Info = "Happy Path";

            if (serverDisconnect.DisconnectReason.IsNoCapacity)
                reason.Info = "No Capacity";

            if (serverDisconnect.DisconnectReason.IsCriticalError)
                reason.Info = serverDisconnect.DisconnectReason.CriticalError.UserMessage;

            if (serverDisconnect.DisconnectReason.IsInfoDisconnect)
                reason.Info = serverDisconnect.DisconnectReason.InfoDisconnect.UserMessage;

            DisconnectFromServer(reason).WithExceptionLogging();
        }

        internal void LocalDisconnect()
        {
            CloseReason reason = new() { Reason = CloseReason.ReasonType.ClientCanceled, Info = "Happy Path" };
            DisconnectFromServer(reason).WithExceptionLogging();
        }

        async Task DisconnectFromServer(CloseReason reason)
        {
            CloseReason = reason;

            // Since the client is disconnecting, do not catch the close event. Instead, handle it here.
            m_WebSocket.OnClose -= HandleWebsocketClosed;
            m_WebSocket.OnMessageReceived -= ProcessReceiveResult;

            // Determine and send disconnect packet
            switch (reason.Reason)
            {
                case CloseReason.ReasonType.CouldNotConnect:
                    break;
                case CloseReason.ReasonType.UnderlyingWebSocketWasClosed:
                    break;
                case CloseReason.ReasonType.ChatResponseTimeout:
                    var timeoutMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromTimeoutModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.TimeoutModel()
                        )
                    };
                    await m_WebSocket.Send(timeoutMessage, default);
                    break;
                case CloseReason.ReasonType.ServerSentUnknownMessage:
                    var unknownMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromInvalidMessageModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.InvalidMessageModel()
                            {
                                InvalidMessage = reason.Info
                            }
                        )
                    };
                    await m_WebSocket.Send(unknownMessage, default);
                    break;
                case CloseReason.ReasonType.ServerSentMessageAtWrongTime:
                    var wrongTimeMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromInvalidMessageOrderModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.InvalidMessageOrderModel() { }
                        )
                    };
                    await m_WebSocket.Send(wrongTimeMessage, default);
                    break;
                case CloseReason.ReasonType.DiscussionInitializationTimeout:
                    var disInitMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromTimeoutModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.TimeoutModel()
                        )
                    };
                    await m_WebSocket.Send(disInitMessage, default);
                    break;
                case CloseReason.ReasonType.ServerDisconnected:
                    // When the server disconnected, don't need to send a disconnect packet
                    break;
            }

            // Close the websocket
            Dispose();

            // Signal closure
            OnClose?.Invoke(reason);
        }

        // Todo - hook this up to the actual server response when we get one
        internal void HandleCancellationResponse()
        {
            if (m_WorkflowState == State.Canceling)
                m_WorkflowState = State.Idle;
            else
            {
                CloseReason reason = new() { Reason = CloseReason.ReasonType.ServerSentMessageAtWrongTime, Info = "Server acknowledged a cancel when a cancellation was not expected." };
                DisconnectFromServer(reason).WithExceptionLogging();
            }
        }

        public void Dispose()
        {
            WorkflowState = State.Closed;

            // Indicate to all other internal tasks that the workflow is canceled.
            m_InternalCancellationTokenSource.Cancel();

            m_WebSocket?.Dispose();
        }

        bool CheckWorkflowIsOneOfStates(params State[] states) => states.Any(s => WorkflowState == s);
    }
}
