using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.ApplicationModels;
using Unity.AI.Assistant.Editor.Backend;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using TextField = UnityEngine.UIElements.TextField;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementResponse : ChatElementBase
    {
        const string k_FeedbackButtonActiveClass = "mui-feedback-button-active";

        static CancellationTokenSource s_FeedbackSendButtonTokenSource;

        readonly IList<VisualElement> m_TextFields = new List<VisualElement>();

        static CancellationTokenSource s_ResponseCopyButtonActiveTokenSource;

        static Texture2D k_UnityAvatarImage;

        Label m_SourcesAttribution;
        Foldout m_SourcesFoldout;
        VisualElement m_SourcesContent;

        VisualElement m_TextFieldRoot;

        VisualElement m_OptionsSection;
        VisualElement m_FeedbackParamSection;

        VisualElement m_ErrorTitle;
        Label m_ErrorTitleLabel;

        Button m_CopyButton;
        AssistantImage m_CopyButtonImage;
        Button m_UpVoteButton;
        Button m_DownVoteButton;

        Toggle m_FeedbackFlagInappropriateCheckbox;
        TextField m_FeedbackText;
        VisualElement m_FeedbackPlaceholderContent;
        Label m_FeedbackPlaceholder;
        bool m_FeedbackTextFocused;

        Button m_FeedbackSendButton;
        Label m_FeedbackSendButtonLabel;
        AssistantImage m_FeedbackSendButtonImage;

        Foldout m_FeedbackCommentFoldout;

        FeedbackEditMode m_FeedbackMode = FeedbackEditMode.None;

        AssistantMessageId m_MessageId;
        static readonly int k_TextAnimationDelay = 500; // in ms
        IVisualElementScheduledItem m_ScheduledAnim;

        static readonly Dictionary<AssistantMessageId, int> k_AnimationIndices = new();

        static AssistantConversationId s_CurrentStoredConversationFeedbacks;
        static readonly Dictionary<AssistantMessageId, FeedbackData> k_StoredFeedbackUIState = new();

        int AnimationIndex
        {
            get
            {
                if(k_AnimationIndices.TryGetValue(m_MessageId, out var index))
                {
                    return index;
                }

                k_AnimationIndices[m_MessageId] = 0;

                return 0;
            }
            set
            {
                // Don't store non-external message IDs:
                if (m_MessageId.Type == AssistantMessageIdType.External)
                {
                    k_AnimationIndices[m_MessageId] = value;
                }
            }
        }

        enum FeedbackEditMode
        {
            None,
            UpVote,
            DownVote
        }

        /// <summary>
        /// Set the data for this response chat element
        /// </summary>
        /// <param name="message">the message to display</param>
        /// <param name="id">the id of the message, used for feedback</param>
        public override void SetData(MessageModel message)
        {
            if (message.Role == MessageModelRole.Error)
            {
                m_ErrorTitle.SetDisplay(true);
                m_ErrorTitleLabel.text = ErrorHandlingUtility.GetErrorTitle();
            }

            if (m_MessageId != message.Id)
            {
                if (k_AnimationIndices.ContainsKey(m_MessageId))
                {
                    k_AnimationIndices.Remove(m_MessageId);
                }
            }

            m_MessageId = message.Id;

            SetCurrentConversation(message.Id.ConversationId);

            base.SetData(message);

            m_FeedbackMode = FeedbackEditMode.None;

            if (message.Id.Type == AssistantMessageIdType.Internal || message.Role == MessageModelRole.Error)
            {
                m_OptionsSection.style.display = DisplayStyle.None;
            }

            RefreshText(m_TextFieldRoot, m_TextFields);
            RefreshSourceBlocks();

            SetupFeedbackParameters();
            RefreshFeedbackParameters();

            if (message.Feedback != null)
            {
                // Feedback returned from backend
                SetFeedback(message.Id, message.Feedback);
                StoreFeedbackUIState(message.Id, message.Feedback.Value);
            }
            else if (k_StoredFeedbackUIState.TryGetValue(Message.Id, out var feedbackData))
            {
                // Feedback cached for current conversation
                SetFeedback(message.Id, feedbackData);
            }

            // Cancel any active animations:
            if (m_ScheduledAnim != null)
            {
                m_ScheduledAnim.Pause();
                m_ScheduledAnim = null;
            }

            // Schedule update to animate text for incomplete messages:
            if (!message.IsComplete)
            {
                GetAnimationInfo(message.Content, out var remainingSpaces, out _);
                var delay = k_TextAnimationDelay / Math.Max(1, remainingSpaces);
                m_ScheduledAnim = schedule.Execute(() =>
                {
                    SetData(message);
                }).StartingIn(delay);
            }

            RemoveCompleteMessageFromAnimationDictionary();
        }

        public override void Reset()
        {
            // If the message is complete, set the animation index to a high value to start the next animation at the last space:
            if (!Message.IsComplete)
            {
                AnimationIndex = int.MaxValue;
            }
        }

        void RemoveCompleteMessageFromAnimationDictionary()
        {
            // No need to keep complete messages in animation data dictionary:
            if (Message.IsComplete && k_AnimationIndices.ContainsKey(m_MessageId))
            {
                k_AnimationIndices.Remove(m_MessageId);
            }
        }

        void GetAnimationInfo(string message, out int remainingSpaces, out int nextSpace)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                nextSpace = 0;
                remainingSpaces = 0;
                return;
            }

            AnimationIndex = Math.Min(AnimationIndex, message.Length - 1);
            nextSpace = message.IndexOf(' ', AnimationIndex);

            remainingSpaces = 0;
            if (nextSpace >= 0)
            {
                remainingSpaces = 0;
                for (var i = nextSpace + 1; i < message.Length; i++)
                {
                    if (message[i] == ' ') remainingSpaces++;
                }

                remainingSpaces = Math.Max(1, remainingSpaces);
            }
        }

        protected override string GetAnimatedMessage(string message)
        {
            if (message.Length > 0)
            {
                GetAnimationInfo(message, out _, out var nextSpace);

                if (nextSpace >= 0)
                {
                    AnimationIndex = nextSpace + 1;
                }

                message = message.Substring(0, AnimationIndex);
            }

            return message;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            LoadSharedAsset(EditorGUIUtility.isProSkin ? "icons/Unity.png" : "icons/UnityDark.png", ref k_UnityAvatarImage);
            view.SetupImage("unityAvatar").SetTexture(k_UnityAvatarImage);

            m_TextFieldRoot = view.Q<VisualElement>("textFieldRoot");

            m_SourcesAttribution = view.Q<Label>("sourcesAttribution");
            m_SourcesFoldout = view.Q<Foldout>("sourcesFoldout");
            m_SourcesFoldout.RegisterValueChangedCallback(_ => OnSourcesFoldoutChanged());

            m_SourcesContent = view.Q<VisualElement>("sourcesContent");

            m_OptionsSection = view.Q<VisualElement>("optionsSection");
            m_CopyButton = view.SetupButton("copyButton", OnCopyClicked);
            m_CopyButtonImage = m_CopyButton.SetupImage("copyButtonImage", "copy");
            m_UpVoteButton = view.SetupButton("upVoteButton", OnUpvoteClicked);
            m_DownVoteButton = view.SetupButton("downVoteButton", OnDownvoteClicked);

            m_FeedbackParamSection = view.Q<VisualElement>("feedbackParamSection");
            m_FeedbackPlaceholderContent = view.Q<VisualElement>("placeholderContent");
            m_FeedbackPlaceholder = view.Q<Label>("placeholderText");

            m_ErrorTitle = view.Q<VisualElement>("errorTitle");
            m_ErrorTitle.SetDisplay(false);

            m_ErrorTitleLabel = view.Q<Label>("errorTitleLabel");
        }

        void SetupFeedbackParameters()
        {
            m_FeedbackFlagInappropriateCheckbox = m_FeedbackParamSection.Q<Toggle>("feedbackFlagCheckbox");

            m_FeedbackText = m_FeedbackParamSection.Q<TextField>("feedbackValueText");
            m_FeedbackText.multiline = true;
            m_FeedbackText.maxLength = AssistantMessageSizeConstraints.FeedbackLimit;
            m_FeedbackText.RegisterValueChangedCallback(_ => CheckFeedbackState());

            m_FeedbackText.RegisterCallback<FocusInEvent>(_ => SetFeedbackTextFocused(true));
            m_FeedbackText.RegisterCallback<FocusOutEvent>(_ => SetFeedbackTextFocused(false));

            m_FeedbackSendButton = m_FeedbackParamSection.SetupButton("feedbackSendButton", OnSendFeedback);
            m_FeedbackSendButtonLabel = m_FeedbackSendButton.Q<Label>();
            m_FeedbackSendButtonImage = m_FeedbackSendButton.SetupImage("feedbackSendButtonImage", "checkmark");
            m_FeedbackSendButtonImage.SetDisplay(false);

            m_FeedbackCommentFoldout = m_FeedbackParamSection.Q<Foldout>("commentFoldout");
            m_FeedbackCommentFoldout.value = false;
            m_FeedbackCommentFoldout.RegisterValueChangedCallback(evt =>
            {
                Context.SendScrollToEndRequest();
            });

            m_FeedbackPlaceholderContent.RegisterCallback<ClickEvent>(_ => m_FeedbackText.Focus());

            CheckFeedbackState();
        }

        private void SetFeedbackTextFocused(bool state)
        {
            m_FeedbackTextFocused = state;

            CheckFeedbackState();
        }

        void CheckFeedbackState()
        {
            m_FeedbackSendButton.SetEnabled(!string.IsNullOrEmpty(m_FeedbackText.value));
            m_FeedbackPlaceholderContent.SetDisplay(!m_FeedbackTextFocused && string.IsNullOrEmpty(m_FeedbackText.value));
        }

        void OnSendFeedback(PointerUpEvent evt)
        {
            if (string.IsNullOrEmpty(m_FeedbackText.value))
            {
                ErrorHandlingUtils.ShowGeneralError($"Failed to send Feedback: 'your feedback' section is empty");
                return;
            }

            string message = m_FeedbackText.value.Trim();

            if (m_FeedbackMode != FeedbackEditMode.DownVote && m_FeedbackMode != FeedbackEditMode.UpVote)
            {
                ErrorHandlingUtils.ShowGeneralError($"Failed to send Feedback: Sentiment must be set");
                return;
            }

            if (m_FeedbackFlagInappropriateCheckbox.value)
            {
                message += " (Message was flagged as inappropriate.)";
            }

            Context.API.SendFeedback(Id, m_FeedbackFlagInappropriateCheckbox.value, message, m_FeedbackMode == FeedbackEditMode.UpVote);

            if (k_StoredFeedbackUIState.TryGetValue(Message.Id, out var feedbackData))
            {
                // Null is intentional since we clear the sent text at this point
                var newFeedbackData = new FeedbackData(feedbackData.Sentiment, null);
                StoreFeedbackUIState(Message.Id, newFeedbackData);
            }

            m_FeedbackSendButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            m_FeedbackSendButtonLabel.text = AssistantUIConstants.FeedbackButtonSentTitle;
            m_FeedbackSendButtonImage.SetDisplay(true);

            ClearFeedbackParameters();

            TimerUtils.DelayedAction(ref s_FeedbackSendButtonTokenSource, () =>
            {
                m_FeedbackSendButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_FeedbackSendButtonLabel.text = AssistantUIConstants.FeedbackButtonDefaultTitle;

                m_FeedbackSendButtonImage.SetDisplay(false);

                m_FeedbackCommentFoldout.value = false;
                m_FeedbackText.value = string.Empty;
                RefreshFeedbackParameters();
            });
        }

        void ClearFeedbackParameters()
        {
            m_FeedbackFlagInappropriateCheckbox.value = false;
            m_FeedbackText.value = string.Empty;
            RefreshFeedbackParameters();
        }

        void OnSourcesFoldoutChanged()
        {
            EditorApplication.delayCall += Context.SendScrollToEndRequest;
        }

        void OnDownvoteClicked(PointerUpEvent evt)
        {
            if (m_FeedbackMode == FeedbackEditMode.DownVote)
            {
                return;
            }

            m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackDownVotePlaceholder;

            Context.API.SendFeedback(Id, m_FeedbackFlagInappropriateCheckbox.value, string.Empty, false);

            var newFeedbackData = new FeedbackData(Sentiment.Negative, m_FeedbackText.value);
            StoreFeedbackUIState(Message.Id, newFeedbackData);

            m_FeedbackMode = FeedbackEditMode.DownVote;
            RefreshFeedbackParameters();
        }

        void OnUpvoteClicked(PointerUpEvent evt)
        {
            if (m_FeedbackMode == FeedbackEditMode.UpVote)
            {
                return;
            }

            m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackUpVotePlaceholder;

            Context.API.SendFeedback(Id, false, string.Empty, true);

            var newFeedbackData = new FeedbackData(Sentiment.Positive, m_FeedbackText.value);
            StoreFeedbackUIState(Message.Id, newFeedbackData);

            m_FeedbackMode = FeedbackEditMode.UpVote;
            RefreshFeedbackParameters();
            m_FeedbackFlagInappropriateCheckbox.value = false;
        }

        void OnCopyClicked(PointerUpEvent evt)
        {
            string disclaimerHeader = string.Format(AssistantConstants.DisclaimerText, DateTime.Now.ToShortDateString());

            // Format message with footnotes (indices to sources)
            IList<SourceBlock> sourceBlocks = new List<SourceBlock>();

            MessageUtils.ProcessText(Message, ref sourceBlocks, out var outMessage,
                MessageUtils.FootnoteFormat.SimpleIndexForClipboard);

            // Add sources in same order of footnote indices
            MessageUtils.AppendSourceBlocks(sourceBlocks, ref outMessage);

            GUIUtility.systemCopyBuffer = string.Concat(disclaimerHeader, outMessage);

            m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            m_CopyButtonImage.SetOverrideIconClass("checkmark");
            TimerUtils.DelayedAction(ref s_ResponseCopyButtonActiveTokenSource, () =>
            {
                m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_CopyButtonImage.SetOverrideIconClass(null);
            });

            AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.CopyResponse, d =>
            {
                d.ConversationId = m_MessageId.ConversationId.Value;
                d.MessageId = m_MessageId.FragmentId;
                d.ResponseMessage = outMessage;
            });
        }

        protected override void HandleLinkClick(LinkType type, string id)
        {
            switch (type)
            {
                case LinkType.Reference:
                {
                    if (!int.TryParse(id, out var sourceId) || SourceBlocks.Count <= sourceId || sourceId < 0)
                    {
                        Debug.LogError("Invalid Source ID: " + sourceId);
                        return;
                    }

                    var sourceBlock = SourceBlocks[sourceId];
                    Application.OpenURL(sourceBlock.source);

                    return;
                }
            }

            base.HandleLinkClick(type, id);
        }

        void RefreshSourceBlocks()
        {
            if (Message.Role == MessageModelRole.Error || !Message.IsComplete || SourceBlocks == null || SourceBlocks.Count == 0)
            {
                m_SourcesFoldout.style.display = DisplayStyle.None;
                return;
            }

            m_SourcesFoldout.style.display = DisplayStyle.Flex;
            m_SourcesContent.Clear();

            // This exists to solve https://jira.unity3d.com/browse/BASST-266 quickly. This should not be the final
            // solution. The source block is extracted from the chat stream as it streamed and sets
            // m_TempBASST266FixSourceAttribution. If not empty, it should be displayed in the sources block.
            var showAttribution = !string.IsNullOrEmpty(Message.SourceAttribution);
            m_SourcesAttribution.SetDisplay(showAttribution);
            if (showAttribution)
            {
                // strip subscript tags to allow our styling
                var attributionText = Message.SourceAttribution
                    .Replace("<sub>", string.Empty)
                    .Replace("</sub>", string.Empty)
                    .Trim();

                m_SourcesAttribution.text = attributionText;
            }

            for (var index = 0; index < SourceBlocks.Count; index++)
            {
                var sourceBlock = SourceBlocks[index];
                var entry = new ChatElementSourceEntry();
                entry.Initialize(Context);
                entry.SetData(index, sourceBlock);
                m_SourcesContent.Add(entry);
            }
        }

        void RefreshFeedbackParameters(bool initialLoadedState = false)
        {
            if (Message.Role == MessageModelRole.Error || !Message.IsComplete)
            {
                m_CopyButton.SetEnabled(false);
                m_UpVoteButton.SetEnabled(false);
                m_DownVoteButton.SetEnabled(false);
                m_FeedbackParamSection.style.display = DisplayStyle.None;
                return;
            }

            m_CopyButton.SetEnabled(true);
            m_UpVoteButton.SetEnabled(true);
            m_DownVoteButton.SetEnabled(true);

            switch (m_FeedbackMode)
            {
                case FeedbackEditMode.None:
                {
                    m_FeedbackParamSection.style.display = DisplayStyle.None;
                    m_UpVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);
                    m_DownVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);
                    return;
                }

                case FeedbackEditMode.DownVote:
                {
                    m_FeedbackParamSection.style.display = DisplayStyle.Flex;
                    m_FeedbackFlagInappropriateCheckbox.style.display = DisplayStyle.Flex;
                    m_UpVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);
                    m_DownVoteButton.AddToClassList(k_FeedbackButtonActiveClass);

                    if (!initialLoadedState)
                        Context.SendScrollToEndRequest();

                    break;
                }

                case FeedbackEditMode.UpVote:
                {
                    m_FeedbackParamSection.style.display = DisplayStyle.Flex;
                    m_FeedbackFlagInappropriateCheckbox.style.display = DisplayStyle.None;
                    m_UpVoteButton.AddToClassList(k_FeedbackButtonActiveClass);
                    m_DownVoteButton.RemoveFromClassList(k_FeedbackButtonActiveClass);

                    if (!initialLoadedState)
                        Context.SendScrollToEndRequest();

                    break;
                }
            }
        }

        void SetFeedback(AssistantMessageId assistantMessageId, FeedbackData? feedbackData)
        {
            if (assistantMessageId != m_MessageId)
                return;

            if (feedbackData == null)
                return;

            if (feedbackData?.Sentiment == Sentiment.Positive)
            {
                m_FeedbackMode = FeedbackEditMode.UpVote;
                m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackUpVotePlaceholder;
            }
            else
            {
                m_FeedbackMode = FeedbackEditMode.DownVote;
                m_FeedbackPlaceholder.text = AssistantUIConstants.FeedbackDownVotePlaceholder;
            }

            m_FeedbackText.value = string.Empty;

            RefreshFeedbackParameters(true);
        }

        static void SetCurrentConversation(AssistantConversationId conversationId)
        {
            if (s_CurrentStoredConversationFeedbacks == conversationId)
                return;

            k_StoredFeedbackUIState.Clear();

            s_CurrentStoredConversationFeedbacks = conversationId;
        }

        void StoreFeedbackUIState(AssistantMessageId assistantMessageId, FeedbackData feedbackData)
        {
            k_StoredFeedbackUIState[assistantMessageId] = feedbackData;
        }
    }
}
