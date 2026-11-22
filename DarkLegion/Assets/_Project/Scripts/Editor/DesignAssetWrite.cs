using System;
using System.Collections.Generic;
using System.IO;
using DarkLegion.Core;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor
{
    internal static class DesignAssetWrite
    {
        public static GameData Read(string folder, params string[] names)
        {
            var sheets = new DataSheet[names.Length];
            for (int i = 0; i < names.Length; i++)
                sheets[i] = DataSheet.FromJson(names[i], File.ReadAllText(Path.Combine(folder, names[i] + ".json")));
            return new GameData(sheets);
        }

        public static bool IsReadError(Exception error) =>
            error is IOException || error is UnauthorizedAccessException || error is FormatException || error is OverflowException;

        public static void RejectForeign<T>(string path, List<string> errors) where T : Object
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is T)) errors.Add(path + ": 다른 종류의 에셋이 있습니다.");
        }

        public static void WarnMissing(string folder, string typeName, HashSet<string> paths, string label)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeName, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path)) Debug.LogWarning("원본에서 사라진 " + label + " 에셋을 유지합니다: " + path);
            }
        }

        public static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created) asset = ScriptableObject.CreateInstance<T>();
            return asset;
        }

        public static void Commit(ScriptableObject asset, string path, bool created)
        {
            if (created) AssetDatabase.CreateAsset(asset, path);
            else
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }
}
