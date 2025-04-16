using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    static class SharedDisplayTemplates
    {
        static readonly Dictionary<Type, CommandDisplayTemplate> k_SharedTemplates = new();

        internal static void Reset()
        {
            k_SharedTemplates.Clear();
        }

        public static T GetSharedTemplate<T>() where T : CommandDisplayTemplate, new()
        {
            if (!k_SharedTemplates.TryGetValue(typeof(T), out var template))
            {
                template = new T();
                k_SharedTemplates[typeof(T)] = template;
            }

            return template as T;
        }
    }
}
