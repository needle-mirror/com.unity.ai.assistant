using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    internal abstract class FunctionToolbox
    {
        protected readonly Dictionary<string, CachedFunction> k_Tools = new();
        protected readonly Dictionary<string, CachedFunction> k_ToolsById = new();

        public IEnumerable<CachedFunction> Tools => k_Tools.Values;

        public FunctionToolbox(FunctionCache functionCache, params string[] tags)
        {
            // Build list of available tool methods:
            k_Tools.Clear();

            foreach (CachedFunction cachedFunction in functionCache.GetFunctionsByTags(tags))
            {
                // Silent fail if multiple functions with same key are added
                k_Tools.TryAdd(cachedFunction.FunctionDefinition.Name, cachedFunction);
                k_ToolsById.TryAdd(cachedFunction.FunctionDefinition.FunctionId, cachedFunction);
            }
        }

        public bool ContainsFunctionById(string id) => k_ToolsById.ContainsKey(id);

        public bool TryGetToolMetadataByID(string id, string metadataKey, out string actionText)
        {
            actionText = string.Empty;

            if (!k_ToolsById.TryGetValue(id, out var tool) || tool.MetaData == null)
                return false;

            actionText = tool.MetaData[metadataKey];
            return string.IsNullOrEmpty(actionText);
        }

        public void GetSelectorIdAndConvertArgs(string id, string[] args, out CachedFunction function, out object[] convertedArgs)
        {
            convertedArgs = null;
            if (!k_ToolsById.TryGetValue(id, out function))
                throw new Exception($"Tool {id} does not exist.");

            ConvertArgsFromFunction(args, function, out convertedArgs);
        }

        public void GetSelectorAndConvertArgs(string name, string[] args, out CachedFunction function, out object[] convertedArgs)
        {
            convertedArgs = null;
            if (!k_Tools.TryGetValue(name, out function))
                throw new Exception($"Tool {name} does not exist.");

            ConvertArgsFromFunction(args, function, out convertedArgs);
        }

        void ConvertArgsFromFunction(string[] args, CachedFunction function, out object[] convertedArgs)
        {
            convertedArgs = null;

            if (function.FunctionDefinition.Parameters == null || function.FunctionDefinition.Parameters.Count == 0)
            {
                convertedArgs = Array.Empty<object>();
                return;
            }

            // Check what parameters are required:
            var requiredArgCount = function.FunctionDefinition?.Parameters?.Count(parameter => !parameter.Optional) ?? 0;

            if (args.Length < requiredArgCount)
                throw new ArgumentException(
                    $"The incorrect number of args were provided. Given args: {string.Join(",", args)}. Required Args: {string.Join(",", function.FunctionDefinition?.Parameters!.Where(p => !p.Optional).Select(p => p.Name)!)}");

            convertedArgs = new object[function.FunctionDefinition.Parameters.Count];

            string[] argNames = function
                .FunctionDefinition
                .Parameters
                .Select(param => param.Name)
                .ToArray();

            Func<string, object>[] converters = function
                .FunctionDefinition
                .Parameters
                .Select(param => FunctionCallingUtilities.GetConverter(param.Type))
                .ToArray();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (!arg.Contains(":"))
                {
                    InternalLog.LogWarning(
                        $"SmartContextError: The LLM did not return an arg as a named arg. Assuming it is a positional arg");

                    try
                    {
                        convertedArgs[i] = converters[i](arg);
                        continue;
                    }
                    catch (Exception)
                    {
                        throw new Exception($"The argument at index {i} could not be converted to a valid type.");
                    }
                }

                try
                {
                    // Split arg at first ":":
                    var splitIndex = arg.IndexOf(":", StringComparison.Ordinal);
                    var argName = arg[..splitIndex];
                    var argValue = arg[(splitIndex + 1)..];

                    var namedindex = Array.IndexOf(argNames, argName);

                    convertedArgs[namedindex] = converters[namedindex](argValue);

                    // TODO: This is a temporary fix for a backend issue, remove this when the backend is fixed:
                    if (convertedArgs[namedindex] as string == "AttributedDict()")
                    {
                        convertedArgs[namedindex] = "";
                    }

                    if (convertedArgs[namedindex] is object[] objArray)
                    {
                        for (var objArrayIdx = 0; objArrayIdx < objArray.Length; objArrayIdx++)
                        {
                            var o = objArray[objArrayIdx];
                            if (o is string s && s.Trim() == "AttributedDict()")
                            {
                                objArray[objArrayIdx] = "";
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    throw new Exception($"{arg} is not a valid argument.");
                }
            }
        }
    }
}
