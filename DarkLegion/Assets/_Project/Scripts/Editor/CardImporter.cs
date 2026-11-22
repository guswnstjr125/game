using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkLegion.Core;
using DarkLegion.Data;
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
                error is FormatException)
            {
                return new[] { error.Message };
            }

            var errors = loaded.Errors;
            var rows = data.Rows("Cards_Master");
            if (rows.Count == 0) errors.Add("Cards_Master: 카드 행이 없습니다.");
            // 1단계 필수 핸들러 (`38` §5, Core HandlerRegistry). 카드 32장은 실패, 저주·유물·이벤트는 구현 전이라 경고 (#76)
            var handlers = HandlerRegistry.CheckStage1(data);
            foreach (string id in handlers.MissingCards)
                errors.Add($"Cards_Master/{id}: 1단계 필수 카드 또는 효과 핸들러가 없습니다.");
            if (handlers.MissingPending.Count > 0)
                Debug.LogWarning($"아직 구현하지 않은 1단계 핸들러 {handlers.MissingPending.Count}개: {string.Join(", ", handlers.MissingPending)}");
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
            DesignAssetFolder.EnsureExists(output);
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

        /// <summary>Core 읽기로 시트를 읽는다 (`01` D-74). 중복 키·잘못된 source·끝 뒤 문자는 FormatException.</summary>
        private static DataSheet ReadSheet(string folder, string name) =>
            DataSheet.FromJson(name, File.ReadAllText(Path.Combine(folder, name + ".json")));
    }
}
