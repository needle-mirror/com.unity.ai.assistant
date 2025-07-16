using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class AssistantConstants
    {
        internal const string PackageName = "com.unity.ai.assistant";

        internal const int MaxConversationHistory = 1000;

        internal const string TextCutoffSuffix = "...";

        internal static readonly string SourceReferenceColor = EditorGUIUtility.isProSkin ? "4c7effff" : "055b9fff";
        internal static readonly string SourceReferencePrefix = "REF:";

        internal static readonly string CodeColorBackground = EditorGUIUtility.isProSkin ? "#78787860" : "#E2E2E260";
        internal static readonly string CodeColorText = EditorGUIUtility.isProSkin ? "#C6C6C6" : "#363636";

        internal const string ProjectIdTagPrefix = "projId:";

        internal const string ContextTag = "#PROJECTCONTEXT#";
        internal static readonly string ContextTagEscaped = ContextTag.Replace("#", @"\#");

        internal const bool DebugMode = false;
        internal const string MediationPrompt = "";
        internal const bool SkipPlanning = false;

        internal const int SuggestedSelectedContextLimit = 5;

        internal const int AttachedContextDisplayLimit = 8;

        internal const string DisclaimerText = @"// {0} AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

";

        internal const string DefaultCodeBlockCsharpFilename = "Code";
        internal const string DefaultCodeBlockCsharpExtension = "cs";
        internal const string DefaultCodeBlockShaderFilename = "NewShader";
        internal const string DefaultCodeBlockShaderExtension = "shader";
        internal const string DefaultCodeBlockTextFilename = "Output";
        internal const string DefaultCodeBlockTextExtension = "txt";

        internal static readonly string[] ShaderCodeBlockTypes = new string[] { "glsl", "hlsl", "shader" };

        internal const string CodeBlockCsharpFiletype = "cs";
        internal const string CodeBlockCsharpValidateFiletype = "validate-csharp";
    }
}
