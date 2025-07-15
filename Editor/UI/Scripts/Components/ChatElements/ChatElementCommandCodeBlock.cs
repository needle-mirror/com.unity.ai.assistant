using Unity.AI.Assistant.CodeAnalyze;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementCommandCodeBlock : CommandDisplayTemplate
    {
        const string k_FailMessage = "The generated script was not able to compile in your project. Try to correct any errors, or generate it again.";

        VisualElement m_WarningBlock;
        VisualElement m_WarningContainer;
        Label m_WarningText;

        CodeBlockElement m_CodeElement;
        CompilationErrors m_Errors;

        public bool ValidateCode { get; set; }

        public ChatElementCommandCodeBlock()
            : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_CodeElement = new CodeBlockElement();
            m_CodeElement.Initialize(Context);
            m_CodeElement.SaveWithDisclaimerOnly = ValidateCode;

            var codeBlockRoot = view.Q<VisualElement>("commandCodeBlockRoot");
            codeBlockRoot.Add(m_CodeElement);

            m_WarningBlock = view.Q<VisualElement>("warningBlock");
            m_WarningBlock.SetDisplay(false);

            m_WarningContainer = view.Q<VisualElement>("warningContainer");
            m_WarningContainer.style.marginBottom = 10;

            m_WarningText = view.Q<Label>("warningText");
        }

        public override void Display(bool isUpdate = false)
        {
            if (ValidateCode)
            {
                var content = ContentGroups[0];

                switch (content.State)
                {
                    case DisplayState.Success:
                    {
                        m_CodeElement.SetCode(ContentGroups[0].Content);
                        m_CodeElement.SetActions(true, true, true, false);
                        break;
                    }

                    case DisplayState.Fail:
                    {
                        FailCode(k_FailMessage);
                        m_CodeElement.SetCode(ContentGroups[0].Content);
                        m_CodeElement.SetActions(true, true, true, false);
                        break;
                    }
                }
            }
            else
            {
                m_CodeElement.SetCode(ContentGroups[0].Content);
                m_CodeElement.SetActions(true, true, true, false);
            }
        }

        protected void SetWarningText(string text)
        {
            m_WarningText.text = text;
            m_WarningBlock.SetDisplay(!string.IsNullOrWhiteSpace(text));
            m_WarningText.selection.isSelectable = true;
        }

        protected override bool ValidateInternal(int index, out string logs)
        {
            if (!ValidateCode)
            {
                logs = string.Empty;
                return true;
            }

            var contentGroup = ContentGroups[index];

            var valid = Context.API.ValidateCode(contentGroup.Content, out var localRepairedCode, out m_Errors);
            logs = m_Errors.ToString();
            contentGroup.Content = localRepairedCode;
            return valid;
        }

        void FailCode(string message)
        {
            SetWarningText(message);

            m_CodeElement.DisplayErrors(m_Errors);
        }

        public override void SetCustomTitle(string title)
        {
            m_CodeElement.SetCustomTitle(title);
        }

        public override void SetCodeReformatting(bool reformatCode)
        {
            m_CodeElement.SetCodeReformatting(reformatCode);
        }
    }
}
