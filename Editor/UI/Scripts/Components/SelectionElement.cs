using System;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using TextOverflow = UnityEngine.UIElements.TextOverflow;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class SelectionElement : AdaptiveListViewEntry
    {
        Label m_Text;
        Label m_Path;
        Button m_FindButton;
        VisualElement m_Checkmark;
        SelectionPopup m_Owner;
        LogData m_LogData;

        SelectionPopup.ListEntry m_Entry;

        public Action<SelectionElement> OnAddRemoveButtonClicked;
        bool m_IsSelected;
        bool m_IgnoreNextClick;

        readonly string k_PrefabInSceneStyleClass = "mui-chat-selection-prefab-text-color";
        readonly string k_EntrySelectedClass = "mui-selected-list-entry";
        readonly string k_LogPathString = "Selected Console Log";


        protected override void InitializeView(TemplateContainer view)
        {
            m_Text = view.Q<Label>("selectionElementText");
            m_Path = view.Q<Label>("selectionElementPath");
            m_Text.enableRichText = false;
            m_FindButton = view.SetupButton("selectionElementFindButton", OnFindClicked);
            m_Checkmark = view.Q<VisualElement>("mui-selection-element-checkmark");

            m_FindButton.visible = false;
            m_FindButton.focusable = false;

            m_Text.style.overflow = Overflow.Hidden;
            m_Text.style.whiteSpace = WhiteSpace.NoWrap;

            view.RegisterCallback<ClickEvent>(ToggleSelection);
            view.RegisterCallback<MouseEnterEvent>(MouseEntered);
            view.RegisterCallback<MouseLeaveEvent>(MouseLeft);
        }

        void MouseEntered(MouseEnterEvent evt)
        {
            if (!m_IsSelected)
                m_FindButton.visible = true;
        }

        void MouseLeft(MouseLeaveEvent evt)
        {
            if (!m_IsSelected)
                m_FindButton.visible = false;
        }

        void ToggleSelection(ClickEvent evt)
        {
            if (m_IgnoreNextClick)
            {
                m_IgnoreNextClick = false;
                return;
            }

            if (m_Entry.LogData.HasValue)
            {
                m_Owner.SelectedLogReference(m_Entry.LogData.Value, this);
                AIAssistantAnalytics.ReportContextEvent(ContextSubType.ChooseContextFromFlyout, d =>
                {
                    d.ContextContent = m_Entry.LogData.Value.Message;
                    d.ContextType = "LogData";
                });
            }
            else
            {
                m_Owner.SelectedObject(m_Entry.Object, this);
                AIAssistantAnalytics.ReportContextEvent(ContextSubType.ChooseContextFromFlyout, d =>
                {
                    d.ContextContent = m_Entry.Object.name;
                    d.ContextType = m_Entry.Object.GetType().ToString();
                });
            }
        }

        public override void SetData(int index, object data, bool isSelected = false)
        {
            base.SetData(index, data);

            m_Entry = data as SelectionPopup.ListEntry;
            SetOwner(m_Entry?.Owner);

            if (m_Entry == null)
            {
                return;
            }

            if (m_Entry.LogData != null)
            {
                SetText(m_Entry.LogData.Value.Message);
                SetPath(k_LogPathString);
                m_FindButton.SetDisplay(false);

                string[] lines = m_Entry.LogData.Value.Message.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                if (lines.Length > 0)
                    m_Text.tooltip = $"Console {m_Entry.LogData.Value.Type}:\n{lines[0]}";

                m_Text.style.textOverflow = TextOverflow.Ellipsis;
            }
            else
            {
                var unityObj = m_Entry.Object;

                if (unityObj != null)
                {
                    SetText(unityObj.name);
                    SetPath(AssetDatabase.GetAssetOrScenePath(unityObj));
                    ShowTextAsPrefabInScene(unityObj.IsPrefabInScene());

                    m_Text.tooltip = ContextViewUtils.GetObjectTooltip(unityObj);
                }

                m_Text.style.textOverflow = TextOverflow.Clip;
            }

            SetSelected(m_Entry.IsSelected);
        }

        void SetText(string text)
        {
            m_Text.text = text;
        }

        void SetPath(string path)
        {
            m_Path.text = path;
        }

        void SetOwner(SelectionPopup selectionPopup)
        {
            m_Owner = selectionPopup;
        }


        void ShowTextAsPrefabInScene(bool isPrefab)
        {
            if (isPrefab)
                m_Text.AddToClassList(k_PrefabInSceneStyleClass);
            else
                m_Text.RemoveFromClassList(k_PrefabInSceneStyleClass);
        }


        public void SetSelected(bool selected)
        {
            m_Checkmark.visible = selected;
            if (selected)
            {
                AddToClassList(k_EntrySelectedClass);
            }
            else
            {
                RemoveFromClassList(k_EntrySelectedClass);
            }
        }

        void OnFindClicked(PointerUpEvent evt)
        {
            m_IgnoreNextClick = true;
            m_Owner.PingObject(m_Entry.Object);

            AIAssistantAnalytics.ReportContextEvent(ContextSubType.PingAttachedContextObjectFromFlyout, d =>
            {
                d.ContextType = m_Entry.Object.GetType().ToString();
                d.ContextContent = m_Entry.Object.name;
            });
        }
    }
}
