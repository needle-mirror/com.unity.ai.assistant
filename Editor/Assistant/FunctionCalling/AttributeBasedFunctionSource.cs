using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.Context.SmartContext;
using Unity.AI.Assistant.Editor.Plugins;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    class AttributeBasedFunctionSource : IFunctionSource
    {
        /// <summary>
        ///     Returns all methods marked with the <see cref="ContextProviderAttribute"/> or the
        ///     <see cref="PluginAttribute"/> that meet the requirements for being a context provider or a plugin.
        /// </summary>
        public CachedFunction[] GetFunctions()
        {
            List<CachedFunction> methods = new();

            PopulateListFromAttribute<ContextProviderAttribute>(
                method => method.IsStatic && method.ReturnType == typeof(SmartContextToolbox.ExtractedContext),
                attribute => attribute.Description
            );

            PopulateListFromAttribute<PluginAttribute>(
                method => method.IsStatic || method.ReturnType == typeof(void),
                attribute => attribute.Description,
                (attribute, dictionary) =>
                    {
                        dictionary[PluginToolbox.ButtonTextMetadataKey] = attribute.ActionText;
                        dictionary[PluginToolbox.PromptTextMetadataKey] = attribute.DisplayText;
                        dictionary[PluginToolbox.BlockTextMetadataKey] = attribute.ToolName;
                    }
            );

            return methods.ToArray();

            void PopulateListFromAttribute<T>(
                Predicate<MethodInfo> validator,
                Func<T, string> descriptionExtractor,
                Action<T, Dictionary<string, string>> metaDataCollector = null)
                where T : Attribute
            {
                // Get all methods marked with the ContextProviderAttribute attribute.
                methods.AddRange(
                    TypeCache.GetMethodsWithAttribute<T>()
                        .Where(methodInfo =>
                        {
                            if (!validator(methodInfo))
                            {
                                Debug.LogWarning(
                                    $"Method \"{methodInfo.Name}\" in \"{methodInfo.DeclaringType?.FullName}\" failed" +
                                    $"validation. This means it does not have the appropriate function signature for" +
                                    $"the given attribute {typeof(T).Name}");
                                return false;
                            }

                            return true;
                        })
                        .Where(method => method.GetCustomAttribute<T>() != null)
                        .Select(method =>
                        {
                            var att = method.GetCustomAttribute<T>();

                            Dictionary<string, string> metaData = new();
                            metaDataCollector?.Invoke(method.GetCustomAttribute<T>(), metaData);

                            // If the method is defined in a dynamic command handler, it gets a special tag instead
                            if (ChatCommands.TryGetCommandHandler(method.DeclaringType, out var commandHandler))
                            {
                                return new CachedFunction
                                {
                                    MetaData = metaData,
                                    Method = method,
                                    FunctionDefinition = FunctionCallingUtilities.GetFunctionDefinition(
                                    method,
                                    descriptionExtractor(att),
                                    FunctionCallingUtilities.GetTagForCommandAttribute(att, commandHandler))
                                };
                            }
                            return new CachedFunction
                            {
                                MetaData = metaData,
                                Method = method,
                                FunctionDefinition = FunctionCallingUtilities.GetFunctionDefinition(
                                    method,
                                    descriptionExtractor(att),
                                    FunctionCallingUtilities.GetTagForAttribute(att))
                            };
                        })
                );
            }
        }
    }
}
