using System;
using Unity.Ai.Assistant.Protocol.Api;
using Unity.Ai.Assistant.Protocol.Client;
using Unity.Ai.Assistant.Protocol.Model;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Protocol
{
    static class ProtocolUtility
    {
        public static IGetAssistantConversationInfoV1RequestBuilder GetConversationInfoV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId)
        {
            return api
                .GetAssistantConversationInfoV1Builder(
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion()
                );
        }

        public static IPutAssistantConversationInfoGenerateTitleUsingConversationIdV1RequestBuilder PutAssistantConversationInfoGenerateTitleUsingConversationIdV1BuilderWithAnalytics(
            this IAiAssistantApi api, string orgId, Guid conversationId)
        {
            return api
                .PutAssistantConversationInfoGenerateTitleUsingConversationIdV1Builder(
                    conversationId: conversationId,
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion()
                );
        }

        public static IGetAssistantConversationUsingConversationIdV1RequestBuilder GetAssistantConversationUsingConversationIdV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId, Guid conversationId)
        {
            return api
                .GetAssistantConversationUsingConversationIdV1Builder(
                    conversationId: conversationId,
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion()
                );
        }

        public static IPatchAssistantConversationInfoUsingConversationIdV1RequestBuilder PatchAssistantConversationInfoUsingConversationIdV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId, Guid conversationId, ConversationInfoUpdateV1 body)
        {
            return api
                .PatchAssistantConversationInfoUsingConversationIdV1Builder(
                    conversationId: conversationId,
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion(),
                    requestBody: body
                );
        }

        public static IDeleteAssistantConversationUsingConversationIdV1RequestBuilder DeleteAssistantConversationUsingConversationIdV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId, Guid conversationId)
        {
            return api
                .DeleteAssistantConversationUsingConversationIdV1Builder(
                    conversationId: conversationId,
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion()
                );
        }

        public static IGetAssistantInspirationV1RequestBuilder GetAssistantInspirationV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId)
        {
            return api
                .GetAssistantInspirationV1Builder(
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion()
                );
        }

        public static IGetAssistantMessagePointsV1RequestBuilder GetAssistantMessagePointsV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId)
        {
            return api
                .GetAssistantMessagePointsV1Builder(
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion()
                );
        }

        public static IPostAssistantFeedbackV1RequestBuilder PostAssistantFeedbackV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId, FeedbackCreationV1 body)
        {
            return api
                .PostAssistantFeedbackV1Builder(
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion(),
                    requestBody: body
                );
        }

        public static IGetAssistantFeedbackUsingConversationIdAndMessageIdV1RequestBuilder GetAssistantFeedbackUsingConversationIdAndMessageIdV1RequestBuilderWithAnalytics(
            this IAiAssistantApi api, string orgId, string conversationId, string messageId)
        {
            return api
                .GetAssistantFeedbackUsingConversationIdAndMessageIdV1Builder(
                    conversationId: conversationId,
                    messageId: messageId,
                    analyticsSessionCount: GetAnalyticsSessionCount(),
                    analyticsSessionId: GetAnalyticsSessionId(),
                    analyticsUserId: GetAnalyticsUserId(),
                    orgId: orgId,
                    projectId: GetProjectId(),
                    versionApiSpecification: GetApiVersion(),
                    versionEditor: GetEditorVersion(),
                    versionPackage: GetPackageVersion()
                );
        }

        public static int GetAnalyticsSessionCount() => (int)EditorAnalyticsSessionInfo.sessionCount;
        public static string GetAnalyticsSessionId() => EnsureAnalyticsString(EditorAnalyticsSessionInfo.id.ToString());
        public static string GetAnalyticsUserId() => EnsureAnalyticsString(EditorAnalyticsSessionInfo.userId);

        public static string GetProjectId() => PlayerSettings.productGUID.ToString();
        public static string GetApiVersion() => Configuration.Version;
        public static string GetEditorVersion() => Application.unityVersion;

        [Serializable]
        public class PackageJson
        {
            public string version;
        }

        public static string GetPackageVersion()
        {
            try
            {
                string path = "Packages/com.unity.ai.assistant/package.json";
                TextAsset packageJson = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (packageJson == null)
                {
                    Debug.LogError($"Failed to load package.json at path: {path}");
                    return "could-not-load-package-info";
                }

                PackageJson packageData = JsonUtility.FromJson<PackageJson>(packageJson.text);
                return packageData.version;
            }
            catch (Exception e)
            {
                return "could-not-load-package-info";
            }
        }

        public static string EnsureAnalyticsString(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
                candidate = "could-not-load-analytics";

            return candidate;
        }
    }
}
