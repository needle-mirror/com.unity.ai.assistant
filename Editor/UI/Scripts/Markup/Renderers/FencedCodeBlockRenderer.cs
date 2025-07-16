using System;
using System.Text;
using Markdig.Renderers;
using Markdig.Syntax;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Markup.Renderers
{
    internal class FencedCodeBlockRenderer : MarkdownObjectRenderer<ChatMarkdownRenderer, FencedCodeBlock>
    {
        readonly AssistantUIContext m_Context;

        public FencedCodeBlockRenderer(AssistantUIContext context)
        {
            m_Context = context;
        }

        protected override void Write(ChatMarkdownRenderer renderer, FencedCodeBlock obj)
        {
            var fullCodeBlock = new StringBuilder();

            for (int i = 0; i < obj.Lines.Count; i++)
            {
                var lineWithoutEscapes = obj.Lines.Lines[i].ToString().Replace(@"\", @"\\");

                fullCodeBlock.Append(lineWithoutEscapes);
                if (i < obj.Lines.Count - 1)
                    fullCodeBlock.Append("\n");
            }

            var codeText = fullCodeBlock.ToString();

            CommandDisplayTemplate displayBlock = null;

            // Finish all pending non-code text/formatting
            renderer.CloseTextElement();

            // Look for a special command block or use the default one
            displayBlock = ChatCommandElementRegistry.CreateElement(obj.Info) as CommandDisplayTemplate ?? new ChatElementCommandCodeBlock();

            // If this is a shared element, we skip doing the additional setup
            if (!renderer.m_OutputTextElements.Contains(displayBlock))
            {
                var isCSharpLanguage =
                    (obj.Info != null && (obj.Info.StartsWith(AssistantConstants.CodeBlockCsharpFiletype, StringComparison.OrdinalIgnoreCase) ||
                                          obj.Info.Contains(AssistantConstants.CodeBlockCsharpValidateFiletype)));

                displayBlock.Fence = obj.Info;
                displayBlock.Initialize(m_Context);
                displayBlock.SetContent(codeText);
                displayBlock.SetCodeType(obj.Info);
                renderer.m_OutputTextElements.Add(displayBlock);

                if (!isCSharpLanguage)
                {
                    if (obj.Info != null)
                    {
                        displayBlock.SetCustomTitle(obj.Info.ToUpper());
                    }

                    // Don't treat non-C# code as needing reformatting or code adjustments
                    displayBlock.SetCodeReformatting(false);
                }

                // Create the element if it is opened and complete
                if (!obj.IsOpen)
                {
                    displayBlock.Validate(0);
                    displayBlock.Display();
                }
            }
            else
            {
                displayBlock.AddContent(codeText);
            }
        }
    }
}
