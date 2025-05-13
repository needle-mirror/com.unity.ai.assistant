using System;
using Unity.AI.Assistant.Editor.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ProgressElement : ManagedTemplate
    {
        const string k_AssistantProgressTime = "ASSISTANT_PROGRESS_TIME";

        float k_ProgressTime = SessionState.GetFloat(k_AssistantProgressTime, 10);

        ProgressBar m_ProgressBar;
        VisualElement m_ProgressBarOverlay;
        Label m_DetailMessage;
        bool m_Running;
        ValueAnimation<float> m_ProgressAnimation;

        const string k_ProgressCompleteMessage = "Almost ready";
        const string k_ProgressMessage = "Preparing";
        const string k_ProcessingMessage = "Processing request";
        const string k_AnalyzingMessage = "Analyzing project context";
        const string k_RefiningMessage = "Refining code";
        const string k_CancelingMessage = "Canceling";

        readonly string[] k_DefaultStateStrings = { k_ProgressMessage, k_ProcessingMessage, k_AnalyzingMessage };
        readonly string[] k_CodeRepairStateStrings = { k_ProgressMessage, k_RefiningMessage };

        public ProgressElement()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public void Start()
        {
            if (m_Running)
            {
                return;
            }

            m_Running = true;
            m_ProgressBar.value = 0;

            EditorApplication.update += UpdateProgress;
            AnimateProgressBar();
            UpdateProgress();
            Show();
        }

        public void Stop()
        {
            if (!m_Running)
            {
                return;
            }

            m_Running = false;
            EditorApplication.update -= UpdateProgress;

            // If the response started streaming, update progress time for next time:
            if (Context.Blackboard.IsAPIStreaming)
            {
                var activeConversation = Context.Blackboard.ActiveConversation;
                if (activeConversation != null)
                {
                    var startTime = activeConversation.StartTime;
                    var timeTaken = (float)(EditorApplication.timeSinceStartup - startTime);
                    // Never let it get too big, could be delayed if there are breakpoints or other long running tasks:
                    k_ProgressTime = Mathf.Min(100, (k_ProgressTime + timeTaken) / 2);

                    SessionState.SetFloat(k_AssistantProgressTime, k_ProgressTime);
                }
            }

            if (m_ProgressAnimation != null)
            {
                // Avoid starting a new animation when stopping:
                m_ProgressAnimation.onAnimationCompleted = null;
                m_ProgressAnimation.Stop();
                m_ProgressAnimation = null;

                // Reset progress bar offset:
                var pos = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Left));
                var p = new BackgroundPosition { keyword = BackgroundPositionKeyword.Left };
                p.offset.value = 0;
                pos.value = p;
                m_ProgressBarOverlay.style.backgroundPositionX = pos;
            }

            Hide();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ProgressBar = view.Q<ProgressBar>("assistantProgressBar");
            m_DetailMessage = view.Q<Label>("assistantProgressMessage");

            m_ProgressBarOverlay = view.Q(className: "unity-progress-bar__progress");

            Context.Blackboard.ActiveConversationChanged += OnActiveConversationChanged;
            Context.API.APIStateChanged += OnAPIStateChanged;
        }

        void OnActiveConversationChanged(AssistantConversationId previousId, AssistantConversationId newId)
        {
            // For now we just assume to stop progress until we receive an API state change
            // Note: Multiple conversations could have active running api states, this is not yet supported properly
            if (previousId.IsValid)
            {
                // We are switching from a valid conversation to another one, remove progress until state change
                // We don't know yet if the new conversation is in progress
                Stop();
            }
        }

        void OnAPIStateChanged()
        {
            if (Context.Blackboard.IsAPIWorking || Context.Blackboard.IsAPICanceling)
            {
                if (!m_Running)
                {
                    Start();
                }
                else
                {
                    UpdateProgress();
                }
            }
            else
            {
                Stop();
            }
        }

        void UpdateMessage()
        {
            string message;
            var progress = GetProgress();
            if (Context.Blackboard.IsAPICanceling)
            {
                message = k_CancelingMessage;
            }
            else if (progress >= 100)
            {
                message = Context.Blackboard.IsAPIRepairing ? k_RefiningMessage : k_ProgressCompleteMessage;
            }
            else
            {
                string[] stringsForState = k_DefaultStateStrings;

                if (Context.Blackboard.IsAPIRepairing)
                {
                    stringsForState = k_CodeRepairStateStrings;
                }

                var index = Math.Min(stringsForState.Length, (int)(progress / 100f * stringsForState.Length));
                message = stringsForState[index];
            }

            m_DetailMessage.text = message;
        }

        void UpdateProgress()
        {
            UpdateMessage();

            m_ProgressBar.value = Mathf.Min(100, GetProgress());
        }

        float GetProgress()
        {
            var conversation = Context.Blackboard.ActiveConversation;
            if (conversation == null)
            {
                return 0;
            }

            // Init the start time if needed:
            if (conversation.StartTime < 1)
                conversation.StartTime = EditorApplication.timeSinceStartup;

            var deltaTime = EditorApplication.timeSinceStartup - conversation.StartTime;

            return (float)(deltaTime / k_ProgressTime) * 100;
        }

        void AnimateProgressBar()
        {
            var x = m_ProgressBarOverlay.style.backgroundPositionX.value.offset.value;

            m_ProgressAnimation = m_ProgressBarOverlay.experimental.animation.Start(x, x + 1000, 10000, (element, f) =>
            {
                var pos = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Left));
                var p = new BackgroundPosition { keyword = BackgroundPositionKeyword.Left };
                p.offset.value = f;
                pos.value = p;
                m_ProgressBarOverlay.style.backgroundPositionX = pos;
            }).Ease(Easing.Linear).OnCompleted(AnimateProgressBar);
        }
    }
}
