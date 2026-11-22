using System;
using System.Collections.Generic;
using System.IO;
using DarkLegion.Core;
using DarkLegion.Data;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor
{
    public static class RestRuleImporter
    {
        private const string SheetName = "RestNode_Rules";
        private const string GeneratedRules = "Assets/_Project/Data/Generated/RestRules";
        private const string Heal = "REST_HEAL";
        private const string Upgrade = "REST_UPGRADE";

        [MenuItem("DarkLegion/Import Rest Node Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), GeneratedRules);
            if (errors.Count == 0) Debug.Log("휴식 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try
            {
                data = new GameData(DataSheet.FromJson(SheetName,
                    File.ReadAllText(Path.Combine(input, SheetName + ".json"))));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                error is FormatException)
            {
                return new[] { error.Message };
            }

            var rows = data.Rows(SheetName);
            var errors = new List<string>();
            var ids = new HashSet<string>();
            foreach (var row in rows)
            {
                string id = row.Str("OptionID");
                if (id != Heal && id != Upgrade)
                {
                    errors.Add($"{SheetName}: 알 수 없는 OptionID {id}");
                    continue;
                }
                if (!ids.Add(id)) errors.Add($"{SheetName}/{id}: 중복 OptionID");
                if (string.IsNullOrEmpty(row.Str("OptionType")))
                    errors.Add($"{SheetName}/{id}: OptionType이 비어 있습니다.");
                double percent = row.Num("HealPercent");
                if (!row.IsNumber("HealPercent") || !(percent >= 0 && percent <= 1) ||
                    (id == Upgrade && percent != 0))
                    errors.Add($"{SheetName}/{id}: HealPercent 범위 오류");

                string path = $"{output}/{id}.asset";
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && !(existing is RestRuleData))
                    errors.Add($"{path}: 다른 종류의 에셋이 있습니다.");
            }
            if (!ids.Contains(Heal)) errors.Add($"{SheetName}: {Heal}이 없습니다.");
            if (!ids.Contains(Upgrade)) errors.Add($"{SheetName}: {Upgrade}가 없습니다.");
            if (errors.Count != 0) return errors;

            var loaded = GameDataLoader.Load(data);
            if (!loaded.Ok) return loaded.Errors;
            DesignAssetFolder.EnsureExists(output);
            foreach (var row in rows)
            {
                string id = row.Str("OptionID");
                string path = $"{output}/{id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<RestRuleData>(path);
                bool isNew = asset == null;
                if (isNew) asset = ScriptableObject.CreateInstance<RestRuleData>();
                asset.Initialize(id, row.Str("OptionType"), id == Heal ? loaded.RestHealPercent : 0);
                if (isNew) AssetDatabase.CreateAsset(asset, path);
                else
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            return errors;
        }
    }
}
