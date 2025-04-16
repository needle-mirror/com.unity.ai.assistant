using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class AssistantConstants
    {
        internal const int MaxConversationHistory = 1000;
        internal const int MaxFeedbackMessageLength = 1000;
        internal const int MaxPromptLength = 4000;

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
// This was created with assistance from Muse, a Unity Artificial Intelligence product

";
    }
}
