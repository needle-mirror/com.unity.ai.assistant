using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.History
{
    class HistoryPanelEntry : AdaptiveListViewEntry
    {
        const string k_HeaderClass = "mui-history-panel-header-entry";
        const string k_SelectedClass = "mui-history-panel-entry-selected";

        const string k_Edit = "Edit";
        const string k_Delete = "Delete";
        ConversationModel m_Data;

        VisualElement m_HeaderRoot;
        Label m_HeaderText;

        VisualElement m_ConversationElementRoot;
        Button m_FavoriteToggle;
        AssistantImage m_FavoriteStateIcon;
        Label m_ConversationText;
        TextField m_ConversationEditText;

        bool m_EditModeActive;
        bool m_IsHeader;
        bool m_IsSelected;
        bool m_IsButtonClick;
        bool m_IsFavorited;

        public ConversationModel Data => m_Data;

        public void SetSelected(bool selected)
        {
            if (m_IsSelected == selected)
            {
                return;
            }

            m_IsSelected = selected;
            EnableInClassList(k_SelectedClass, selected);
            m_ConversationText.EnableInClassList(k_SelectedClass, selected);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_HeaderRoot = view.Q<VisualElement>("historyPanelHeaderRoot");
            m_HeaderText = view.Q<Label>("historyPanelHeaderText");

            m_ConversationElementRoot = view.Q<VisualElement>("historyPanelElementConversationRoot");
            m_ConversationElementRoot.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == (int)MouseButton.RightMouse)
                {
                    OnConversationClicked(evt);
                }
            });

            m_ConversationElementRoot.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == (int)MouseButton.RightMouse)
                {
                    OnConversationClicked(evt);
                }
            });

            m_FavoriteToggle = view.SetupButton("historyPanelFavoriteStateButton", OnToggleFavorite);
            m_FavoriteStateIcon = view.SetupImage("historyPanelFavoriteStateIcon");

            m_ConversationText = view.Q<Label>("historyPanelElementConversationText");
            m_ConversationText.enableRichText = false;

            m_ConversationEditText = view.Q<TextField>("historyPanelElementConversationEditText");
            m_ConversationEditText.isDelayed = true;
            m_ConversationEditText.RegisterCallback<FocusOutEvent>(OnEditFocusLost);
            m_ConversationEditText.RegisterValueChangedCallback(OnEditComplete);

            RegisterCallback<PointerUpEvent>(OnSelectEntry);

            RefreshUI();
        }

        void OnSelectEntry(PointerUpEvent evt)
        {
            if (m_IsSelected || m_IsHeader || m_IsButtonClick)
            {
                m_IsButtonClick = false;
                return;
            }

            NotifySelectionChanged();
        }

        void OnToggleFavorite(PointerUpEvent evt)
        {
            m_IsButtonClick = true;

            m_IsFavorited = !m_IsFavorited;

            Context.API.SetFavorite(m_Data.Id, m_IsFavorited);

            RefreshFavoriteDisplay();

            AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.FavoriteConversation, d =>
            {
                d.ConversationId = m_Data.Id.Value;
                d.IsFavorite = m_IsFavorited.ToString();
                d.ConversationTitle = m_Data.Title;
            });
        }

        void OnConversationClicked(PointerUpEvent evt)
        {
            m_IsButtonClick = true;

            // Create the menu and add items to it
            var menu = new GenericMenu();

            // Add menu items
            menu.AddItem(new GUIContent(k_Edit), false, OnEditClicked);
            menu.AddItem(new GUIContent(k_Delete), false, OnDeleteClicked);
            // Add more items here

            // Show the menu at the current mouse position
            menu.ShowAsContext();

            // Use the event
            evt.StopPropagation();
            evt.StopImmediatePropagation();
            Event.current.Use();
        }

        void OnEditClicked()
        {
            BeginEdit();
            m_ConversationEditText.SetValueWithoutNotify(m_Data.Title);

            RefreshUI();
        }

        void OnDeleteClicked()
        {
            Context.API.ConversationDelete(m_Data.Id);

            AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.DeleteConversation, d =>
            {
                d.ConversationId = m_Data.Id.Value;
                d.ConversationTitle = m_Data.Title;
            });
        }

        public override void SetData(int index, object newData, bool isSelected = false)
        {
            base.SetData(index, newData);

            if (newData is string headerText)
            {
                SetAsHeader(headerText);
                SetSelected(false);
            }
            else
            {
                var data = (ConversationModel)newData;
                SetAsData(data);
                SetSelected(isSelected);
            }
        }

        void SetAsHeader(string text)
        {
            m_IsHeader = true;

            ToggleConversationDisplay(false);
            m_HeaderText.text = text;

            RefreshUI();
        }

        void ToggleConversationDisplay(bool isVisible)
        {
            m_HeaderRoot.SetDisplay(!isVisible);
            m_ConversationElementRoot.SetDisplay(isVisible);

            if (isVisible)
            {
                RemoveFromClassList(k_HeaderClass);
            }
            else
            {
                AddToClassList(k_HeaderClass);
            }
        }

        void SetAsData(ConversationModel data)
        {
            m_Data = data;
            EndEdit(false);
            m_IsHeader = false;

            ToggleConversationDisplay(true);

            // TODO: Enable if design wants a log to indicate contextual conversations
            // m_ConversationIcon.style.display = data.IsContextAware ? DisplayStyle.Flex : DisplayStyle.None;
            m_ConversationText.text = data.Title == null ? "New conversation" : data.Title.Replace("\n", " ");
            m_ConversationText.tooltip = data.Title;

            // This field is fetched via cache, which gets invalidated on a full reload
            // Until then we persist our local state since changing this value can take longer than a conversation refresh
            m_IsFavorited = Context.Blackboard.GetFavorite(data.Id);

            RefreshUI();
        }

        void RefreshFavoriteDisplay()
        {
            string newClassName = m_IsFavorited ? "star-filled-white" : "star-filled-grey";
            m_FavoriteStateIcon.SetIconClassName(newClassName);
        }

        void RefreshUI()
        {
            if (m_EditModeActive)
            {
                m_ConversationEditText.style.display = DisplayStyle.Flex;
                m_ConversationText.style.display = DisplayStyle.None;
                m_ConversationEditText.Focus();
            }
            else
            {
                m_ConversationEditText.style.display = DisplayStyle.None;
                m_ConversationText.style.display = DisplayStyle.Flex;
            }

            RefreshFavoriteDisplay();
        }

        void OnEditFocusLost(FocusOutEvent evt)
        {
            EndEdit();
        }

        void OnEditComplete(ChangeEvent<string> evt)
        {
            EndEdit();

            if (string.IsNullOrWhiteSpace(evt.newValue))
                return;

            if (evt.newValue == m_Data.Title)
            {
                return;
            }

            // Set the conversation title directly, we don't wait for the server to respond
            m_Data.Title = evt.newValue;
            m_ConversationText.text = m_Data.Title;

            Context.API.ConversationRename(m_Data.Id, evt.newValue);
            AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.RenameConversation, d =>
            {
                d.ConversationId = m_Data.Id.Value;
                d.ConversationTitle = evt.newValue;
            });
        }

        void BeginEdit()
        {
            m_EditModeActive = true;
            Context.API.SuspendConversationRefresh();
        }

        void EndEdit(bool refresh = true)
        {
            Context.API.ResumeConversationRefresh();
            m_EditModeActive = false;

            if (refresh)
            {
                RefreshUI();
            }
        }
    }
}
