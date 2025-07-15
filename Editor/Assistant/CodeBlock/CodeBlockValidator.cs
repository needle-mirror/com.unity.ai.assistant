using System.IO;
using Unity.AI.Assistant.CodeAnalyze;
using Unity.AI.Assistant.Editor.CodeAnalyze;

namespace Unity.AI.Assistant.Editor.CodeBlock
{
    internal class CodeBlockValidator
    {
        internal const string k_ValidatorAssemblyName = "Unity.Muse.CodeGen";

        readonly DynamicAssemblyBuilder m_Builder = new(k_ValidatorAssemblyName);

        public bool ValidateCode(string code, out string localFixedCode, out CompilationErrors compilationErrors)
        {
            using var stream = new MemoryStream();
            return m_Builder.TryCompileCode(code, stream, out compilationErrors, out localFixedCode);
        }
    }
}
