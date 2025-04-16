using System.Text.RegularExpressions;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    static class ContextViewUtils
    {
        public static string GetObjectTooltip(Object obj)
        {
            var type = obj.GetType().ToString();
            var idx = type.LastIndexOf('.');

            if (idx != -1)
                type = type.Substring(idx + 1);

            return $"{obj.name} ({AddSpacesBeforeCapitals(type)})";
        }

        public static string GetObjectTooltipByName(string objName, string typeName)
        {
            var type = typeName;
            var idx = type.LastIndexOf('.');

            if (idx != -1)
                type = type.Substring(idx + 1);

            return $"{objName} ({AddSpacesBeforeCapitals(type)})";
        }

        static string AddSpacesBeforeCapitals(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var newText = Regex.Replace(text, "(?<=[a-z])([A-Z])", " $1", RegexOptions.Compiled).Trim();
            return newText;
        }

        public static string GetShortTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            if (fullTypeName.Contains('.'))
            {
                string[] parts = fullTypeName.Split('.');
                return parts[^1];
            }

            return fullTypeName;
        }
    }
}
