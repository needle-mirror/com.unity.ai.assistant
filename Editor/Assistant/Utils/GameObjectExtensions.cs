using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Utils
{
    internal static class GameObjectExtensions
    {
        static readonly IList<string> k_TempList = new List<string>();

        public static Texture2D GetTextureForObject(this Object obj)
        {
            if (obj is GameObject go)
            {
                return PrefabUtility.GetIconForGameObject(go);
            }

            if (AssetDatabase.IsSubAsset(obj))
            {
                return GetTextureForObjectType(obj);
            }

            var result = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(obj)) as Texture2D;
            if (result != null)
            {
                return result;
            }

            return EditorGUIUtility.GetIconForObject(obj);
        }

        public static Texture2D GetTextureForObjectType(this Object obj)
        {
            return EditorGUIUtility.ObjectContent(null, obj.GetType()).image as Texture2D;
        }

        public static string GetObjectHierarchy(this GameObject source)
        {
            k_TempList.Clear();
            GetObjectHierarchy(source, k_TempList);

            string path;
            if (k_TempList.Count > 1)
            {
                path = $"/{string.Join("/", k_TempList)}/{source.name}";
            }
            else if (k_TempList.Count == 1)
            {
                path = $"/{k_TempList[0]}/{source.name}";
            }
            else
            {
                path = $"/{source.name}";
            }

            // Add instance ID with unique linebreak separator (character that is not valid/used in object names)
            return $"{path}\n{source.GetInstanceID()}";
        }

        static void GetObjectHierarchy(this GameObject source, IList<string> segments)
        {
            if (source.transform.parent == null)
            {
                return;
            }

            var parent = source.transform.parent.gameObject;
            segments.Insert(0, parent.name);
            GetObjectHierarchy(parent, segments);
        }

        public static bool IsPrefabInstance(this GameObject obj)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
            return root != null;
        }

        public static bool IsPrefabVariant(this GameObject obj)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
            if (root == null)
            {
                return false;
            }

            return PrefabUtility.IsPartOfVariantPrefab(root);
        }

        public static bool IsPrefabType(this Object obj)
        {
            if (obj is GameObject go)
            {
                return go.IsPrefabType();
            }

            return false;
        }

        public static bool IsPrefabType(this GameObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            var isAsset = AssetDatabase.Contains(obj);
            if (!isAsset)
            {
                return obj.IsPrefabInScene();
            }

            return PrefabUtility.IsPartOfAnyPrefab(obj);
        }

        public static bool IsPrefabInScene(this Object obj)
        {
            if (obj is GameObject go)
            {
                return go.IsPrefabInScene();
            }

            return false;
        }

        public static bool IsPrefabInScene(this GameObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (AssetDatabase.Contains(obj))
            {
                return false;
            }

            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj) != null;
        }
    }
}
