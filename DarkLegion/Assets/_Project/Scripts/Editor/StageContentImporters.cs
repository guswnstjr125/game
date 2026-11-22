using System;
using System.Collections.Generic;
using System.IO;
using DarkLegion.Core;
using DarkLegion.Data;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor
{
    public static class MonsterImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/Monsters";

        [MenuItem("DarkLegion/Import Monster Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("몬스터 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Monsters_Master", "Monster_Effects", "Cards_Master"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Monsters_Master").Count == 0) errors.Add("Monsters_Master: 몬스터 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var monster in content.Monsters)
            {
                string path = output + "/" + monster.Id + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<MonsterData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "MonsterData", paths, "몬스터");
            foreach (var monster in content.Monsters)
            {
                string path = output + "/" + monster.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<MonsterData>(path, out bool created);
                asset.Initialize(monster);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class BossPatternImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/BossPatterns";

        [MenuItem("DarkLegion/Import Boss Pattern Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("보스 패턴 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try
            {
                data = DesignAssetWrite.Read(input, "Boss_List", "Boss_Phase", "Boss_TurnCycle", "Enemies_Master", "Relics_MVP", "Curse_Cards");
            }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Boss_TurnCycle").Count == 0) errors.Add("Boss_TurnCycle: 행동 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var boss in content.Bosses)
            {
                string path = output + "/" + boss.BossId + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<BossPatternData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "BossPatternData", paths, "보스 패턴");
            foreach (var boss in content.Bosses)
            {
                string path = output + "/" + boss.BossId + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<BossPatternData>(path, out bool created);
                asset.Initialize(boss);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class RelicImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/Relics";

        [MenuItem("DarkLegion/Import Relic Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("유물 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Relics_MVP"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Relics_MVP").Count == 0) errors.Add("Relics_MVP: 유물 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relic in content.Relics)
            {
                string path = output + "/" + relic.Id + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<RelicData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "RelicData", paths, "유물");
            foreach (var relic in content.Relics)
            {
                string path = output + "/" + relic.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<RelicData>(path, out bool created);
                asset.Initialize(relic);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class EventImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/Events";

        [MenuItem("DarkLegion/Import Event Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("이벤트 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Events_MVP", "Event_FloorPool"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Events_MVP").Count == 0) errors.Add("Events_MVP: 이벤트 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ev in content.Events)
            {
                string path = output + "/" + ev.Id + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<EventData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "EventData", paths, "이벤트");
            foreach (var ev in content.Events)
            {
                string path = output + "/" + ev.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<EventData>(path, out bool created);
                asset.Initialize(ev);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class CurseImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/Curses";

        [MenuItem("DarkLegion/Import Curse Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("저주 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Curse_Cards"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Curse_Cards").Count == 0) errors.Add("Curse_Cards: 저주 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var curse in content.Curses)
            {
                string path = output + "/" + curse.Id + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<CurseCardData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "CurseCardData", paths, "저주");
            foreach (var curse in content.Curses)
            {
                string path = output + "/" + curse.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<CurseCardData>(path, out bool created);
                asset.Initialize(curse);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class KeywordImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/Keywords";

        [MenuItem("DarkLegion/Import Keyword Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("키워드 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Keywords"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Keywords").Count == 0) errors.Add("Keywords: 키워드 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyword in content.Keywords)
            {
                string path = output + "/" + keyword.Id + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<KeywordData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "KeywordData", paths, "키워드");
            foreach (var keyword in content.Keywords)
            {
                string path = output + "/" + keyword.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<KeywordData>(path, out bool created);
                asset.Initialize(keyword);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class FusionImporter
    {
        private const string Recipes = "Assets/_Project/Data/Generated/FusionRecipes";
        private const string Pools = "Assets/_Project/Data/Generated/FusionPools";

        [MenuItem("DarkLegion/Import Fusion Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Recipes, Pools);
            if (errors.Count == 0) Debug.Log("융합 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string recipeOutput, string poolOutput)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Fusion_Recipes", "Fusion_RandomPool", "Monsters_Master"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Fusion_Recipes").Count == 0) errors.Add("Fusion_Recipes: 레시피 행이 없습니다.");
            var recipePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var poolPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var recipe in content.FusionRecipes)
            {
                string path = recipeOutput + "/" + recipe.Id + ".asset";
                recipePaths.Add(path);
                DesignAssetWrite.RejectForeign<FusionRecipeData>(path, errors);
            }
            foreach (var pool in content.FusionPools)
            {
                string path = poolOutput + "/" + pool.Id + ".asset";
                poolPaths.Add(path);
                DesignAssetWrite.RejectForeign<FusionPoolData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(recipeOutput);
            DesignAssetFolder.EnsureExists(poolOutput);
            DesignAssetWrite.WarnMissing(recipeOutput, "FusionRecipeData", recipePaths, "융합 레시피");
            DesignAssetWrite.WarnMissing(poolOutput, "FusionPoolData", poolPaths, "융합 풀");
            foreach (var recipe in content.FusionRecipes)
            {
                string path = recipeOutput + "/" + recipe.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<FusionRecipeData>(path, out bool created);
                asset.Initialize(recipe);
                DesignAssetWrite.Commit(asset, path, created);
            }
            foreach (var pool in content.FusionPools)
            {
                string path = poolOutput + "/" + pool.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<FusionPoolData>(path, out bool created);
                asset.Initialize(pool);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class RewardImporter
    {
        private const string Battles = "Assets/_Project/Data/Generated/Rewards";
        private const string Elites = "Assets/_Project/Data/Generated/EliteRelics";

        [MenuItem("DarkLegion/Import Reward Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Battles, Elites);
            if (errors.Count == 0) Debug.Log("보상 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string rewardOutput, string eliteOutput)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Reward_BattleTable", "Reward_EliteRelic", "Relics_MVP"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Reward_BattleTable").Count == 0) errors.Add("Reward_BattleTable: 보상 행이 없습니다.");
            var rewardPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var elitePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in content.Rewards)
            {
                string path = rewardOutput + "/" + row.BattleType + "_F" + row.Floor + ".asset";
                rewardPaths.Add(path);
                DesignAssetWrite.RejectForeign<RewardTableData>(path, errors);
            }
            foreach (var row in content.EliteRelics)
            {
                string path = eliteOutput + "/F" + row.Floor + ".asset";
                elitePaths.Add(path);
                DesignAssetWrite.RejectForeign<RewardEliteRelicData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(rewardOutput);
            DesignAssetFolder.EnsureExists(eliteOutput);
            DesignAssetWrite.WarnMissing(rewardOutput, "RewardTableData", rewardPaths, "보상");
            DesignAssetWrite.WarnMissing(eliteOutput, "RewardEliteRelicData", elitePaths, "엘리트 유물");
            foreach (var row in content.Rewards)
            {
                string path = rewardOutput + "/" + row.BattleType + "_F" + row.Floor + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<RewardTableData>(path, out bool created);
                asset.Initialize(row);
                DesignAssetWrite.Commit(asset, path, created);
            }
            foreach (var row in content.EliteRelics)
            {
                string path = eliteOutput + "/F" + row.Floor + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<RewardEliteRelicData>(path, out bool created);
                asset.Initialize(row);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class StatusImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/Statuses";

        [MenuItem("DarkLegion/Import Status Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("상태이상 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Status_Master"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Status_Master").Count == 0) errors.Add("Status_Master: 상태이상 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var status in content.Statuses)
            {
                string path = output + "/" + status.Id + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<StatusData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "StatusData", paths, "상태이상");
            foreach (var status in content.Statuses)
            {
                string path = output + "/" + status.Id + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<StatusData>(path, out bool created);
                asset.Initialize(status);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }

    public static class ClassStartImporter
    {
        private const string Generated = "Assets/_Project/Data/Generated/ClassStarts";

        [MenuItem("DarkLegion/Import Class Start Design Data")]
        private static void ImportFromMenu()
        {
            var errors = Import(Path.Combine(Application.dataPath, "..", "DesignData"), Generated);
            if (errors.Count == 0) Debug.Log("시작 능력치 설계 데이터 임포트 완료");
            else Debug.LogError(string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Import(string input, string output)
        {
            GameData data;
            try { data = DesignAssetWrite.Read(input, "Player_Stats"); }
            catch (Exception error) when (DesignAssetWrite.IsReadError(error)) { return new[] { error.Message }; }
            var content = ContentImport.Read(data);
            var errors = new List<string>(content.Errors);
            if (data.Rows("Player_Stats").Count == 0) errors.Add("Player_Stats: 시작 능력치 행이 없습니다.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var start in content.ClassStarts)
            {
                string path = output + "/" + start.ClassName + ".asset";
                paths.Add(path);
                DesignAssetWrite.RejectForeign<ClassStartData>(path, errors);
            }
            if (errors.Count != 0) return errors;
            DesignAssetFolder.EnsureExists(output);
            DesignAssetWrite.WarnMissing(output, "ClassStartData", paths, "시작 능력치");
            foreach (var start in content.ClassStarts)
            {
                string path = output + "/" + start.ClassName + ".asset";
                var asset = DesignAssetWrite.LoadOrCreate<ClassStartData>(path, out bool created);
                asset.Initialize(start);
                DesignAssetWrite.Commit(asset, path, created);
            }
            return errors;
        }
    }
}
