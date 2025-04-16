using System;
using System.Reflection;
using Unity.AI.Assistant.AvatarHelper;
using Unity.AI.Assistant.AvatarHelper.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    internal static class UserInfoCache
    {
        const string k_ConnectAssembly = "UnityEditor.Connect.UnityConnect";
        const string k_UserInfoType = "UnityEditor.Connect.UserInfo";
        const string k_UserInfoMethod = "GetUserInfo";
        const string k_UserInstanceProperty = "instance";
        const string k_UserInfoDisplayNameProperty = "displayName";
        const string k_UserInfoIdProperty = "userId";

        public static string DisplayName;
        public static Texture2D Avatar;

        public static void Refresh()
        {
            try
            {
                var connectAssembly = TypeDef<CloudProjectSettings>.Assembly;
                var unityConnectType = connectAssembly.GetType(k_ConnectAssembly);
                var userInfoMethod = unityConnectType.GetMethod(k_UserInfoMethod);
                var instanceProperty = unityConnectType.GetProperty(k_UserInstanceProperty, BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProperty.GetValue(null, null);

                var userInfo = userInfoMethod.Invoke(instance, null);

                var userInfoType = connectAssembly.GetType(k_UserInfoType);
                var displayNameProp = userInfoType.GetProperty(k_UserInfoDisplayNameProperty);
                DisplayName = (string)displayNameProp.GetValue(userInfo);

                var userIdProp = userInfoType.GetProperty(k_UserInfoIdProperty);
                var userId = (string)userIdProp.GetValue(userInfo);

                UserAvatarHelper.GetUserAvatar(userId, (icon) =>
                {
                    if (icon != null)
                    {
                        Avatar = icon;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
