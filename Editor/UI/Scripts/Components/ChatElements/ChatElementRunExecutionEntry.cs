using System.Text.RegularExpressions;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementRunExecutionEntry : ManagedTemplate
    {
        const string k_ActionCursorClassName = "mui-action-cursor";
        const string k_TextPillSpacingClass = "mui-text-object-pill-spacing";
        const string k_WarningClassName = "mui-object-pill-warning";

        ExecutionLog m_Content;
        AssistantImage m_ExecutionIcon;
        VisualElement m_ExecutionLogContainer;

        public ChatElementRunExecutionEntry()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public void SetLog(ExecutionLog content)
        {
            m_Content = content;

            if (m_Content.LogType != LogType.Log)
            {
                m_ExecutionIcon.SetIconClassName("warn");
            }

            if (content.LoggedObjects != null && content.LoggedObjects.Length > 0)
            {
                FormatLog(content);
            }
            else
            {
                AddTextToLog(content.Log, content.LogType);
            }
        }

        void AddTextToLog(string text, LogType logType = LogType.Log ,string className = null)
        {
            var textElementToAdd = new TextElement { enableRichText = true, text = $"{text} " };
            if (logType == LogType.Warning || logType == LogType.Error)
            {
                textElementToAdd.AddToClassList(k_WarningClassName);
            }
            textElementToAdd.AddToClassList(className);
            m_ExecutionLogContainer.Add(textElementToAdd);
        }

        void FormatLog(ExecutionLog content)
        {
            var log = content.Log;
            var references = content.LoggedObjects;
            var referenceNames = content.LoggedObjectNames;
            var matches = ExecutionResult.PlaceholderRegex.Matches(log);
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                int currentMatchIndex = match.Groups[1].Captures[0].Index;

                int index = int.Parse(match.Groups[1].Value);
                if (index >= 0 && index < references.Length)
                {
                    var reference = references[index];
                    switch (reference)
                    {
                        case Object objectInstance:
                        {
                            if (objectInstance != null)
                            {
                                var objectPill = new ObjectPill();
                                objectPill.Initialize(Context);
                                objectPill.SetData(objectInstance);
                                m_ExecutionLogContainer.Add(objectPill);
                                break;
                            }

                            var referenceName = referenceNames[index];
                            AddTextToLog(string.IsNullOrEmpty(referenceName) ? "<i>Destroyed Object</i>" : $"<i>{referenceName}</i>");
                            break;
                        }
                        default:
                            AddTextToLog(references[index]?.ToString());
                            break;
                    }
                }

                if (i < matches.Count - 1)
                {
                    var nextMatch = matches[i + 1];
                    int nextMatchIndex = nextMatch.Groups[1].Captures[0].Index;
                    AddTextToLog(log.Substring(currentMatchIndex + 1, nextMatchIndex - currentMatchIndex - 2), LogType.Log, k_TextPillSpacingClass);
                }
                else
                {
                    AddTextToLog(log.Substring(currentMatchIndex + 1, log.Length - currentMatchIndex - 1));
                }
            }
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ExecutionIcon = view.SetupImage("executionIcon", "checkmark");
            m_ExecutionLogContainer = view.Q<VisualElement>("executionLogContainer");
            m_ExecutionLogContainer.RegisterCallback<PointerOverLinkTagEvent>(OnLinkOver);
            m_ExecutionLogContainer.RegisterCallback<PointerOutLinkTagEvent>(OnLinkOut);
        }

        void OnLinkOut(PointerOutLinkTagEvent evt)
        {
            if (evt.target is Label text)
                text.RemoveFromClassList(k_ActionCursorClassName);
        }

        void OnLinkOver(PointerOverLinkTagEvent evt)
        {
            if (evt.target is Label text)
                text.AddToClassList(k_ActionCursorClassName);
        }
    }
}
