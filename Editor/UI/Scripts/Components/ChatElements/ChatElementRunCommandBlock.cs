using System;
using System.IO;
using System.Linq;
using Unity.AI.Assistant.Editor.Agent;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementRunCommandBlock : CommandDisplayTemplate
    {
        const int k_RunCommandContentIndex = 0;

        public static Action<string, string> OnDevToolClicked;
        public static Action<VisualElement, string, ConversationModel> OnRunCommandBlockSetup;

        Label m_Title;
        Button m_ExecuteButton;
        Button m_DebugButton;
        CodeBlockElement m_CodePreview;
        VisualElement m_ActionBlockRoot;
        VisualElement m_PreviewContainer;
        VisualElement m_WarningContainer;
        Label m_WarningText;
        Foldout m_OverviewFoldout;
        VisualElement m_TaskBlockInner;
        Label m_TaskMessage;
        AssistantImage m_TaskWarningIcon;

        AgentRunCommand m_RunCommand;

        public ChatElementRunCommandBlock()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Title = view.Q<Label>("actionTitle");
            m_Title.text = "New command";

            m_OverviewFoldout = view.Q<Foldout>("overviewFoldout");
            m_OverviewFoldout.pickingMode = PickingMode.Ignore;
            m_OverviewFoldout.toggleOnLabelClick = false;

            m_WarningContainer = view.Q<VisualElement>("warningContainer");
            m_WarningContainer.SetDisplay(false);
            m_WarningText = view.Q<Label>("warningText");

            view.SetupImage("executeButtonIcon");
            m_ExecuteButton = view.SetupButton("executeButton", OnExecuteCodeClicked);
            m_ExecuteButton.SetEnabled(false);

            var devToolButton = view.Q<Button>("devToolButton");
            devToolButton.SetDisplay(false);

            if (OnDevToolClicked != null)
                InitializeDevTool(devToolButton);

            var codePreviewRoot = view.Q<VisualElement>("codePreviewRoot");
            m_CodePreview = new CodeBlockElement();
            m_CodePreview.Initialize(Context);
            m_CodePreview.SetActions(true, false, true, true);
            m_CodePreview.EnableAnalytics();
            m_CodePreview.OnCodeChanged += UpdateCommandCode;

            codePreviewRoot.Add(m_CodePreview);

            m_PreviewContainer = view.Q<VisualElement>("previewItems");
            m_ActionBlockRoot = view.Q<VisualElement>(className: "mui-action-block-root");

            m_TaskBlockInner = view.Q<VisualElement>("taskBlockInner");

            m_TaskMessage = view.Q<Label>("taskMessage");

            m_TaskWarningIcon = view.SetupImage("taskWarningIcon");
            m_TaskWarningIcon.SetDisplay(false);

            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    RefreshDisplayOnExitPlayMode();
            };
        }

        internal new void SetMessage(MessageModel message)
        {
            base.SetMessage(message);

            OnRunCommandBlockSetup?.Invoke(m_ActionBlockRoot, m_ParentMessage.Id.FragmentId, Context.Blackboard.ActiveConversation);
        }

        void InitializeDevTool(Button devToolButton)
        {
            devToolButton.SetDisplay(true);
            devToolButton.clicked += () =>
            {
                string userQuery = string.Empty;
                var conversation = Context.Blackboard.ActiveConversation;
                for (var i = conversation.Messages.Count - 1; i >= 1; i--)
                {
                    var message = conversation.Messages[i];
                    if (message.Id == m_ParentMessage.Id)
                    {
                        userQuery = conversation.Messages[i - 1].Content;
                        break;
                    }
                }

                OnDevToolClicked.Invoke(userQuery, ContentGroups[k_RunCommandContentIndex].Content);
            };
        }

        void UpdateCommandCode(string updatedCode)
        {
            ContentGroups[k_RunCommandContentIndex].Content = updatedCode;
            Validate(k_RunCommandContentIndex);
            Display(true);

            updatedCode = String.Concat("```csx\n", updatedCode, "\n```");

            Context.API.SendEditRunCommand(m_ParentMessage.Id, updatedCode);
        }

        protected override bool ValidateInternal(int index, out string logs)
        {
            m_RunCommand = Context.API.BuildAgentRunCommand(ContentGroups[index].Content, Context.Blackboard.ObjectAttachments);
            if (m_RunCommand == null)
            {
                logs = "No command present";
                return false;
            }
            logs = m_RunCommand.CompilationErrors.ToString();
            return m_RunCommand.CompilationSuccess;
        }

        void RefreshDisplayOnExitPlayMode()
        {
            if (m_RunCommand == null)
            {
                m_ExecuteButton.SetEnabled(false);
                return;
            }

            if (!m_RunCommand.RequiredMonoBehaviours.Any() && m_RunCommand.PreviewIsDone)
            {
                var content = ContentGroups[k_RunCommandContentIndex];

                if (content.State == DisplayState.Success)
                {
                    m_ExecuteButton.SetEnabled(true);
                }
            }
        }

        public override void Display(bool isUpdate = false)
        {
            var content = ContentGroups[k_RunCommandContentIndex];

            switch (content.State)
            {
                case DisplayState.Success:
                    DisplayCommandPreview(isUpdate);
                    break;
                case DisplayState.Fail:
                    DisplayCompilationWarning();
                    break;
            }

        }

        void DisplayCompilationWarning()
        {
            m_PreviewContainer.Clear();

            //Still display the code
            m_TaskMessage.text = "Unable to generate tasks";
            m_CodePreview.SetCodePreviewTitle("Failed command attempt");
            m_CodePreview.SetTitleIcon();
            m_TaskWarningIcon.SetDisplay(true);
            m_CodePreview.Show();
            m_CodePreview.SetCode(m_RunCommand.Script);

            m_WarningContainer.SetDisplay(true);
            m_ExecuteButton.SetEnabled(false);


            if (m_RunCommand.HasUnauthorizedNamespaceUsage())
                m_WarningText.text =  "A script was generated that triggers an unauthorized API. As a safety precaution, this is not permitted.";
            else
                m_WarningText.text =  "Avoid complex commands, and try to split large requests into smaller steps. Only attach relevant items.";

            m_CodePreview.DisplayErrors(m_RunCommand.CompilationErrors);
        }

        void DisplayCommandPreview(bool isUpdate)
        {
            m_PreviewContainer.Clear();
            m_WarningContainer.SetDisplay(false);
            m_TaskMessage.text = "Tasks";

            if (m_RunCommand.Unsafe)
                DisplayUnsafeWarning();

            m_Title.text = m_RunCommand.Description;

            // Update Code preview text with latest code
            m_CodePreview.SetCodePreviewTitle("Command logic");

            if (!isUpdate)
                m_CodePreview.SetToggle(false);

            m_CodePreview.Show();
            m_CodePreview.SetCode(m_RunCommand.Script);

            if (!m_RunCommand.PreviewIsDone)
            {
                // Update preview content
                m_RunCommand.BuildPreview(out var previewBuilder);

                if (m_RunCommand.RequiredMonoBehaviours.Any())
                {
                    foreach (var requiredComponent in m_RunCommand.RequiredMonoBehaviours)
                        AddTaskSaveNewComponent(requiredComponent);
                }

                foreach (var previewLine in previewBuilder.Preview)
                {
                    var commandEntry = new ChatElementRunCommandEntry();
                    commandEntry.Initialize(Context, false);
                    commandEntry.SetCommand(previewLine, m_RunCommand);

                    m_PreviewContainer.Add(commandEntry);
                }

                if (!EditorApplication.isPlaying && !m_RunCommand.RequiredMonoBehaviours.Any())
                {
                    m_ExecuteButton.SetEnabled(true);
                    m_TaskWarningIcon.SetDisplay(false);
                    m_CodePreview.SetTitleIcon(false);
                    m_CodePreview.ClearDisplayedErrors();
                }
            }
        }

        void AddTaskSaveNewComponent(ClassCodeTextDefinition requiredComponent)
        {
            var commandEntry = new ChatElementRunCommandEntry();
            commandEntry.Initialize(Context, false);
            commandEntry.SetCommand($"A new C# component <b>{requiredComponent.ClassName}</b> is required to perform this command.", m_RunCommand);
            commandEntry.RegisterAction(() =>
            {
                string file = EditorUtility.SaveFilePanel("Save new Component", Application.dataPath, requiredComponent.ClassName, "cs");
                if (!string.IsNullOrEmpty(file))
                {
                    File.WriteAllText(file, requiredComponent.Code);
                    AssetDatabase.Refresh();
                    EditorUtility.RequestScriptReload();
                }
            });

            m_PreviewContainer.Add(commandEntry);
        }

        void DisplayUnsafeWarning()
        {
            m_WarningContainer.SetDisplay(true);
            m_WarningText.text =  "This command is performing operations that cannot be undone.";
        }

        void OnExecuteCodeClicked(PointerUpEvent evt)
        {
            ExecuteCommand();

            AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.ExecuteRunCommand, d =>
            {
                d.MessageId = m_ParentMessage.Id.FragmentId;
                d.ConversationId = m_ParentMessage.Id.ConversationId.Value;
                d.ResponseMessage = m_RunCommand.Script;
            });
        }

        void ExecuteCommand()
        {
            Context.API.RunAgentCommand(m_RunCommand);
        }
    }
}
