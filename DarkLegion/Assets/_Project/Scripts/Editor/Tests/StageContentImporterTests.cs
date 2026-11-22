using System;
using System.IO;
using System.Linq;
using DarkLegion.Core;
using DarkLegion.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkLegion.Editor.Tests
{
    /// <summary>
    /// 에셋 작성과 GUID 보존 (`32` DATA-02). 숫자는 ContentImportTests가 Excel과 대조한 값과 같다.
    /// </summary>
    [Category("Stage1")]
    public sealed class StageContentImporterTests
    {
        [Test]
        public void DATA_02_ImportsStageContentAndPreservesGuid()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string input = Path.Combine(Application.dataPath, "..", "DesignData");
            string monsters = "Assets/MonsterImport_" + suffix;
            string bosses = "Assets/BossImport_" + suffix;
            string relics = "Assets/RelicImport_" + suffix;
            string events = "Assets/EventImport_" + suffix;
            string curses = "Assets/CurseImport_" + suffix;
            string keywords = "Assets/KeywordImport_" + suffix;
            string recipes = "Assets/FusionRecipeImport_" + suffix;
            string pools = "Assets/FusionPoolImport_" + suffix;
            string rewards = "Assets/RewardImport_" + suffix;
            string elites = "Assets/EliteRelicImport_" + suffix;
            string statuses = "Assets/StatusImport_" + suffix;
            string classes = "Assets/ClassStartImport_" + suffix;
            try
            {
                Assert.That(MonsterImporter.Import(input, monsters), Is.Empty);
                Assert.That(AssetDatabase.FindAssets("t:MonsterData", new[] { monsters }).Length, Is.EqualTo(97));
                var bone = AssetDatabase.LoadAssetAtPath<MonsterData>(monsters + "/MON_NEC_L1_001.asset");
                Assert.That(bone.Hp, Is.EqualTo(10));
                Assert.That(bone.Atk, Is.EqualTo(3));
                Assert.That(bone.Def, Is.EqualTo(1));
                Assert.That(bone.Crystal, Is.EqualTo(1));
                Assert.That(bone.Level, Is.EqualTo(1));
                var ghoul = AssetDatabase.LoadAssetAtPath<MonsterData>(monsters + "/MON_NEC_L1_002.asset");
                Assert.That(ghoul.EffectAt(0).EffectId, Is.EqualTo("Heal"));
                Assert.That(ghoul.EffectAt(0).Value, Is.EqualTo(3));
                Assert.That(ghoul.EffectAt(0).Target, Is.EqualTo("RandomAlly"));
                string guid = AssetDatabase.AssetPathToGUID(monsters + "/MON_NEC_L1_001.asset");
                Assert.That(MonsterImporter.Import(input, monsters), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(monsters + "/MON_NEC_L1_001.asset"), Is.EqualTo(guid));

                Assert.That(BossPatternImporter.Import(input, bosses), Is.Empty);
                var boss = AssetDatabase.LoadAssetAtPath<BossPatternData>(bosses + "/BOS_1F_001.asset");
                Assert.That(boss.Hp, Is.EqualTo(47));
                Assert.That(boss.Atk, Is.EqualTo(8));
                Assert.That(boss.StepAt(0).Damage, Is.EqualTo(8));
                Assert.That(boss.StepAt(0).EnqueueTwice, Is.False);
                Assert.That(boss.PhaseAt(0).HpPctMin, Is.EqualTo(50));
                Assert.That(boss.PhaseAt(0).HpPctMax, Is.EqualTo(100));
                Assert.That(boss.GoldRewardMin, Is.EqualTo(50));
                Assert.That(boss.GoldRewardMax, Is.EqualTo(75));
                var floor2 = AssetDatabase.LoadAssetAtPath<BossPatternData>(bosses + "/BOS_2F_001.asset");
                Assert.That(floor2.StepAt(0).Action, Is.EqualTo("ApplyBurn"));
                Assert.That(floor2.StepAt(0).StatusEffect, Is.EqualTo("Burn+2"));
                var floor3 = AssetDatabase.LoadAssetAtPath<BossPatternData>(bosses + "/BOS_3F_001.asset");
                var patterns = new BossPatternCatalog();
                floor3.AddTo(patterns);
                Assert.That(patterns.Get(floor3.EnemyId).Any(step => step.OnceOnPhaseEnter && step.Action == "Summon"), Is.True);
                var floor4 = AssetDatabase.LoadAssetAtPath<BossPatternData>(bosses + "/BOS_4F_001.asset");
                bool threshold = false;
                for (int i = 0; i < floor4.StepCount; i++) threshold |= floor4.StepAt(i).ThresholdPct == 30;
                Assert.That(threshold, Is.True);
                var floor5 = AssetDatabase.LoadAssetAtPath<BossPatternData>(bosses + "/BOS_5F_001.asset");
                bool curseStep = false;
                for (int i = 0; i < floor5.StepCount; i++) curseStep |= floor5.StepAt(i).CurseId == "CUR_001";
                Assert.That(curseStep, Is.True);

                Assert.That(RelicImporter.Import(input, relics), Is.Empty);
                Assert.That(AssetDatabase.FindAssets("t:RelicData", new[] { relics }).Length, Is.EqualTo(20));
                var chalice = AssetDatabase.LoadAssetAtPath<RelicData>(relics + "/REL_BLOOD_CHALICE.asset");
                Assert.That(chalice.DisplayName, Is.EqualTo("피의 잔"));
                Assert.That(chalice.Trigger, Is.EqualTo("OnSacrifice"));
                string relicGuid = AssetDatabase.AssetPathToGUID(relics + "/REL_BLOOD_CHALICE.asset");
                Assert.That(RelicImporter.Import(input, relics), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(relics + "/REL_BLOOD_CHALICE.asset"), Is.EqualTo(relicGuid));

                Assert.That(EventImporter.Import(input, events), Is.Empty);
                var pact = AssetDatabase.LoadAssetAtPath<EventData>(events + "/EVT_DEVIL_PACT.asset");
                Assert.That(pact.Floor1, Is.EqualTo(0.14));
                Assert.That(pact.OptionAt(0).Condition, Is.EqualTo("최대 HP > 20"));
                Assert.That(AssetDatabase.LoadAssetAtPath<EventData>(events + "/EVT_FORGOTTEN_SHRINE.asset").Floor1, Is.EqualTo(0.22));

                Assert.That(CurseImporter.Import(input, curses), Is.Empty);
                var brand = AssetDatabase.LoadAssetAtPath<CurseCardData>(curses + "/CUR_002.asset");
                Assert.That(brand.Cost, Is.EqualTo(0));
                Assert.That(brand.Value1, Is.EqualTo(2));
                Assert.That(brand.RemoveCost, Is.EqualTo("80G"));
                var price = AssetDatabase.LoadAssetAtPath<CurseCardData>(curses + "/CUR_004.asset");
                Assert.That(price.Removable, Is.False);
                Assert.That(price.RemoveCost, Is.EqualTo(""));
                Assert.That(price.Value1, Is.EqualTo(3));

                Assert.That(KeywordImporter.Import(input, keywords), Is.Empty);
                Assert.That(AssetDatabase.FindAssets("t:KeywordData", new[] { keywords }).Length, Is.EqualTo(18));
                Assert.That(AssetDatabase.LoadAssetAtPath<KeywordData>(keywords + "/KW_EXHAUST.asset").DisplayName, Is.EqualTo("소멸"));

                Assert.That(FusionImporter.Import(input, recipes, pools), Is.Empty);
                var recipe = AssetDatabase.LoadAssetAtPath<FusionRecipeData>(recipes + "/FUS_001.asset");
                Assert.That(recipe.Weight, Is.EqualTo(100));
                Assert.That(recipe.IsFixedRecipe, Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<FusionRecipeData>(recipes + "/FUS_002.asset").IsFixedRecipe, Is.False);
                var pool = AssetDatabase.LoadAssetAtPath<FusionPoolData>(pools + "/POOL_NEC_L1_001.asset");
                Assert.That(pool.Weight, Is.EqualTo(40));
                Assert.That(pool.InputLevel, Is.EqualTo(1));
                Assert.That(pool.OutputLevel, Is.EqualTo(2));

                Assert.That(RewardImporter.Import(input, rewards, elites), Is.Empty);
                var normal = AssetDatabase.LoadAssetAtPath<RewardTableData>(rewards + "/Normal_F1.asset");
                var tables = new RewardTables();
                normal.AddTo(tables);
                Assert.That(tables.Row(NodeKind.Battle, 1).GoldMin, Is.EqualTo(5));
                Assert.That(normal.GoldMin, Is.EqualTo(5));
                Assert.That(normal.GoldMax, Is.EqualTo(10));
                Assert.That(normal.Common, Is.EqualTo(0.6));
                Assert.That(normal.Uncommon, Is.EqualTo(0.28));
                Assert.That(normal.Rare, Is.EqualTo(0.1));
                Assert.That(normal.Legendary, Is.EqualTo(0.02));
                var bossPay = AssetDatabase.LoadAssetAtPath<RewardTableData>(rewards + "/Boss_F2.asset");
                Assert.That(bossPay.GoldMin, Is.EqualTo(50));
                Assert.That(bossPay.GoldMax, Is.EqualTo(80));
                Assert.That(AssetDatabase.LoadAssetAtPath<RewardEliteRelicData>(elites + "/F1.asset").PoolA, Is.EqualTo("REL_COM_001"));

                Assert.That(StatusImporter.Import(input, statuses), Is.Empty);
                var bind = AssetDatabase.LoadAssetAtPath<StatusData>(statuses + "/STATUS_BIND.asset");
                Assert.That(bind.DurationUnspecified, Is.True);
                Assert.That(bind.HasDuration, Is.False);
                Assert.That(AssetDatabase.LoadAssetAtPath<StatusData>(statuses + "/STATUS_POISON.asset").MaxStack, Is.EqualTo(20));
                Assert.That(AssetDatabase.LoadAssetAtPath<StatusData>(statuses + "/STATUS_WEAK.asset").DurationTurns, Is.EqualTo(2));

                Assert.That(ClassStartImporter.Import(input, classes), Is.Empty);
                var nec = AssetDatabase.LoadAssetAtPath<ClassStartData>(classes + "/NEC.asset");
                Assert.That(nec.StartHp, Is.EqualTo(80));
                Assert.That(nec.MaxHp, Is.EqualTo(80));
                Assert.That(nec.StartGold, Is.EqualTo(0));
                Assert.That(nec.StartCrystal, Is.EqualTo(3));
                Assert.That(AssetDatabase.LoadAssetAtPath<ClassStartData>(classes + "/HOL.asset").StartHp, Is.EqualTo(85));
                Assert.That(AssetDatabase.LoadAssetAtPath<ClassStartData>(classes + "/NEU.asset").StartGold, Is.EqualTo(40));
                string classGuid = AssetDatabase.AssetPathToGUID(classes + "/NEC.asset");
                Assert.That(ClassStartImporter.Import(input, classes), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(classes + "/NEC.asset"), Is.EqualTo(classGuid));
            }
            finally
            {
                foreach (string folder in new[] { monsters, bosses, relics, events, curses, keywords, recipes, pools, rewards, elites, statuses, classes })
                    if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void DATA_02_InvalidMonsterHpLeavesTheAssetUnchanged()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string source = Path.Combine(Application.dataPath, "..", "DesignData");
            string output = "Assets/MonsterInvalid_" + suffix;
            string scratch = Path.Combine(Path.GetTempPath(), "DarkLegionMonsterImport_" + suffix);
            Directory.CreateDirectory(scratch);
            try
            {
                Assert.That(MonsterImporter.Import(source, output), Is.Empty);
                string assetPath = output + "/MON_NEC_L1_001.asset";
                string before = File.ReadAllText(assetPath);
                foreach (string name in new[] { "Monsters_Master.json", "Monster_Effects.json", "Cards_Master.json" })
                    File.Copy(Path.Combine(source, name), Path.Combine(scratch, name));
                string jsonPath = Path.Combine(scratch, "Monsters_Master.json");
                string json = File.ReadAllText(jsonPath);
                const string search = "\"HP\": 10";
                int at = json.IndexOf(search, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0));
                File.WriteAllText(jsonPath, json.Remove(at, search.Length).Insert(at, "\"HP\": 0"));
                Assert.That(MonsterImporter.Import(scratch, output), Has.Some.Contains("HP"));
                Assert.That(File.ReadAllText(assetPath), Is.EqualTo(before));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
                Directory.Delete(scratch, true);
            }
        }

        [Test]
        public void DATA_02_InvalidRewardSumLeavesTheAssetUnchanged()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string source = Path.Combine(Application.dataPath, "..", "DesignData");
            string rewards = "Assets/RewardInvalid_" + suffix;
            string elites = "Assets/EliteInvalid_" + suffix;
            string scratch = Path.Combine(Path.GetTempPath(), "DarkLegionRewardImport_" + suffix);
            Directory.CreateDirectory(scratch);
            try
            {
                Assert.That(RewardImporter.Import(source, rewards, elites), Is.Empty);
                string assetPath = rewards + "/Normal_F1.asset";
                string before = File.ReadAllText(assetPath);
                foreach (string name in new[] { "Reward_BattleTable.json", "Reward_EliteRelic.json", "Relics_MVP.json" })
                    File.Copy(Path.Combine(source, name), Path.Combine(scratch, name));
                string jsonPath = Path.Combine(scratch, "Reward_BattleTable.json");
                string json = File.ReadAllText(jsonPath);
                const string search = "\"Drop_Common\": 0.6";
                int at = json.IndexOf(search, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0));
                File.WriteAllText(jsonPath, json.Remove(at, search.Length).Insert(at, "\"Drop_Common\": 0.5"));
                Assert.That(RewardImporter.Import(scratch, rewards, elites), Has.Some.Contains("희귀도"));
                Assert.That(File.ReadAllText(assetPath), Is.EqualTo(before));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(rewards)) AssetDatabase.DeleteAsset(rewards);
                if (AssetDatabase.IsValidFolder(elites)) AssetDatabase.DeleteAsset(elites);
                Directory.Delete(scratch, true);
            }
        }
    }
}
