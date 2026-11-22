using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkLegion.Core;
using DarkLegion.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor
{
    public static class CardImporter
    {
        private const string GeneratedCards = "Assets/_Project/Data/Generated/Cards";
        private static readonly string[] SheetNames =
        {
            "Cards_Master", "Cards_Effect", "Cards_Upgrade", "Cards_FloorUnlock", "Cards_Text",
            "Monsters_Master", "Monster_Effects"
        };
        // D-44의 20종 몬스터. 스킬·액션 12종은 D-71 Core 이름표에서 가져온다.
        private static readonly string[] StageOneMonsters =
        {
            "NEC_MON_001", "NEC_MON_002", "NEC_MON_003", "NEC_MON_004", "NEC_MON_005", "NEC_MON_006",
            "NEC_MON_007", "NEC_MON_008", "NEC_MON_009", "NEC_MON_010", "NEC_MON_013", "NEC_MON_014",
            "NEU_MON_001", "NEU_MON_002", "NEU_MON_003", "NEU_MON_004", "NEU_MON_005", "NEU_MON_006",
            "NEU_MON_007", "NEU_MON_008"
        };

        [MenuItem("DarkLegion/Import Card Design Data")]
        private static void ImportFromMenu()
        {
            string input = Path.Combine(Application.dataPath, "..", "DesignData");
            var errors = Import(input, GeneratedCards);
            if (errors.Count == 0) Debug.Log("카드 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            GameDataLoadResult loaded;
            try
            {
                data = new GameData(SheetNames.Select(name => ReadSheet(input, name)).ToArray());
                loaded = GameDataLoader.Load(data);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                error is FormatException || error is JsonException)
            {
                return new[] { error.Message };
            }

            var errors = loaded.Errors;
            var rows = data.Rows("Cards_Master");
            if (rows.Count == 0) errors.Add("Cards_Master: 카드 행이 없습니다.");
            foreach (string id in StageOneMonsters)
                if (loaded.Cards.Get(id)?.Type != CardType.Monster)
                    errors.Add($"Cards_Master/{id}: 1단계 필수 몬스터 카드가 없습니다.");
            foreach (string id in GameDataLoader.ValueNames.Keys)
            {
                var expectedType = id.Contains("_SKL_") ? CardType.Skill : CardType.Action;
                if (loaded.Cards.Get(id)?.Type != expectedType || CardEffects.For(id) == null)
                    errors.Add($"Cards_Master/{id}: 1단계 필수 카드 또는 효과 핸들러가 없습니다.");
            }
            foreach (var row in data.Rows("Cards_Upgrade"))
            {
                if (GameDataLoader.TryParseUpParams(row.Str("Up_Params"), out var values)
                    && values.TryGetValue("Cost", out double cost) && !IsValidCost(cost))
                    errors.Add($"Cards_Upgrade/{row.Str("Card_ID")}: 비용 범위 오류");
            }
            var unlocks = new Dictionary<string, DataRow>();
            foreach (var row in data.Rows("Cards_FloorUnlock"))
            {
                string id = row.Str("Card_ID");
                if (loaded.Cards.Get(id) == null)
                    errors.Add($"Cards_FloorUnlock/{id}: Cards_Master에 없는 카드입니다.");
                else if (!unlocks.TryAdd(id, row))
                    errors.Add($"Cards_FloorUnlock/{id}: 중복 카드입니다.");
            }
            var assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                string id = row.Str("ID");
                if (!Regex.IsMatch(id, @"\A[A-Z0-9_]+\z"))
                {
                    errors.Add($"Cards_Master: 유효하지 않은 카드 ID {id}");
                    continue;
                }

                var card = loaded.Cards.Get(id);
                if (!row.IsNumber("Crystal") || !IsValidCost(row.Num("Crystal")))
                    errors.Add($"Cards_Master/{id}: 비용 범위 오류");
                if (card?.Type == CardType.Monster && (card.Hp <= 0 || card.UpHp <= 0))
                    errors.Add($"Cards_Master/{id}: HP 범위 오류");

                string path = $"{output}/{id}.asset";
                assetPaths.Add(path);
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && !(existing is CardData))
                    errors.Add($"{path}: 다른 종류의 에셋이 있습니다.");
            }
            if (errors.Count != 0) return errors;

            var texts = data.Rows("Cards_Text").ToDictionary(row => row.Str("Card_ID"));
            EnsureAssetFolder(output);
            foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { output }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPaths.Contains(path)) Debug.LogWarning($"원본에서 사라진 카드 에셋을 유지합니다: {path}");
            }

            foreach (var row in rows)
            {
                string id = row.Str("ID");
                string path = $"{output}/{id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<CardData>(path);
                bool isNew = asset == null;
                if (isNew) asset = ScriptableObject.CreateInstance<CardData>();
                unlocks.TryGetValue(id, out var unlock);
                texts.TryGetValue(id, out var cardText);
                asset.Initialize(loaded.Cards.Get(id), row, unlock, cardText);
                if (isNew) AssetDatabase.CreateAsset(asset, path);
                else
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            return errors;
        }

        private static bool IsValidCost(double cost) => cost >= 0 && cost <= int.MaxValue && cost == Math.Truncate(cost);

        private static DataSheet ReadSheet(string folder, string name)
        {
            using var file = File.OpenText(Path.Combine(folder, name + ".json"));
            using var reader = new JsonTextReader(file) { DateParseHandling = DateParseHandling.None };
            var sheet = JObject.Load(reader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (!(sheet["rows"] is JArray rows))
                throw new FormatException($"{name}: rows 배열이 없습니다.");
            var source = sheet["source"];
            if (source != null && source.Type != JTokenType.String && source.Type != JTokenType.Null)
                throw new FormatException($"{name}: source는 문자열이어야 합니다.");
            if (reader.Read())
                throw new FormatException($"{name}: 시트 객체 뒤에 추가 데이터가 있습니다.");
            return new DataSheet(name, (string)source, rows.Select(row =>
            {
                if (!(row is JObject columns))
                    throw new FormatException($"{name}: 행은 JSON 객체여야 합니다.");
                return new DataRow(columns.Properties().ToDictionary(column => column.Name,
                    column => ReadCell(name, column)));
            }).ToArray());
        }

        private static object ReadCell(string sheet, JProperty cell)
        {
            switch (cell.Value.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float: return (double)cell.Value;
                case JTokenType.Boolean: return (bool)cell.Value;
                case JTokenType.String: return (string)cell.Value;
                case JTokenType.Null: return null;
                default: throw new FormatException($"{sheet}/{cell.Name}: 지원하지 않는 셀 형식");
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
