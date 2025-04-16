using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Ai.Assistant.Protocol.Client;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.Backend
{
    static class ConfigurationUtility
    {
        public static void SetAccessToken([NotNull] this Configuration @this, string accessToken)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            @this.DefaultHeaders["Authorization"] = $"Bearer {accessToken}";
        }

        public static void SetDynamicAccessToken([NotNull] this Configuration @this, bool active)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            if (active)
                @this.DynamicHeaders["Authorization"] = () => $"Bearer {CloudProjectSettings.accessToken}";
            else
                @this.DynamicHeaders.Remove("Authorization");
        }
    }
}
