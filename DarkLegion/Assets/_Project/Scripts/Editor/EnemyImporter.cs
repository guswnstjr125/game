using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkLegion.Core;
using DarkLegion.Data;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor
{
    public static class EnemyImporter
    {
        private const string GeneratedEnemies = "Assets/_Project/Data/Generated/Enemies";

        [MenuItem("DarkLegion/Import Enemy Design Data")]
        private static void ImportFromMenu()
        {
            string input = Path.Combine(Application.dataPath, "..", "DesignData");
            var errors = Import(input, GeneratedEnemies);
            if (errors.Count == 0) Debug.Log("적 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            GameDataLoadResult loaded;
            try
            {
                data = new GameData(ReadSheet(input, "Enemies_Master"), ReadSheet(input, "Enemy_Actions"),
                    ReadSheet(input, "Status_Master"));
                loaded = GameDataLoader.Load(data);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                error is FormatException || error is OverflowException)
            {
                return new[] { error.Message };
            }

            var errors = loaded.Errors;
            var rows = data.Rows("Enemies_Master");
            if (rows.Count == 0) errors.Add("Enemies_Master: 적 행이 없습니다.");
            var statuses = new HashSet<string>(data.Rows("Status_Master").Select(row => row.Str("Name_KO")));
            var actionRows = data.Rows("Enemy_Actions");
            ValidateActions(actionRows, statuses, errors);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                string id = row.Str("ENE_ID");
                if (!Regex.IsMatch(id, @"\A[A-Z0-9_]+\z"))
                {
                    errors.Add($"Enemies_Master: 유효하지 않은 적 ID {id}");
                    continue;
                }
                if (!IsInteger(row, "HP", 1)) errors.Add($"Enemies_Master/{id}: HP 범위 오류");
                if (!IsInteger(row, "ATK", 0) || !IsInteger(row, "DEF", 0))
                    errors.Add($"Enemies_Master/{id}: ATK 또는 DEF 범위 오류");

                string path = $"{output}/{id}.asset";
                paths.Add(path);
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && !(existing is EnemyData))
                    errors.Add($"{path}: 다른 종류의 에셋이 있습니다.");
            }
            if (errors.Count != 0) return errors;

            var actionsByEnemy = actionRows.GroupBy(row => row.Str("ENE_ID"))
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.Int("Order")).ToArray());
            DesignAssetFolder.EnsureExists(output);
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { output }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path)) Debug.LogWarning($"원본에서 사라진 적 에셋을 유지합니다: {path}");
            }

            foreach (var row in rows)
            {
                string id = row.Str("ENE_ID");
                string path = $"{output}/{id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                bool isNew = asset == null;
                if (isNew) asset = ScriptableObject.CreateInstance<EnemyData>();
                actionsByEnemy.TryGetValue(id, out var orderedActions);
                asset.Initialize(row, loaded.EnemyKits, orderedActions ?? Array.Empty<DataRow>());
                if (isNew) AssetDatabase.CreateAsset(asset, path);
                else
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            return errors;
        }

        private static void ValidateActions(IReadOnlyList<DataRow> rows, HashSet<string> statuses, List<string> errors)
        {
            if (rows.Count == 0) errors.Add("Enemy_Actions: 적 행동 행이 없습니다.");
            var orders = new HashSet<(string id, int order)>();
            foreach (var row in rows)
            {
                string id = row.Str("ENE_ID");
                string label = $"Enemy_Actions/{id}/{row.Str("Action_ID")}";
                if (!IsInteger(row, "Order", 1) || !orders.Add((id, row.Int("Order"))))
                    errors.Add($"{label}: 잘못되었거나 중복된 Order");
                if (row.Has("Chance"))
                {
                    double chance = row.Num("Chance");
                    if (!row.IsNumber("Chance") || !(chance >= 0 && chance <= 1))
                        errors.Add($"{label}: Chance 범위 오류");
                }
                if (row.Has("Value") && row.Str("ActionType") != "Summon")
                {
                    double value = row.Num("Value");
                    if (!row.IsNumber("Value") || double.IsInfinity(value) || double.IsNaN(value))
                        errors.Add($"{label}: Value는 유한한 숫자여야 합니다.");
                }
                string status = row.Str("Status");
                string[] parts = string.IsNullOrEmpty(status) ? Array.Empty<string>() : status.Split('+');
                if (parts.Any(part => !statuses.Contains(part)))
                    errors.Add($"{label}: Status_Master에 없는 상태 {status}");
                if (row.Has("Duration") && !IsInteger(row, "Duration", 0) &&
                    !IsDurationText(row.Str("Duration")))
                {
                    string[] durations = row.Str("Duration").Split('/');
                    if (parts.Length < 2 || durations.Length != parts.Length ||
                        durations.Any(duration => !IsDurationText(duration)))
                        errors.Add($"{label}: Duration 형식 오류");
                }
            }
        }

        private static bool IsDurationText(string value) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);

        private static bool IsInteger(DataRow row, string name, int minimum)
        {
            double value = row.Num(name);
            return row.IsNumber(name) && value >= minimum && value <= int.MaxValue && value == Math.Truncate(value);
        }

        private static DataSheet ReadSheet(string input, string name) =>
            DataSheet.FromJson(name, File.ReadAllText(Path.Combine(input, name + ".json")));
    }
}
