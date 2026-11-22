using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkLegion.Core;
using DarkLegion.Data;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor
{
    public static class EncounterImporter
    {
        private const string GeneratedEncounters = "Assets/_Project/Data/Generated/Encounters";

        [MenuItem("DarkLegion/Import Encounter Design Data")]
        private static void ImportFromMenu()
        {
            string input = Path.Combine(Application.dataPath, "..", "DesignData");
            var errors = Import(input, GeneratedEncounters);
            if (errors.Count == 0) Debug.Log("전투 편성 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try
            {
                data = new GameData(ReadSheet(input, "Enemies_Master"), ReadSheet(input, "Encounters"));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is FormatException)
            {
                return new[] { error.Message };
            }

            var loaded = GameDataLoader.Load(data);
            var errors = loaded.Errors;
            if (data.Rows("Encounters").Count == 0) errors.Add("Encounters: 전투 편성 행이 없습니다.");
            foreach (var row in data.Rows("Enemies_Master"))
                if (!IsPositiveInteger(row, "HP", int.MaxValue))
                    errors.Add($"Enemies_Master/{row.Str("ENE_ID")}: HP는 양의 정수여야 합니다.");
            ValidateEncounterRows(data.Rows("Encounters"), errors);
            if (errors.Count != 0) return errors;

            foreach (var encounter in loaded.Encounters.All)
            {
                if (!encounter.Waves.ContainsKey(1))
                    errors.Add($"{encounter.Label}: 첫 번째 Wave 편성이 없습니다.");
                foreach (var wave in encounter.Waves)
                    if (wave.Value.Sum(enemy => enemy.count) > BattleField.EnemySlotCount)
                        errors.Add($"{encounter.Label}/W{wave.Key}: 전투 슬롯보다 적이 많습니다.");
            }
            if (errors.Count != 0) return errors;

            var groups = loaded.Encounters.All.SelectMany(encounter =>
                encounter.Waves.Keys.Select(wave => (encounter, wave, path: $"{output}/F{encounter.Floor}_N{encounter.Node}_{encounter.Variant}_W{wave}.asset")))
                .ToArray();
            foreach (var group in groups)
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(group.path);
                if (existing != null && !(existing is EncounterData))
                    errors.Add($"{group.path}: 다른 종류의 에셋이 있습니다.");
            }
            if (errors.Count != 0) return errors;

            DesignAssetFolder.EnsureExists(output);
            var paths = new HashSet<string>(groups.Select(group => group.path), StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:EncounterData", new[] { output }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path)) Debug.LogWarning($"원본에서 사라진 전투 편성 에셋을 유지합니다: {path}");
            }

            foreach (var group in groups)
            {
                var asset = AssetDatabase.LoadAssetAtPath<EncounterData>(group.path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<EncounterData>();
                    asset.Initialize(group.encounter, group.wave);
                    AssetDatabase.CreateAsset(asset, group.path);
                }
                else
                {
                    asset.Initialize(group.encounter, group.wave);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            return errors;
        }

        private static void ValidateEncounterRows(IReadOnlyList<DataRow> rows, List<string> errors)
        {
            var keys = new HashSet<(int floor, int node, string variant, int wave, string enemy)>();
            foreach (var row in rows)
            {
                string enemy = row.Str("Enemy_ID");
                if (!IsPositiveInteger(row, "Floor", 5) || !IsPositiveInteger(row, "Node", RunMap.NodesPerFloor)
                    || !IsPositiveInteger(row, "Wave", 2) || !IsPositiveInteger(row, "Count", BattleField.EnemySlotCount))
                {
                    errors.Add($"Encounters/{enemy}: Floor·Node·Wave·Count 값은 범위 안의 양의 정수여야 합니다.");
                    continue;
                }
                int floor = row.Int("Floor");
                int node = row.Int("Node");
                string variant = row.Str("Variant");
                int wave = row.Int("Wave");
                string label = $"Encounters/F{floor}-N{node}/{variant}/W{wave}/{enemy}";
                var kind = RunMap.KindOf(floor, node, false);
                if ((kind != NodeKind.Battle && kind != NodeKind.Elite && kind != NodeKind.Boss) ||
                    row.Str("NodeType") != kind.ToString())
                    errors.Add($"{label}: NodeType이 맵의 전투 노드 유형과 일치하지 않습니다.");
                else if (variant != "A" && (variant != "B" || kind != NodeKind.Elite))
                    errors.Add($"{label}: Variant 범위 오류");
                else if (!keys.Add((floor, node, variant, wave, enemy)))
                    errors.Add($"{label}: 중복 편성 행");
            }
        }

        private static bool IsPositiveInteger(DataRow row, string column, int maximum)
        {
            if (!row.IsNumber(column)) return false;
            double value = row.Num(column);
            return value > 0 && value <= maximum && value == Math.Truncate(value);
        }

        private static DataSheet ReadSheet(string input, string name) =>
            DataSheet.FromJson(name, File.ReadAllText(Path.Combine(input, name + ".json")));
    }
}
