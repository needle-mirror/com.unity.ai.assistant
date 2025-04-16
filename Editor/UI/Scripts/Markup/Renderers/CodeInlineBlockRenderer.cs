using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using Unity.AI.Assistant.Editor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Markup.Renderers
{
    internal class CodeInlineBlockRenderer : MarkdownObjectRenderer<ChatMarkdownRenderer, CodeInline>
    {
        protected override void Write(ChatMarkdownRenderer renderer, CodeInline obj)
        {
            var codeWithoutEscapes = obj.Content.Replace(@"\", @"\\");

            //Note: "<noparse>" ensures that quoted code containing tags is not interpreted as actual rich text tags
            renderer.AppendText($"<mark={AssistantConstants.CodeColorBackground}> <color={AssistantConstants.CodeColorText}><noparse>{codeWithoutEscapes}</noparse></color> </mark>");
        }
    }
}
