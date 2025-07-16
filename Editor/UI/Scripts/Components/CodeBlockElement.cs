using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.CodeAnalyze;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// A generic re-usable code block element, keep it as such, this is not to be made into a specific implementation
    /// Any additions here should be able to be used in any scenario that we want to use a code block
    /// </summary>
    internal class CodeBlockElement : ManagedTemplate
    {
        const string k_DefaultCodePreviewTitle = "C#";
        const string k_CollapsedControlsStyle = "mui-code-block-buttons-collapsed";
        const string k_MarkingStylePrefix = "mui-code-block-marking-";

        static CancellationTokenSource s_CodeCopyButtonActiveTokenSource;
        static CancellationTokenSource s_SaveButtonActiveTokenSource;

        readonly IDictionary<int, LineMarkingInfo> m_LineMarkings = new Dictionary<int, LineMarkingInfo>();

        LineNumberController m_LineNumberController;

        VisualElement m_Controls;
        VisualElement m_Content;
        VisualElement m_ContentBackground;
        VisualElement m_ContentForeground;
        Label m_CodeBlockTitle;
        AssistantImage m_CodeBlockTitleIcon;
        Label m_LineNumberText;
        ScrollView m_CodeScrollView;
        Label m_CodeText;
        Foldout m_Toggle;
        Button m_SaveButton;
        AssistantImage m_SaveButtonImage;
        Button m_CopyButton;
        AssistantImage m_CopyButtonImage;
        Button m_EditButton;
        AssistantImage m_EditButtonImage;

        FileSystemWatcher m_FileWatcher;

        bool m_AnalyticsEnabled;

        string m_Code;
        string m_CodeType;
        bool m_ReformatCode;

        string m_TempEditedFilePath;

        public Action<string> OnCodeChanged;

        struct LineMarkingInfo
        {
            public int Line;
            public string Tooltip;
            public CodeLineMarkingType Type;
        }

        public CodeBlockElement()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public bool StripNameSpaces { get; set; }
        public bool SaveWithDisclaimerOnly { get; set; }

        public void SetCodePreviewTitle(string title)
        {
            m_Toggle.text = "";
            m_CodeBlockTitle.text = string.IsNullOrEmpty(title) ? k_DefaultCodePreviewTitle : title;
        }

        public void SetCustomTitle(string title = null)
        {
            m_CodeBlockTitle.text = string.IsNullOrEmpty(title) ? k_DefaultCodePreviewTitle : title;
        }

        public void SetToggle(bool value)
        {
            ToggleDisplay(value);
        }

        public void SetCode(string code)
        {
            m_Code = code;
            RefreshCodeDisplay();
        }

        public void SetCodeReformatting(bool reformatCode)
        {
            m_ReformatCode = reformatCode;
        }

        public void SetCodeType(string codeType)
        {
            m_CodeType = codeType;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            RegisterCallback<DetachFromPanelEvent>(OnDetach);

            m_Controls = view.Q<VisualElement>("codeBlockControls");
            m_Content = view.Q<VisualElement>("codeBlockContent");
            m_ContentBackground = view.Q<VisualElement>("codeTextBackground");
            m_ContentForeground = view.Q<VisualElement>("codeTextForeground");

            m_LineNumberText = view.Q<Label>("lineNumberText");
            m_CodeScrollView = view.Q<ScrollView>("codeScrollView");

            // Attach callback to filter wheel events so that we can horizontally scroll with the trackpad
            // But vertical scrolling scroll higher level containers (instead of trying to horizontal scroll as well)
            // We also apply the same logic to the horizontal slider
            m_CodeScrollView.contentContainer.RegisterCallback<WheelEvent>(FilterVerticalScrollEvents, TrickleDown.TrickleDown);
            m_CodeScrollView.horizontalScroller?.RegisterCallback<WheelEvent>(FilterVerticalScrollEvents, TrickleDown.TrickleDown);

            m_CodeText = view.Q<Label>("codeText");
            m_CodeText.selection.isSelectable = true;

            m_Toggle = view.Q<Foldout>("codeBlockDisplayToggle");
            m_Toggle.RegisterValueChangedCallback(x =>
            {
                ToggleDisplay(x.newValue, true);
            });

            view.Q<VisualElement>("actionHeaderContainer").RegisterCallback<PointerUpEvent>(_ =>
            {
                ToggleDisplay(!m_Toggle.value, true);
            });

            // We are showing by default
            ToggleDisplay(true);

            m_SaveButton = view.SetupButton("saveCodeButton", OnSaveCodeClicked);
            m_SaveButtonImage = m_SaveButton.SetupImage("saveCodeButtonImage", "save");

            m_CopyButton = view.SetupButton("copyCodeButton", OnCopyCodeClicked);
            m_CopyButtonImage = m_CopyButton.SetupImage("copyCodeButtonImage", "copy");

            m_EditButton = view.SetupButton("editCodeButton", OnEditCodeClicked);
            m_EditButtonImage = m_EditButton.SetupImage("editCodeButtonImage", "edit");

            m_CodeBlockTitle = view.Q<Label>("codeBlockTitle");
            m_CodeBlockTitle.text = k_DefaultCodePreviewTitle;
            m_CodeBlockTitleIcon = view.SetupImage("codeBlockTitleIcon", "error");
            m_CodeBlockTitleIcon.SetDisplay(false);

            m_LineNumberController = new LineNumberController(m_CodeText, m_LineNumberText);
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            DeleteTempEditFile();
            m_FileWatcher?.Dispose();
        }

        internal void SetTitleIcon(bool isEnabled = true)
        {
            m_CodeBlockTitleIcon.SetDisplay(isEnabled);
        }

        public void SetActions(bool copy, bool save, bool select, bool edit)
        {
            m_CopyButton.SetDisplay(copy);
            m_SaveButton.SetDisplay(save);
            m_EditButton.SetDisplay(edit);
            m_CodeText.selection.isSelectable = select;
        }

        public void MarkLine(int lineNumber, CodeLineMarkingType type = CodeLineMarkingType.Error, string lineTooltip = "", bool refresh = true)
        {
            var info = new LineMarkingInfo { Line = lineNumber, Tooltip = lineTooltip, Type = type, };
            m_LineMarkings[lineNumber] = info;

            if (refresh)
            {
                RefreshCodeDisplay();
            }
        }

        public void UnmarkLine(int lineNumber, bool refresh = true)
        {
            m_LineMarkings.Remove(lineNumber);

            if (refresh)
            {
                RefreshCodeDisplay();
            }
        }

        public void EnableAnalytics()
        {
            m_AnalyticsEnabled = true;
        }

        string GetDisclaimerText()
        {
            return string.Format(AssistantConstants.DisclaimerText, DateTime.Now.ToShortDateString());
        }

        public void RefreshCodeDisplay()
        {
            // Update Code preview with AI disclaimer
            var disclaimerText = GetDisclaimerText();

            string code = m_Code;

            if (m_ReformatCode)
            {
                if (StripNameSpaces)
                {
                    // Remove namespaces from display
                    var tree = SyntaxFactory.ParseSyntaxTree(disclaimerText + code);
                    code = tree.RemoveNamespaces().GetText().ToString();
                }
                else
                {
                    code = CodeExportUtils.Format(code);
                }
            }
            else
            {
                var output = new StringBuilder();
                output.Append(string.Format(AssistantConstants.DisclaimerText, DateTime.Now.ToShortDateString()));
                output.Append(code);

                code = output.ToString();
            }

            string highlightedText = CodeSyntaxHighlight.Highlight(code);
            m_CodeText.text = highlightedText;

            RefreshMarkings();
        }

        public void DisplayErrors(CompilationErrors compilationErrors)
        {
            m_LineMarkings.Clear();

            var linesWithErrors = compilationErrors.Errors
                .Where(e => e.Line != -1)
                .Select(e => (e.Line + 4, e.Message)) // + 2 for Disclaimer Lines
                .Distinct();

            foreach (var error in linesWithErrors)
            {
                MarkLine(error.Item1, CodeLineMarkingType.Error, error.Item2);
            }

            RefreshCodeDisplay();
        }

        public void ClearDisplayedErrors()
        {
            m_LineMarkings.Clear();
            RefreshCodeDisplay();
        }

        void OnCopyCodeClicked(PointerUpEvent evt)
        {
            string disclaimerHeader = GetDisclaimerText();
            GUIUtility.systemCopyBuffer = string.Concat(disclaimerHeader, m_Code);

            m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            m_CopyButtonImage.SetOverrideIconClass("checkmark");
            TimerUtils.DelayedAction(ref s_CodeCopyButtonActiveTokenSource, () =>
            {
                m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_CopyButtonImage.SetOverrideIconClass(null);
            });

            AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.CopyCode, d =>
            {
                d.ConversationId = Context.Blackboard.ActiveConversation.Id.Value;
                d.ResponseMessage = m_Code;
            });
        }

        bool IsShaderType(string codeType)
        {
            return AssistantConstants.ShaderCodeBlockTypes.Any(t => codeType.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        void OnSaveCodeClicked(PointerUpEvent evt)
        {
            AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.SaveCode, d =>
            {
                d.ConversationId = Context.Blackboard.ActiveConversation.Id.Value;
                d.ResponseMessage = m_Code;
            });

            var defaultName = AssistantConstants.DefaultCodeBlockCsharpFilename;
            var defaultExtension = AssistantConstants.DefaultCodeBlockCsharpExtension;

            bool isCSharpLanguage = false;
            if (m_CodeType != null)
            {
                isCSharpLanguage = m_CodeType.StartsWith(AssistantConstants.CodeBlockCsharpFiletype, StringComparison.OrdinalIgnoreCase) ||
                                   m_CodeType.Contains(AssistantConstants.CodeBlockCsharpValidateFiletype);

                if (isCSharpLanguage)
                {
                    defaultName = CodeExportUtils.ExtractClassName(m_Code) ?? AssistantConstants.DefaultCodeBlockCsharpFilename;
                }
                else if (IsShaderType(m_CodeType))
                {
                    defaultName = AssistantConstants.DefaultCodeBlockShaderFilename;
                    defaultExtension = AssistantConstants.DefaultCodeBlockShaderExtension;
                }
                else
                {
                    defaultName = AssistantConstants.DefaultCodeBlockTextFilename;
                    defaultExtension = m_CodeType.Length > 0
                        ? m_CodeType.ToLowerInvariant()
                        : AssistantConstants.DefaultCodeBlockTextExtension;
                }
            }

            string file = EditorUtility.SaveFilePanel("Save Code", Application.dataPath, defaultName, defaultExtension);
            if (string.IsNullOrEmpty(file))
            {
                return;
            }

            EditorUtility.DisplayProgressBar("Saving Code", "Saving code to file", 0.5f);

            try
            {
                string output = m_Code;
                output = SaveWithDisclaimerOnly || !isCSharpLanguage
                    ? CodeExportUtils.AddDisclaimer(output)
                    : CodeExportUtils.Format(output, Path.GetFileNameWithoutExtension(file));

                File.WriteAllText(file, output);
            }
            catch (Exception)
            {
                ErrorHandlingUtils.ShowGeneralError("Failed to save code to file");
                EditorUtility.ClearProgressBar();
                return;
            }

            m_SaveButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            m_SaveButtonImage.SetOverrideIconClass("checkmark");
            TimerUtils.DelayedAction(ref s_SaveButtonActiveTokenSource, () =>
            {
                m_SaveButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_SaveButtonImage.SetOverrideIconClass(null);
            });

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        void OnEditCodeClicked(PointerUpEvent evt)
        {
            AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.EditCode);

            if (string.IsNullOrEmpty(m_TempEditedFilePath))
            {
                m_TempEditedFilePath = AssemblyCSProject.CreateTemporaryFile();

                File.WriteAllText(m_TempEditedFilePath, m_Code);

                CodeEditorProjectUtils.Sync();

                StartFileWatcher(m_TempEditedFilePath);
            }

            // Open it even if it already exists, this allows us to re-open the file if it was closed
            Process.Start(m_TempEditedFilePath);
        }

        void StartFileWatcher(string filepath)
        {
            m_FileWatcher?.Dispose();

            var directory = Path.GetDirectoryName(filepath);
            var fileName = Path.GetFileName(filepath);

            m_FileWatcher = new FileSystemWatcher(directory, fileName);
            m_FileWatcher.Changed += OnFileChanged;
            m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            m_FileWatcher.EnableRaisingEvents = true;
        }

        void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (File.Exists(m_TempEditedFilePath))
            {
                string updatedCode = File.ReadAllText(m_TempEditedFilePath);

                if (updatedCode != m_Code)
                {
                    TaskUtils.DispatchToMainThread(() =>
                    {
                        SetCode(updatedCode);
                        OnCodeChanged?.Invoke(updatedCode);
                    });
                }
            }
        }

        void DeleteTempEditFile()
        {
            if (string.IsNullOrEmpty(m_TempEditedFilePath))
                return;

            File.Delete(m_TempEditedFilePath);
            m_TempEditedFilePath = string.Empty;
            AssemblyCSProject.ClearTemporaryFiles();
        }

        void ToggleDisplay(bool isVisible, bool sendAnalytics = false)
        {
            m_Toggle.SetValueWithoutNotify(isVisible);
            m_Content.SetDisplay(isVisible);
            m_Controls.EnableInClassList(k_CollapsedControlsStyle, !isVisible);

            if (isVisible && m_AnalyticsEnabled && sendAnalytics)
                AIAssistantAnalytics.ReportUITriggerLocalEvent(UITriggerLocalEventSubType.ExpandCommandLogic);
        }

        void RefreshMarkings()
        {
            m_ContentBackground.Clear();
            m_ContentForeground.Clear();

            if (m_LineMarkings.Count == 0)
            {
                return;
            }

            // Note: This is very specific tuned to the current font settings, any change there will need to be adjusted here
            const float lineHeight = 15.83f;

            foreach (var info in m_LineMarkings.Values)
            {
                float elementPosition = lineHeight * (info.Line - 1);

                var markingElement = new VisualElement
                {
                    style =
                    {
                        marginTop = elementPosition,
                        height = lineHeight
                    }
                };

                markingElement.AddToClassList(k_MarkingStylePrefix + "bg");
                markingElement.AddToClassList(k_MarkingStylePrefix + info.Type.ToString().ToLowerInvariant());
                m_ContentBackground.Add(markingElement);

                if (!string.IsNullOrEmpty(info.Tooltip))
                {
                    var tooltipElement = new VisualElement
                    {
                        style =
                        {
                            marginTop = elementPosition,
                            height = lineHeight
                        }
                    };

                    tooltipElement.AddToClassList(k_MarkingStylePrefix+ "tooltip");
                    tooltipElement.AddToClassList(k_MarkingStylePrefix + "tooltip-" + info.Type.ToString().ToLowerInvariant());
                    tooltipElement.tooltip = info.Tooltip;
                    m_ContentForeground.Add(tooltipElement);
                }
            }
        }

        ScrollView GetFirstParentScrollForVerticalScrolling(ScrollView childScrollView, Vector2 globalMousePosition)
        {
            var parentScrollView = m_CodeScrollView.GetFirstAncestorOfType<ScrollView>();
            if (parentScrollView == null)
                return null;

            var localMousePosition = parentScrollView.WorldToLocal(globalMousePosition);
            if (parentScrollView.verticalScroller.visible && parentScrollView.ContainsPoint(localMousePosition))
                return parentScrollView;

            return GetFirstParentScrollForVerticalScrolling(parentScrollView, globalMousePosition);
        }

        void FilterVerticalScrollEvents(WheelEvent evt)
        {
            var delta = evt.delta;
            var scrollFactor = m_CodeScrollView.mouseWheelScrollSize;
            var isHorizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);

            evt.StopImmediatePropagation();

            if (isHorizontal && m_CodeScrollView.horizontalScroller.visible)
            {
                m_CodeScrollView.scrollOffset += new Vector2(delta.x * scrollFactor, 0);
            }
            else
            {
                // Note: Unity does not properly handle newly created wheel events so we directly scroll any parent scrollview directly
                var parentScrollView = GetFirstParentScrollForVerticalScrolling(m_CodeScrollView, evt.mousePosition);
                if (parentScrollView != null)
                {
                    var parentScrollFactor = parentScrollView.mouseWheelScrollSize;
                    parentScrollView.scrollOffset += new Vector2(0, delta.y * parentScrollFactor);
                }
            }
        }
    }
}
