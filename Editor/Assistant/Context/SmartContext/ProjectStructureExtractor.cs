using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.Editor.Plugins;
using Unity.AI.Assistant.Editor.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.Context.SmartContext
{
    internal static partial class ContextRetrievalTools
    {
        internal class ProjectHierarchyInfo : IParentable<ProjectHierarchyInfo>
        {
            public readonly string FileInfoName;
            public ProjectHierarchyInfo Parent { get; }

            private string m_Suffix;
            public string Suffix => m_Suffix ?? string.Empty;

            public ProjectHierarchyInfo(string fileInfoName, ProjectHierarchyInfo parent, [CanBeNull] string suffix = null)
            {
                FileInfoName = fileInfoName;
                Parent = parent;
                m_Suffix = suffix;
            }

            public override bool Equals(object obj)
            {
                if (obj is not ProjectHierarchyInfo other)
                {
                    return false;
                }

                return FileInfoName == other.FileInfoName && Parent == other.Parent;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(FileInfoName, Parent);
            }
        }

        internal class ProjectHierarchyMapEntry : HierarchyMapEntry<ProjectHierarchyInfo>
        {
            public ProjectHierarchyMapEntry(ProjectHierarchyInfo obj) : base(obj)
            {
            }

            public override string ObjectName => k_ObjectRef?.FileInfoName + k_ObjectRef?.Suffix;

            protected override HierarchyMapEntry<ProjectHierarchyInfo> CreateInstance(ProjectHierarchyInfo obj, HierarchyMapEntry<ProjectHierarchyInfo> parent)
            {
                return new ProjectHierarchyMapEntry(obj);
            }
        }

        [ContextProvider("Returns the file structure under the Assets/ folder.")]
        internal static SmartContextToolbox.ExtractedContext ProjectStructureExtractor(
            [Parameter("Filter to specify which files or assets to include. Use an empty string if the full project hierarchy is needed." +
                       "Optional: Add one asset type per name, by adding a suffix in the name starting with '|type:' followed by the asset type, with one from the following asset types: script, mesh, texture, material, audioclip, sprite, model, prefab, shader, and animationclip. " +
                       "To search for a asset type with any name a '*' wildcard instead of an asset name works, e.g. the parameter '*|type:texture' searches all texture assets without checking the asset names.")]
            params string[] assetNameFilters)
        {
            if (assetNameFilters == null || assetNameFilters.Length == 0)
            {
                assetNameFilters = new[] { string.Empty };
            }

            var resultPrefix = "Filter:";
            var result = new SmartContextToolbox.ExtractedContext();

            // Store all objects in a tree structure first, then serialize it:
            var hierarchyMap = new ProjectHierarchyMapEntry(null);

            var assetPath = new DirectoryInfo(Path.GetFullPath(Application.dataPath));

            Dictionary<string, ProjectHierarchyInfo> parentMap = new();

            ProjectHierarchyMapEntry.Reset();

            var finalNameFilters = new List<string>();
            var finalTypeFilters = new List<string>();

            // For each filtered name create a matching assetTypeFilters entry, an empty string or a given type
            string[] assetTypeFilters = null;
            if (assetNameFilters is { Length: > 0 })
            {
                for (int i = 0; i < assetNameFilters.Length; i++)
                {
                    var filter = assetNameFilters[i];

                    InternalLog.Log($"ProjectStructureExtractor - Checking '{assetNameFilters[i]}' for any type filters...");

                    // See if "|type:" or "|~type:" is in the string, and extract the two strings before and after that
                    var split = Regex.Split(filter, @"\|type:|\|~type:");
                    if (split.Length == 2)
                    {
                        var name = split[0];

                        // Note: Smart Context thinks '!' is a valid negation operator
                        if (name is "*" or "!" or "!*")
                        {
                            name = "";
                        }

                        // Has another type at the start
                        if (name.StartsWith("type:"))
                        {
                            var firstType = name.Substring("type:".Length);

                            name = "";

                            if (!NameAndTypeExist(finalNameFilters, finalTypeFilters, name, firstType))
                            {
                                finalNameFilters.Add(name);
                                finalTypeFilters.Add(firstType);

                                InternalLog.Log(
                                    $"Found 1st additional object name '{name}' with component filter: '{firstType}'");
                            }
                        }

                        if (!NameAndTypeExist(finalNameFilters, finalTypeFilters, name, split[1]))
                        {
                            finalNameFilters.Add(name);
                            finalTypeFilters.Add(split[1]);

                            InternalLog.Log($"Found object name '{name}' with component filter: '{split[1]}'");
                        }
                    }
                    else
                    {
                        var name = assetNameFilters[i];
                        if (name is "*")
                        {
                            name = "";
                        }

                        if (!NameAndTypeExist(finalNameFilters, finalTypeFilters, name, string.Empty))
                        {
                            finalNameFilters.Add(name);
                            finalTypeFilters.Add(string.Empty);
                        }
                    }
                }
            }

            assetNameFilters = finalNameFilters.ToArray();
            assetTypeFilters = finalTypeFilters.ToArray();

            List<string> assetPathsToAdd = new();
            Dictionary<Object, List<Object>> assetsToSubAssets = new();

            for (int i = 0; i < assetNameFilters.Length; i++)
            {
                var assetName = assetNameFilters[i];
                var assetType = assetTypeFilters[i];

                // If there is no filter given, extract everything:
                if (string.IsNullOrEmpty(assetName) && string.IsNullOrEmpty(assetType))
                {
                    if (i > 0)
                    {
                        resultPrefix += ", plus";
                    }
                    resultPrefix += " all files and assets";

                    var directoriesToProcess = new Queue<DirectoryInfo>(new[] { assetPath });

                    while (directoriesToProcess.Count > 0)
                    {
                        var subDir = directoriesToProcess.Dequeue();
                        ProcessDirectory(subDir);

                        if (ProjectHierarchyMapEntry.EstimatedSerializedLength >
                            AssistantMessageSizeConstraints.ContextLimit)
                        {
                            result.Truncated = true;
                            break;
                        }

                        continue;

                        void ProcessDirectory(DirectoryInfo dir)
                        {
                            foreach (var file in dir.GetFiles())
                            {
                                // Ignore meta files:
                                if (file.Extension == ".meta")
                                    continue;

                                var info = new ProjectHierarchyInfo(file.Name, GetParentInfo(file.Directory));

                                hierarchyMap.Insert(info);

                                if (ProjectHierarchyMapEntry.EstimatedSerializedLength >
                                    AssistantMessageSizeConstraints.ContextLimit)
                                {
                                    result.Truncated = true;
                                    break;
                                }
                            }

                            foreach (var subDir in dir.GetDirectories())
                            {
                                var info = new ProjectHierarchyInfo(subDir.Name, GetParentInfo(subDir.Parent));

                                hierarchyMap.Insert(info);

                                directoriesToProcess.Enqueue(subDir);

                                if (ProjectHierarchyMapEntry.EstimatedSerializedLength >
                                    AssistantMessageSizeConstraints.ContextLimit)
                                {
                                    result.Truncated = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (assetName.StartsWith("*."))
                    {
                        assetName = assetName[2..];
                    }

                    // For specific searches, include entire project directory.
                    // We'll need to think about this more, it leads to a lof of false positives:
                    // const bool includeOnlyAssetPath = true;
                    // // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    // if (!includeOnlyAssetPath)
//#pragma warning disable CS0162 // Unreachable code detected
                    // {
                    //     assetPath = assetPath.Parent;
                    // }
//#pragma warning restore CS0162 // Unreachable code detected

                    result.Truncated = true;

                    if (i > 0)
                    {
                        resultPrefix += ", plus";
                    }
                    var nameFilter = string.IsNullOrEmpty(assetName) ? "any name" : $"'{assetName}'";
                    var typeFilter = string.IsNullOrEmpty(assetType) ? "any type" : $"'{assetType}'";
                    resultPrefix += $" names matching {nameFilter} and asset type is {typeFilter}";

                    // Map main assets to sub assets
                    var assetsAndSubAssets = TryFindAssets(assetName, assetType);
                    if (assetsAndSubAssets?.Count() > 0)
                    {
                        foreach (var asset in assetsAndSubAssets)
                        {
                            var path = AssetDatabase.GetAssetPath(asset);
                            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                            if (!assetPathsToAdd.Contains(path))
                            {
                                assetPathsToAdd.Add(path);
                            }

                            if (AssetDatabase.IsSubAsset(asset))
                            {
                                if (assetsToSubAssets.TryGetValue(mainAsset, out var subAssets))
                                {
                                    subAssets.Add(asset);
                                }
                                else
                                {
                                    assetsToSubAssets[mainAsset] = new List<Object> { asset };
                                }
                            }
                        }
                    }
                }
            }

            // Build hierarchy info
            StringBuilder sb = new();
            foreach (var file in assetPathsToAdd)
            {
                var mainAsset = AssetDatabase.LoadMainAssetAtPath(file);
                string suffix = null;

                if (mainAsset != null && assetsToSubAssets.TryGetValue(mainAsset, out var subAssets))
                {
                    // create a dictionary to group assets by type in a List<Object>
                    Dictionary<Type, List<Object>> subAssetsByType = new();
                    foreach (var s in subAssets)
                    {
                        var type = s.GetType();
                        if (subAssetsByType.TryGetValue(type, out var list))
                        {
                            list.Add(s);
                        }
                        else
                        {
                            subAssetsByType[type] = new List<Object> { s };
                        }
                    }

                    sb.Clear();
                    sb.Append(" - ");

                    // List sub assets in groups of types (like "type1: asset1, asset2; type2: asset3")
                    bool hasEntries = false;
                    foreach (var subAssetsOfType in subAssetsByType)
                    {
                        if (hasEntries)
                        {
                            sb.Append("; ");
                        }

                        sb.Append(subAssetsOfType.Key.Name);
                        sb.Append(" assets: ");

                        bool hasAssetEntries = false;
                        foreach (var s in subAssetsOfType.Value)
                        {
                            if (s == null)
                                continue;

                            if (s.name != null)
                            {
                                if (s.name.Length > 0)
                                {
                                    if (hasAssetEntries)
                                    {
                                        sb.Append(", ");
                                    }

                                    sb.Append(s.name);
                                }

                                hasAssetEntries = true;
                            }
                        }

                        hasEntries = true;
                    }

                    suffix = sb.ToString();
                }

                var fileInfo = new FileInfo(Path.GetFullPath(file));
                var info = new ProjectHierarchyInfo(fileInfo.Name, GetParentInfo(fileInfo.Directory), suffix);

                hierarchyMap.Insert(info);

                if (ProjectHierarchyMapEntry.EstimatedSerializedLength >
                    AssistantMessageSizeConstraints.ContextLimit)
                {
                    break;
                }
            }

            if (hierarchyMap.Children.Count == 0)
                throw new Exception("The project does not contain any asset that matches the specified filters.");

            resultPrefix += ":\n";

            result.ContextType = "project structure";
            result.Payload = resultPrefix + hierarchyMap.Serialized();

            InternalLog.Log("ProjectStructureExtractor Payload:\n" + result.Payload);

            return result;

            bool IsDirectoryInside(DirectoryInfo dir1, DirectoryInfo dir2)
            {
                var relativePath = Path.GetRelativePath(dir2.FullName, dir1.FullName);
                return relativePath.StartsWith(".");
            }

            ProjectHierarchyInfo GetParentInfo(DirectoryInfo directoryInfo)
            {
                // Don't go above assetPath:
                if (directoryInfo == null || IsDirectoryInside(directoryInfo, assetPath))
                    return null;

                if (parentMap.TryGetValue(directoryInfo.FullName, out var parentInfo))
                    return parentInfo;

                parentInfo = new ProjectHierarchyInfo(directoryInfo.Name, GetParentInfo(directoryInfo.Parent));

                parentMap[directoryInfo.FullName] = parentInfo;

                return parentInfo;
            }

            bool NameAndTypeExist(List<string> nameFilters, List<string> typeFilters, string name, string type)
            {
                for (int i = 0; i < nameFilters.Count; i++)
                {
                    if (nameFilters[i] == name && typeFilters[i] == type)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static bool ContainsSubAsset(Object asset, string objectName, string assetType)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset));
            foreach (var subAsset in subAssets)
            {
                var subAssetType = subAsset.GetType().ToString().ToLower();
                var dotIdx = subAssetType.LastIndexOf('.');
                if (dotIdx >= 0)
                    subAssetType = subAssetType.Substring(dotIdx + 1);

                if (subAssetType == assetType)
                {
                    // Perfect match or empty/null name if we care only about sub assets of a given type
                    if (String.IsNullOrEmpty(objectName) || objectName == subAsset.name)
                        return true;
                }
            }

            return false;
        }
    }
}
