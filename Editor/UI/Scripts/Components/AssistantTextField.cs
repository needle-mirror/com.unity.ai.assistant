using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.Data;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using TextField = UnityEngine.UIElements.TextField;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantTextField : ManagedTemplate
    {
        const string k_ChatFocusClass = "mui-mft-input-focused";
        const string k_ChatHoverClass = "mui-mft-input-hovered";
        const string k_ScrollVisibleClass = "mui-mft-scroll-active";
        const string k_ChatActionEnabledClass = "mui-submit-enabled";
        const string k_CommandInputStylingOpeningTags = "<b><color=#3A79BB>";
        const string k_CommandInputStylingClosingTags = "</color></b>";

        const string k_SubmitImage = "arrow-up";
        const string k_StopImage = "stop-square";

        const string k_ActionButtonToolTipSend = "Send prompt";
        const string k_ActionButtonToolTipStop = "Stop response";
        const string k_ActionButtonToolTipNoPrompt = "No prompt entered";

        VisualElement m_Root;

        Button m_ActionButton;
        AssistantImage m_SubmitButtonImage;

        VisualElement m_PopupAnchor;

        ScrollView m_InputScrollView;
        TextField m_ChatInput;
        string m_FirstWord;
        TextElement m_ChatInputTextElement;
        Label m_ChatCharCount;
        Label m_Placeholder;
        VisualElement m_PlaceholderContent;
        VisualElement m_ActionRow;

        Label m_PointCostLabel;
        VisualElement m_PointCostDisplay;
        PointCostRequestId m_ActivePointCostRequestId = PointCostRequestId.Invalid;

        VisualElement m_ContextLimitWarning;

        readonly List<string> k_RouteLabels = new();
        bool m_RouteActive;

        Button m_AddContextButton;

        VisualElement m_PopupRoot;
        RoutesPopup m_RoutesPopup;
        PopupTracker m_RoutesPopupTracker;
        Button m_AddRouteButton;
        int m_SelectedRouteItemIndex;
        string m_InputAfterSlash;

        bool m_TextHasFocus;
        bool m_ShowPlaceholder;
        bool m_HighlightFocus;
        bool m_RoutesEnabled;
        bool m_EditContextEnabled;
        bool m_RouteSelection;

        IAssistantHostWindow m_HostWindow;

        public AssistantTextField()
            : base(AssistantUIConstants.UIModulePath)
        {
            m_RoutesEnabled = false;
            m_EditContextEnabled = false;
        }

        public bool ShowPlaceholder
        {
            get => m_ShowPlaceholder;
            set
            {
                if (m_ShowPlaceholder == value)
                {
                    return;
                }

                m_ShowPlaceholder = value;
                RefreshUI();
            }
        }

        public bool HighlightFocus
        {
            get => m_HighlightFocus;
            set
            {
                if (m_HighlightFocus == value)
                {
                    return;
                }

                m_HighlightFocus = value;
                RefreshUI();
            }
        }

        internal string Text => m_ChatInput.value;

        public event Action<string, bool> OnCommand;
        public event Action<string> SubmitRequest;
        public event Action CancelRequest;

        public Button ContextButton => m_AddContextButton;

        public void SetHost(IAssistantHostWindow hostWindow, VisualElement popupRoot, VisualElement popupAnchor)
        {
            m_HostWindow = hostWindow;
            m_PopupRoot = popupRoot;
            m_PopupAnchor = popupAnchor;
            m_RoutesEnabled = m_PopupRoot != null;
            m_EditContextEnabled = true;

            if (m_RoutesEnabled)
            {
                InitializeRoutesPopup();
            }

            m_AddContextButton.SetDisplay(m_EditContextEnabled);
            m_AddRouteButton.SetDisplay(m_RoutesEnabled);
        }

        public void ClearText()
        {
            m_ChatInput.value = AssistantUISessionState.instance.Command;
            if (AssistantUISessionState.instance.Command != "")
            {
                m_ChatInput.value += " ";
            }
        }

        public void SetText(string text)
        {
            m_ChatInput.SetValueWithoutNotify(text);
            m_ChatInput.Focus();
            OnChatValueChanged();
        }

        public void Enable()
        {
            m_ChatInput.SetEnabled(true);
        }

        public void Disable(string reason)
        {
            m_Placeholder.text = reason;
            m_ChatInput.SetEnabled(false);
        }

        public void ToggleContextLimitWarning(bool enabled)
        {
            m_ContextLimitWarning.SetDisplay(enabled);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("museTextFieldRoot");

            m_AddRouteButton = view.SetupButton("addRouteButton", ToggleRoutesPopupShown);
            m_AddContextButton = view.Q<Button>("addContextButton");
            m_AddContextButton.SetDisplay(m_EditContextEnabled);
            m_AddRouteButton.SetDisplay(m_RoutesEnabled);

            k_RouteLabels.Clear();
            foreach (var command in ChatCommands.GetCommands())
            {
                ChatCommands.TryGetCommandHandler(command, out var commandHandler);

                if (!commandHandler.ShowInList)
                    continue;

                k_RouteLabels.Add(commandHandler.Label);
            }

            m_ActionButton = view.Q<Button>("actionButton");
            m_ActionButton.RegisterCallback<PointerUpEvent>(_ => OnSubmit());

            m_SubmitButtonImage = view.SetupImage("actionButtonImage", k_SubmitImage);

            m_InputScrollView = view.Q<ScrollView>("inputScrollView");

            m_PlaceholderContent = view.Q<VisualElement>("placeholderContent");
            m_Placeholder = view.Q<Label>("placeholderText");

            m_ChatInput = view.Q<TextField>("input");
            m_ChatInput.maxLength = AssistantMessageSizeConstraints.PromptLimit;
            m_ChatInput.multiline = true;
            m_ChatInput.selectAllOnFocus = false;
            m_ChatInput.selectAllOnMouseUp = false;
            m_ChatInput.RegisterCallback<ClickEvent>(_ => SetPopupVisibility());
            m_ChatInput.RegisterCallback<KeyUpEvent>(OnChatKeyUpEvent);
            // TrickleDown.TrickleDown is a workaround for registering KeyDownEvent type with Unity 6
            m_ChatInput.RegisterCallback<KeyDownEvent>(OnChatKeyDownEvent, TrickleDown.TrickleDown);
            m_ChatInput.RegisterValueChangedCallback(_ => OnChatValueChanged());
            m_PlaceholderContent.RegisterCallback<ClickEvent>(_ => m_ChatInput.Focus());
            m_ChatInput.RegisterCallback<FocusInEvent>(_ => SetTextFocused(true));
            m_ChatInput.RegisterCallback<FocusOutEvent>(_ => SetTextFocused(false));
            m_ChatInput.RegisterCallback<PointerLeaveEvent>(_ => m_ActionButton.RemoveFromClassList(k_ChatHoverClass));
            m_ChatInput.RegisterCallback<GeometryChangedEvent>(OnInputGeometryChanged);

            m_ActionRow = view.Q<VisualElement>("museTextFieldActionRow");

            m_Root.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target != m_ActionRow)
                {
                    return;
                }

                m_ChatInput.Focus();
            });

            m_ChatInputTextElement = m_ChatInput.Q<TextElement>();
            m_ChatInputTextElement.enableRichText = true;

            m_PlaceholderContent = view.Q<VisualElement>("placeholderContent");
            m_Placeholder = view.Q<Label>("placeholderText");

            m_ChatCharCount = view.Q<Label>("characterCount");

            m_ContextLimitWarning = view.Q<VisualElement>("contextLimitWarning");

            m_PointCostDisplay = view.Q<VisualElement>("textFieldPointCostDisplayElement");
            m_PointCostLabel = view.Q<Label>("textFieldPointCostDisplayLabel");
            m_PointCostDisplay.SetDisplay(false);

            Context.API.APIStateChanged += OnAPIStateChanged;
            Context.API.PointCostReceived += OnPointCostReceived;

            ShowPlaceholder = true;
            HighlightFocus = true;

            m_ChatInput.value = AssistantUISessionState.instance.Prompt;
            RefreshUI();
        }

        void OnAPIStateChanged()
        {
            RefreshUI();
        }

        void OnSubmit()
        {
            if (Context.Blackboard.IsAPIWorking)
            {
                CancelRequest?.Invoke();
                return;
            }

            if (!Context.Blackboard.IsAPIReadyForPrompt)
            {
                return;
            }

            SubmitRequest?.Invoke(AssistantUISessionState.instance.Prompt);
        }

        void OnInputGeometryChanged(GeometryChangedEvent evt)
        {
            m_ActionButton.EnableInClassList(k_ScrollVisibleClass, m_InputScrollView.verticalScroller.style.display != DisplayStyle.None);
        }

        void SetTextFocused(bool state)
        {
            m_TextHasFocus = state;
            RefreshUI();
        }

        void RefreshUI()
        {
            RefreshChatCharCount();
            var actionButtonEnabled = Context.Blackboard.IsAPIWorking ||
                                !string.IsNullOrEmpty(m_ChatInput.value) && Context.Blackboard.IsAPIReadyForPrompt;
            m_ActionButton.EnableInClassList(k_ChatActionEnabledClass, actionButtonEnabled);

            if (!ShowPlaceholder || m_TextHasFocus || !string.IsNullOrEmpty(m_ChatInput.value) || m_RouteActive)
            {
                m_PlaceholderContent.style.display = DisplayStyle.None;
            }
            else
            {
                m_PlaceholderContent.style.display = DisplayStyle.Flex;
            }

            m_Root.EnableInClassList(k_ChatFocusClass, m_TextHasFocus && m_HighlightFocus);

            m_SubmitButtonImage.SetIconClassName(Context.Blackboard.IsAPIWorking ? k_StopImage : k_SubmitImage);

            if (actionButtonEnabled)
            {
                m_ActionButton.tooltip =
                    Context.Blackboard.IsAPIWorking ? k_ActionButtonToolTipStop : k_ActionButtonToolTipSend;
            }
            else
            {
                m_ActionButton.tooltip = k_ActionButtonToolTipNoPrompt;
            }
        }

        void OnChatValueChanged()
        {
            StyleRouteInput();
            RefreshUI();
            RefreshPointCost();
        }

        void RefreshChatCharCount()
        {
            m_ChatCharCount.text = $"{m_ChatInput.value.Length.ToString()}/{AssistantMessageSizeConstraints.PromptLimit}";
        }

        private bool CheckPopupNavigationInput(KeyCode keycode)
        {
            var popupNavigationKeyPress =
                keycode != KeyCode.UpArrow
                && keycode != KeyCode.DownArrow
                && keycode != KeyCode.KeypadEnter
                && keycode != KeyCode.Return;
            return popupNavigationKeyPress;

        }

        private void OnChatKeyUpEvent(KeyUpEvent evt)
        {
            if (CheckPopupNavigationInput(evt.keyCode))
            {
                SetPopupVisibility();
            }

            RefreshChatCharCount();
        }

        internal void NewLineInput(KeyDownEvent evt)
        {
            var insertPosition = m_ChatInput.cursorIndex;

            bool pastFirstWord = insertPosition >= m_FirstWord.Length + 1;

            int tagLength = k_CommandInputStylingOpeningTags.Length + k_CommandInputStylingClosingTags.Length;

            // delete first character and tag when delete is pressed at start of route tag
            if (m_RouteActive && insertPosition + tagLength == tagLength &&
                evt.keyCode == KeyCode.Delete)
            {
                var splitChatInputOnSpace = m_ChatInput.value.Split(" ", 2);
                if (splitChatInputOnSpace.Length > 1)
                {
                    m_ChatInput.value =
                        $"{splitChatInputOnSpace[0].Remove(0, k_CommandInputStylingOpeningTags.Length + 1)} {splitChatInputOnSpace[1]}";
                }
                else
                {
                    m_ChatInput.value = string.Empty;
                }

                evt.StopImmediatePropagation();
                return;
            }

            // prevent delete key default behavior if at end of route tag
            bool deleteKeyAtEndOfRouteTag = m_RouteActive &&
                                         insertPosition + tagLength == tagLength + m_FirstWord.Length &&
                                         evt.keyCode == KeyCode.Delete;

            bool enterKey = evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Return;

            // do not allow newline if cursor is within first word
            bool newlineWithinRouteTag = m_RouteActive && !pastFirstWord &&
                                         (enterKey ||
                                          evt.character == '\n');

           if (deleteKeyAtEndOfRouteTag || newlineWithinRouteTag) {
                evt.StopImmediatePropagation();
                return;
            }

            // do not allow newline if cursor is within first word
            if (m_RouteActive && !pastFirstWord)
            {
                return;
            }

            var isAtEnd = m_ChatInput.cursorIndex == m_ChatInput.value.Length;

            if (m_RouteActive && pastFirstWord)
            {
                insertPosition += tagLength;
                isAtEnd = insertPosition == m_ChatInput.value.Length;
            }

            if (string.IsNullOrEmpty(m_ChatInput.value) &&
                (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter ||
                 evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow))
            {
                evt.StopImmediatePropagation();
            }

            // Shift + enter adds a line break.
            // We get 2 similar events when the user presses shift+enter and want to stop both from propagating but only add one line break!
            if (evt.shiftKey &&
                (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.character == '\n'))
            {
                if (evt.character == '\n')
                {
                    m_ChatInput.SetValueWithoutNotify(m_ChatInput.value.Insert(insertPosition, "\n"));
                    m_ChatInput.cursorIndex += 1;

                    if (isAtEnd)
                    {
                        m_ChatInput.selectIndex = insertPosition + 1;
                    }
                    else
                    {
                        m_ChatInput.selectIndex = m_ChatInput.cursorIndex;
                    }
                }

                evt.StopPropagation();

#if !UNITY_2023_1_OR_NEWER
                evt.PreventDefault();
#endif
            }
            else if (evt.character == '\n')
            {
                // Don't do default behaviour of adding a new line with return:
                evt.StopPropagation();

#if !UNITY_2023_1_OR_NEWER
                evt.PreventDefault();
#endif
            }

        }

        internal void OnChatKeyDownEvent(KeyDownEvent evt)
        {
            if (CheckPopupNavigationInput(evt.keyCode))
            {
                SetPopupVisibility();
            }

            NewLineInput(evt);

            if (m_RoutesEnabled)
            {
                bool cursorAtOrBeforeFirstWord = m_ChatInput.value.Length == 0 || m_ChatInput.cursorIndex <= m_ChatInput.value.Split(" ")[0].Length;

                if (!cursorAtOrBeforeFirstWord && m_RoutesPopup.IsShown)
                {
                    HideRoutesPopup();
                }

                if (cursorAtOrBeforeFirstWord)
                {
                    DetectRouteInputOnKeyDown(evt);
                }
            }

            if (m_RouteSelection)
            {
                m_RouteSelection = false;
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                {
                    var trimmedPrompt = m_ChatInput.value.Trim();

                    // check whether content of chat is only route/command tag
                    if (m_RouteActive && m_FirstWord.Length == RemoveCommandStyling(trimmedPrompt).Length)
                    {
                        return;
                    }
                    // check for empty prompt, only whitespaces
                    if (trimmedPrompt.Length == 0)
                    {
                        return;
                    }

                    break;
                }

                default:
                {
                    return;
                }
            }

            bool useModifierToSend = EditorPrefs.GetBool(AssistantSettingsProvider.k_SendPromptModifierKey, false);

            if (evt.altKey || evt.shiftKey)
                return;

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                if (evt.commandKey != useModifierToSend)
                {
                    return;
                }
            }
            else
            {
                if (evt.ctrlKey != useModifierToSend)
                {
                    return;
                }
            }

            evt.StopPropagation();

            // Ignore enter if we have an active API/prompt
            if (Context.Blackboard.IsAPIWorking || !Context.Blackboard.IsAPIReadyForPrompt)
            {
                return;
            }

            OnSubmit();
            m_AddRouteButton.SetEnabled(true);
        }

        void ToggleRoutesPopupShown(PointerUpEvent evt)
        {
            if (m_RoutesPopup.IsShown)
            {
                HideRoutesPopup();
            }
            else
            {
                // Popup only available when no input in chat window
                m_RoutesPopup.UpdateFilteredRoutes("", true);
                ShowRoutesPopup();
                AdjustPopupHighlight();

                AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.OpenShortcuts);
            }
        }

        void ShowRoutesPopup()
        {
            m_RoutesPopup.Show();
            m_ChatInput.Focus();
            m_RoutesPopupTracker = new PopupTracker(m_RoutesPopup, m_AddRouteButton, m_PopupAnchor);
            m_RoutesPopupTracker.Dismiss += HideRoutesPopup;
        }

        void HideRoutesPopup()
        {
            if (m_RoutesPopupTracker == null)
            {
                // Popup is not active
                return;
            }

            m_RoutesPopupTracker.Dismiss -= HideRoutesPopup;
            m_RoutesPopupTracker.Dispose();
            m_RoutesPopupTracker = null;

            m_RoutesPopup.Hide();
        }

        void RouteSelectionChanged(string command)
        {
            var splitChatInput = m_ChatInput.value.Split(' ', 2);

            if (splitChatInput.Length > 1) {
                m_ChatInput.value = $"/{command} {splitChatInput[1]}";
            }
            else
            {
                m_ChatInput.value = $"/{command} ";
            }

            m_ChatInput.SelectRange(m_ChatInput.value.Length, m_ChatInput.value.Length);
            HideRoutesPopup();
            RefreshUI();
            m_ChatInput.Focus();
            m_RouteSelection = true;
        }

        void InitializeRoutesPopup()
        {
            if (m_RoutesPopup != null)
            {
                // Already done
                return;
            }

            m_RoutesPopup = new RoutesPopup();
            m_RoutesPopup.Initialize(Context);
            m_RoutesPopup.Hide();


            m_RoutesPopup.VisibleEntries.First().SetHovered(true);
            m_RoutesPopup.m_OnSelectionChanged += RouteSelectionChanged;

            m_PopupRoot.Add(m_RoutesPopup);

            if (m_HostWindow != null)
            {
                m_HostWindow.FocusLost += HideRoutesPopup;
            }
        }

        string AddCommandStyling(string input)
        {
            if (m_ChatInput.value.Contains(k_CommandInputStylingOpeningTags)
                && m_ChatInput.value.Contains(k_CommandInputStylingClosingTags))
            {
                return m_ChatInput.value;
            }
            return $"{k_CommandInputStylingOpeningTags}{input}{k_CommandInputStylingClosingTags}";
        }

        string RemoveCommandStyling(string input)
        {
            return input.Replace(k_CommandInputStylingOpeningTags, "").Replace(k_CommandInputStylingClosingTags, "");
        }

        void StyleRouteInput()
        {
            var splitChatInputOnSpace = m_ChatInput.value.Split(' ', 2);
            var splitChatInputOnClosingTags = m_ChatInput.value.Split(k_CommandInputStylingClosingTags, 2);

            // Moves space added directly after command behind closing rich text tags
            if (splitChatInputOnClosingTags.Length == 2 && splitChatInputOnClosingTags[0].Contains(" "))
            {
                m_ChatInput.value =
                    $"{splitChatInputOnClosingTags[0].Replace(" ", "")}{k_CommandInputStylingClosingTags} {splitChatInputOnClosingTags[1]}";
            }

            m_FirstWord = RemoveCommandStyling(splitChatInputOnSpace[0]).ToLower();

            m_RouteActive = ChatCommandParser.IsValidCommand(m_FirstWord);

            if (ChatCommandParser.IsValidCommand(m_FirstWord) && splitChatInputOnSpace.Length > 1)
            {
                if (!m_ChatInput.value.Contains(k_CommandInputStylingOpeningTags))
                {
                    m_ChatInput.value = $"{AddCommandStyling(m_FirstWord)} {splitChatInputOnSpace[1]}";
                }
                else if (!m_ChatInput.value.Contains(k_CommandInputStylingClosingTags))
                {
                    m_ChatInput.SetValueWithoutNotify($"{AddCommandStyling(m_FirstWord)} {splitChatInputOnSpace[1]}");
                }
            }
            else
            {
                m_ChatInput.value = RemoveCommandStyling(m_ChatInput.value);
            }


            bool validCommand = ChatCommandParser.IsValidCommand(m_FirstWord);

            SetPopupVisibility();

            SetShortcutButtonEnabledState();

            // Hide invalid command warning if first word input deleted
            if (m_FirstWord == string.Empty)
            {
                OnCommand?.Invoke(m_FirstWord, false);
            }

            if (validCommand)
            {
                AssistantUISessionState.instance.Command = m_FirstWord;
            }
            else
            {
                AssistantUISessionState.instance.Command = "";
            }

            AssistantUISessionState.instance.Prompt = RemoveCommandStyling(m_ChatInput.value);
        }

        void SetPopupVisibility()
        {
            m_FirstWord ??= string.Empty;

            var routesFilter = m_FirstWord;

            // if cursor within first word change first word to be before cursor
            if (m_ChatInput.cursorIndex < m_FirstWord.Length)
            {
                routesFilter = m_ChatInput.value.Substring(0, m_ChatInput.cursorIndex);
            }

            bool attemptedCommand = ChatCommandParser.IsCommand(routesFilter);
            bool validCommand = ChatCommandParser.IsValidCommand(routesFilter);
            bool caretBeyondFirstWord = m_ChatInput.cursorIndex > m_FirstWord.Length;
            bool caretBeyondFirstWordAndSpace = m_ChatInput.cursorIndex > m_FirstWord.Length + 1;

            if (m_RoutesPopup != null)
            {
                m_RoutesPopup.UpdateFilteredRoutes(routesFilter);

                if (attemptedCommand && !validCommand && !caretBeyondFirstWord)
                {
                    ShowRoutesPopup();
                    m_RoutesPopupTracker?.RealignPopup();
                }

                if (attemptedCommand && !caretBeyondFirstWordAndSpace)
                {
                    AdjustPopupHighlight();
                }

                if (caretBeyondFirstWord)
                {
                    m_RoutesPopup.HideNoResultsEntry();
                    HideRoutesPopup();
                }
            }

            if (m_FirstWord != string.Empty)
            {
                OnCommand?.Invoke(m_FirstWord, !validCommand && caretBeyondFirstWord && attemptedCommand);
            }
        }

        private void SetShortcutButtonEnabledState()
        {
            // Shortcuts button should only be enabled if no content in chat & if no chip
            m_AddRouteButton.SetEnabled(m_ChatInput.value.Length == 0 && !m_RouteActive);
        }

        void AdjustPopupHighlight()
        {
            foreach (var entry in m_RoutesPopup.VisibleEntries)
            {
                entry.SetHovered(false);
            }

            m_SelectedRouteItemIndex = 0;

            if (m_RoutesPopup.VisibleEntries.Count > 0)
            {
                m_RoutesPopup.VisibleEntries.First().SetHovered(true);
            }

            m_RoutesPopup.DisplayRoutes();
        }

        void RefreshPointCost()
        {
            string promptForCost = AssistantUISessionState.instance.Prompt;

            m_ActivePointCostRequestId = PointCostRequestId.GetNext(Context.Blackboard.ActiveConversationId);
            Context.API.GetPointCost(m_ActivePointCostRequestId, new PointCostRequestData(promptForCost));
        }

        void OnPointCostReceived(PointCostRequestId id, int cost)
        {
            if (id != m_ActivePointCostRequestId)
            {
                // Not for us
                return;
            }

            m_PointCostDisplay.SetDisplay(cost != 0);

            m_ActivePointCostRequestId = PointCostRequestId.Invalid;
            m_PointCostLabel.text = cost.ToString();
        }
    }
}
