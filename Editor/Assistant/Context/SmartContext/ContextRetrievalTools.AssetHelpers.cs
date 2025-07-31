using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Context.SmartContext
{
    internal static partial class ContextRetrievalTools
    {
        private static readonly string[] k_TypesWithModelMainAssets = { "mesh", "animationclip", "animation", "avatar" };
        private static readonly string[] k_TypesWithTextureMainAssets = { "sprite" };

        private static Object TryFindAsset(string objectName, string assetType)
        {
            var res = TryFindAssets(objectName, assetType);

            if (res?.Count() == 0)
                return null;

            return res.First();
        }

        private static IEnumerable<Object> TryFindAssets(string objectName, string assetType)
        {
            List<(Object asset, bool isTypeMatch, long score)> allMatchingAssets = new();

            assetType = assetType.ToLower();

            // Types that are potentially sub assets inside models
            if (k_TypesWithModelMainAssets.Contains(assetType) || string.IsNullOrEmpty(assetType))
            {
                var matchingSubAsset = TryFindAssetsOrSubAssetsOfType(objectName, assetType, "model", false);
                if (matchingSubAsset != null)
                {
                    foreach (var asset in matchingSubAsset)
                    {
                        allMatchingAssets.Add((asset, IsTypeMatch(asset, assetType), GetNameScore(asset, objectName)));
                    }
                }
            }

            // Types that are potentially sub assets inside texture2D
            if (k_TypesWithTextureMainAssets.Contains(assetType) || string.IsNullOrEmpty(assetType))
            {
                var matchingAsset = TryFindAssetsOrSubAssetsOfType(objectName, assetType, "texture2d", false);
                if (matchingAsset != null)
                {
                    foreach (var asset in matchingAsset)
                    {
                        allMatchingAssets.Add((asset, IsTypeMatch(asset, assetType), GetNameScore(asset, objectName)));
                    }
                }
            }

            // Various main asset types (not sub assets inside other assets)
            var matchingMainAssets = ContextRetrievalHelpers.FindAssetsWithFuzzyMatch(objectName, assetType, false);
            if (matchingMainAssets != null)
            {
                foreach (var match in matchingMainAssets)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(match.Path);
                    if (asset != null && !allMatchingAssets.Any(x => x.asset == asset))
                        allMatchingAssets.Add((asset, IsTypeMatch(asset, assetType), match.Score));
                }
            }

            // Sort: type match first, then by score
            var sorted = allMatchingAssets
                .OrderByDescending(a => a.isTypeMatch)
                .ThenBy(a => -a.score)
                .Select(a => a.asset);

            return sorted;
        }

        // Check if asset matches the specified type
        private static bool IsTypeMatch(Object asset, string assetType)
        {
            if (string.IsNullOrEmpty(assetType))
                return true;

            return asset.GetType().Name.ToLower().Contains(assetType.ToLower());
        }

        // Get a score for name closeness
        private static long GetNameScore(Object asset, string objectName)
        {
            long outScore = 0;
            FuzzySearch.FuzzyMatch(objectName.ToLowerInvariant(), asset.name.ToLowerInvariant(), ref outScore);
            return outScore;
        }

        private static IEnumerable<Object> TryFindAssetsOrSubAssetsOfType(string objectName, string assetType,
            string mainAssetType, bool returnFirstMatch)
        {
            return FindAssetsOrSubAssetsOfType(objectName, assetType, mainAssetType, returnFirstMatch);
        }

        private static Object TryFindAssetOfType(string objectName, string assetType)
        {
            return ContextRetrievalHelpers.FindAsset(objectName, assetType);
        }

        private static IEnumerable<Object> FindAssetsOrSubAssetsOfType(string objectName, string assetType,
            string mainAssetType, bool returnFirstMatch)
        {
            var matchingAssets = ContextRetrievalHelpers.FindAssetsWithFuzzyMatch(objectName, assetType, returnFirstMatch);
            if (matchingAssets?.Count() > 0)
            {
                if (returnFirstMatch)
                    return new[] { AssetDatabase.LoadAssetAtPath<Object>(matchingAssets.First().Path) };
            }

            // may be a sub asset
            var matchingAssetObjects = FindSubAssetsOfType(objectName, assetType, mainAssetType, returnFirstMatch);

            if (returnFirstMatch)
            {
                if (matchingAssetObjects?.Count() > 0)
                    return new[] { matchingAssetObjects.First() };

                return matchingAssetObjects;
            }

            if (matchingAssets?.Count() > 0)
            {
                var allMatchingAssets = matchingAssets.Select(x => AssetDatabase.LoadAssetAtPath<Object>(x.Path)).Where(x => x != null);;
                matchingAssetObjects = allMatchingAssets.Concat(matchingAssetObjects);
            }

            return matchingAssetObjects;
        }

        private static IEnumerable<Object> FindSubAssetsOfType(string objectName, string assetType, string mainAssetType,
            bool returnFirstMatch)
        {
            List<Object> subAssetCandidatesWithMainAssetMatch = new List<Object>();

            var mainAsset  = ContextRetrievalHelpers.FindAsset(objectName, mainAssetType);
            if (mainAsset != null)
            {
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mainAsset));
                foreach (var subAsset in subAssets)
                {
                    var subAssetType = subAsset.GetType().ToString().ToLower();
                    var dotIdx = subAssetType.LastIndexOf('.');
                    if (dotIdx >= 0)
                        subAssetType = subAssetType.Substring(dotIdx + 1);

                    if (subAssetType == assetType)
                    {
                        // Perfect match
                        if (objectName == subAsset.name && returnFirstMatch)
                            return new[] { subAsset };

                        subAssetCandidatesWithMainAssetMatch.Add(subAsset);
                    }
                }
            }

            // Go wider, this may be a sub asset name, not an main asset name
            var assetsOfMainType =
                AssetDatabase.FindAssets($"t:{mainAssetType}")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.StartsWith("Assets"));

            var subAssetCandidates = new List<Object>();

            bool isTextureType = mainAssetType == "texture2d";

            foreach (var path in assetsOfMainType)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    var texImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (isTextureType && texImporter != null)
                    {
                        // Early-out: skip non-sprite textures
                        if (texImporter.textureType != TextureImporterType.Sprite)
                            continue;
                    }

                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

                    var subAssets = allAssets.Where(asset =>
                    {
                        // Match any sub asset type with empty 'assetType' filter
                        if (string.IsNullOrEmpty(assetType))
                            return true;

                        var subAssetType = asset.GetType().ToString().ToLower();
                        var dotIdx = subAssetType.LastIndexOf('.');
                        if (dotIdx >= 0)
                            subAssetType = subAssetType.Substring(dotIdx + 1);

                        return subAssetType == assetType;
                    }).ToList();

                    var perfectMatch = subAssets.FirstOrDefault(e => e.name == objectName);
                    if (perfectMatch && returnFirstMatch)
                    {
                        return new[] { perfectMatch };
                    }

                    var allSubAssetFuzzyMatches = ContextRetrievalHelpers.FuzzyObjectSearch(objectName, subAssets);

                    if (allSubAssetFuzzyMatches?.Count() > 0)
                    {
                        if (returnFirstMatch)
                        {
                            var fuzzyMatch = allSubAssetFuzzyMatches.FirstOrDefault();

                            if (!subAssetCandidates.Any(e => e.name == fuzzyMatch.name))
                                subAssetCandidates.Add(fuzzyMatch);
                        }
                        else
                        {
                            subAssetCandidates.AddRange(allSubAssetFuzzyMatches.Where(e => !subAssetCandidatesWithMainAssetMatch.Contains(e)));
                        }
                    }
                }
            }

            // TODO: Revisit the fallbacks here; instead of just matching once more we'd like the best "score" from all the candidates!?

            // Best name match inside all main assets, that may not have any good name matches themselves
            var fuzzyMatchCandidate = ContextRetrievalHelpers.FuzzyObjectSearch(objectName, subAssetCandidates).FirstOrDefault();
            if (fuzzyMatchCandidate != null && returnFirstMatch)
                return new[] { fuzzyMatchCandidate };

            // Best name match inside all main assets that had fuzzy matched names (the main assets, not the actual sub asset we search)
            fuzzyMatchCandidate = ContextRetrievalHelpers.FuzzyObjectSearch(objectName, subAssetCandidatesWithMainAssetMatch).FirstOrDefault();
            if (fuzzyMatchCandidate != null && returnFirstMatch)
                return new[] { fuzzyMatchCandidate };

            // At least return the first sub asset that was found (main asset name match, just the sub asset didn't match)
            if (subAssetCandidatesWithMainAssetMatch.Count > 0 && returnFirstMatch)
                return new[] { subAssetCandidatesWithMainAssetMatch[0] };

            if (returnFirstMatch)
                return null;

            // Collect all
            subAssetCandidates.AddRange(subAssetCandidatesWithMainAssetMatch);
            return subAssetCandidates;
        }
    }
}
