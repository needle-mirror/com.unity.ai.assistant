using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementRunExecutionBlock : CommandDisplayTemplate
    {
        public const string FencedBlockTag = "csx_execute";

        Button m_UndoButton;
        Label m_Title;
        Foldout m_Toggle;
        VisualElement m_Content;
        AssistantImage m_Icon;

        public ChatElementRunExecutionBlock()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        VisualElement m_ExecutionContainer;

        protected override void InitializeView(TemplateContainer view)
        {
            m_Title = view.Q<Label>("runActionTitle");

            m_ExecutionContainer = view.Q<VisualElement>("executionContainer");

            m_UndoButton = view.SetupButton("undoButton", _ => UndoHistoryUtils.OpenHistory());
            m_UndoButton.SetEnabled(false);

            m_Toggle = view.Q<Foldout>("executionDisplayToggle");
            m_Toggle.value = false;
            m_ExecutionContainer.SetDisplay(false);
            m_Toggle.RegisterValueChangedCallback(OnToggleDisplay);

            m_Icon = view.SetupImage("mui-execution-block-icon", "tick");
        }

        internal void SetWarnIcon()
        {
            m_Icon.SetIconClassName("warn");
        }

        void OnToggleDisplay(ChangeEvent<bool> evt)
        {
            m_ExecutionContainer.SetDisplay(evt.newValue);
        }

        public void FormatExecutionResult(ExecutionResult executionResult, VisualElement container)
        {
            if (!executionResult.SuccessfullyStarted)
            {
                var executionEntry = new ChatElementRunExecutionEntry();
                executionEntry.Initialize(Context,false);
                executionEntry.SetLog(new ExecutionLog($"<color={ExecutionResult.WarningTextColor}>{executionResult.ConsoleLogs}</color>", LogType.Error));
                container.Add(executionEntry);

                return;
            }

            var resultLogs = executionResult.Logs;
            if (resultLogs.Count == 0)
            {
                var executionEntry = new ChatElementRunExecutionEntry();
                executionEntry.Initialize(Context,false);
                executionEntry.SetLog(new ExecutionLog("Executed without logs", LogType.Log));
                container.Add(executionEntry);
                return;
            }

            foreach (var executionLog in resultLogs)
            {
                var executionEntry = new ChatElementRunExecutionEntry();
                executionEntry.Initialize(Context,false);
                executionEntry.SetLog(executionLog);
                if (executionLog.LogType == LogType.Error || executionLog.LogType == LogType.Warning)
                {
                    SetWarnIcon();
                }
                container.Add(executionEntry);
            }
        }

        public override void Display(bool isUpdate = false)
        {
            var content = ContentGroups[0].Content;
            if (int.TryParse(content, out var executionId))
            {
                var execution = Context.API.GetRunCommandExecution(executionId);

                m_Title.text = execution.CommandName ?? "Command completed";
                FormatExecutionResult(execution, m_ExecutionContainer);
                m_UndoButton.SetEnabled(execution.SuccessfullyStarted);
            }
        }
    }
}
