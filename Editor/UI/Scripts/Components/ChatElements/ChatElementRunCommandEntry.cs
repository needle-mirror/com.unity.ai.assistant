using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.Editor.Agent;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Toggle = UnityEngine.UIElements.Toggle;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementRunCommandEntry : ManagedTemplate
    {
        const string k_ParamTagStart = "<param>";
        const string k_ParamTagEnd = "</param>";

        VisualElement m_Description;
        Button m_PreviewAction;

        string m_PreviewText;
        AgentRunCommand m_AgentRunCommand;
        bool m_ItemizeAction;

        Action m_Action;

        public ChatElementRunCommandEntry()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Description = view.Q<VisualElement>("description");

            m_PreviewAction = view.SetupButton("action", OnActionClicked);
            m_PreviewAction.SetDisplay(false);
        }

        public void SetCommand(string previewText, AgentRunCommand agentRunCommand)
        {
            m_PreviewText = previewText;
            m_AgentRunCommand = agentRunCommand;

            ItemizeItems();
        }

        void ItemizeItems()
        {
            m_Description.Clear();

            var segments = Regex.Split(m_PreviewText, $"({k_ParamTagStart}.*?{k_ParamTagEnd})");
            foreach (var segment in segments)
            {
                if (segment.StartsWith(k_ParamTagStart) && segment.EndsWith(k_ParamTagEnd))
                {
                    string fieldName = segment.Substring(k_ParamTagStart.Length, segment.Length - k_ParamTagStart.Length - k_ParamTagEnd.Length);
                    var instanceType = m_AgentRunCommand.Instance.GetType();
                    var fieldInfo = instanceType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        var field = CreateFieldForType(fieldInfo, m_AgentRunCommand.Instance);
                        if (field != null)
                        {
                            field.AddToClassList("entry-label-item");
                            m_Description.Add(field);
                        }
                        else
                        {
                            // Go the label route
                            object value = fieldInfo.GetValue(m_AgentRunCommand.Instance);
                            var labelText = $"{value?.ToString() ?? "None"} ";

                            // Call a split function
                            SplitAndAddToDescription(labelText);
                        }
                    }
                }
                else
                {
                    SplitAndAddToDescription(segment);
                }
            }
        }

        void SplitAndAddToDescription(string segment)
        {
            // Split the segment by spaces or dots, and keep them
            string[] parts = Regex.Split(segment, @"(?<=\s+|\.)");
            foreach (string part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    var label = new Label(part.Replace(" ", "\u00A0\u200B"));
                    label.AddToClassList("entry-label-item");
                    m_Description.Add(label);
                }
            }
        }

        VisualElement CreateFieldForType(FieldInfo fieldInfo, object instance)
        {
            var fieldType = fieldInfo.FieldType;
            object value = fieldInfo.GetValue(instance);

            if (fieldType == typeof(float))
            {
                var floatField = new FloatField { value = (float)value };
                floatField.RegisterValueChangedCallback(evt =>
                {
                    fieldInfo.SetValue(instance, evt.newValue);
                    ReportValueChange(evt);
                });
                return floatField;
            }

            if (fieldType == typeof(int))
            {
                var intField = new IntegerField { value = (int)value };
                intField.RegisterValueChangedCallback(evt =>
                {
                    fieldInfo.SetValue(instance, evt.newValue);
                    ReportValueChange(evt);
                });
                return intField;
            }

            if (fieldType == typeof(string))
            {
                var textField = new TextField { value = (string)value };
                textField.RegisterValueChangedCallback(evt =>
                {
                    fieldInfo.SetValue(instance, evt.newValue);
                    ReportValueChange(evt);
                });
                return textField;
            }

            if (fieldType == typeof(bool))
            {
                var toggle = new Toggle { value = (bool)value };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    fieldInfo.SetValue(instance, evt.newValue);
                    ReportValueChange(evt);
                });
                return toggle;
            }

            if (fieldType == typeof(Vector3))
            {
                var vector3Field = new Vector3Field { value = (Vector3)value };
                vector3Field.RegisterValueChangedCallback(evt =>
                {
                    fieldInfo.SetValue(instance, evt.newValue);
                    ReportValueChange(evt);
                });
                return vector3Field;
            }

            if (fieldType == typeof(Vector2))
            {
                var vector3Field = new Vector2Field { value = (Vector2)value };
                vector3Field.RegisterValueChangedCallback(evt =>
                {
                    fieldInfo.SetValue(instance, evt.newValue);
                    ReportValueChange(evt);
                });
                return vector3Field;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                var objectField = new ObjectField { objectType = fieldType, value = (UnityEngine.Object)value };
                objectField.RegisterValueChangedCallback(evt =>
                {
                    fieldInfo.SetValue(instance, evt.newValue);
                    AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.ModifyRunCommandPreviewWithObjectPicker,
                        d =>
                        {
                            if (evt.newValue != null)
                                d.PreviewParameter = evt.newValue.ToString();
                        });
                });
                return objectField;
            }

            return null;
        }

        public void RegisterAction(Action entryAction)
        {
            m_PreviewAction.SetDisplay(true);
            m_Action = entryAction;
        }

        void OnActionClicked(PointerUpEvent evt)
        {
            m_Action?.Invoke();
        }

        void ReportValueChange<T>(ChangeEvent<T> evt)
        {
            AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.ModifyRunCommandPreviewValue,
                d =>
                {
                    d.PreviewParameter = evt.newValue.ToString();
                });
        }
    }
}
