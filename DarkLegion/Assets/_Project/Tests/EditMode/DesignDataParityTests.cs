using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 실제 export JSON(`DarkLegion/DesignData`, `38` §1) → Core 로더 결과가 손으로 옮긴 픽스처(`StageOne*.cs`)와 같은지 (#76 1-2).
    /// JSON은 `python tools/export_game_data.py`로 만든 생성물이다. 여기서 다르면 Excel이 맞고 픽스처를 고친다.
    /// </summary>
    [Category("Stage1")]
    public class DesignDataParityTests
    {
        private static GameDataLoadResult _loaded;

        /// <summary>DesignData 폴더: dotnet은 bin 아래에서, Unity는 프로젝트 폴더(DarkLegion/)에서 실행되므로 위로 올라가며 찾는다.</summary>
        public static string FindDesignData()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), System.AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    foreach (var candidate in new[] { Path.Combine(dir.FullName, "DesignData"), Path.Combine(dir.FullName, "DarkLegion", "DesignData") })
                    {
                        if (File.Exists(Path.Combine(candidate, "Cards_Master.json"))) return candidate;
                    }
                }
            }
            return null;
        }

        private static GameDataLoadResult Loaded()
        {
            if (_loaded != null) return _loaded;
            string dir = FindDesignData();
            Assert.IsNotNull(dir, "DarkLegion/DesignData/*.json 없음 — python tools/export_game_data.py");
            var texts = Directory.GetFiles(dir, "*.json").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => File.ReadAllText(f));
            return _loaded = GameDataLoader.Load(GameData.FromJson(texts));
        }

        private static readonly string[] Stage1Cards =
        {
            "NEC_ACT_001", "NEC_ACT_002", "NEC_ACT_003", "NEC_ACT_004", "NEC_MON_001", "NEC_MON_002", "NEC_MON_003", "NEC_MON_004",
            "NEC_MON_005", "NEC_MON_006", "NEC_MON_007", "NEC_MON_008", "NEC_MON_009", "NEC_MON_010", "NEC_MON_013", "NEC_MON_014",
            "NEC_SKL_001", "NEC_SKL_002", "NEC_SKL_003", "NEC_SKL_004", "NEU_ACT_001", "NEU_ACT_002", "NEU_MON_001", "NEU_MON_002",
            "NEU_MON_003", "NEU_MON_004", "NEU_MON_005", "NEU_MON_006", "NEU_MON_007", "NEU_MON_008", "NEU_SKL_001", "NEU_SKL_002"
        };

        [Test]
        public void RealData_LoadsWithoutErrors_AllRows()
        {
            var r = Loaded();
            Assert.IsEmpty(r.Errors, string.Join("\n", r.Errors));
            Assert.AreEqual(98, r.Cards.Ids.Count(), "Cards_Master 98행 전부 (1단계에도 모든 행, `38` §3)");
        }

        [TestCaseSource(nameof(Stage1Cards))]
        public void Card_MatchesFixture(string id)
        {
            var want = StageOneCards.Create().Get(id);
            var got = Loaded().Cards.Get(id);
            Assert.IsNotNull(got, id);
            Assert.AreEqual(want.Type, got.Type, "Type");
            Assert.AreEqual(want.Cost, got.Cost, "Cost");
            Assert.AreEqual(want.UpCost, got.UpCost, "UpCost");
            Assert.AreEqual(want.Exhaust, got.Exhaust, "Exhaust");
            Assert.AreEqual(want.Ethereal, got.Ethereal, "Ethereal");
            CollectionAssert.AreEquivalent(want.Params, got.Params, "Params");
            CollectionAssert.AreEquivalent(want.UpParams, got.UpParams, "UpParams");
            if (want.Type != CardType.Monster) return;
            Assert.AreEqual((want.Level, want.Hp, want.Atk, want.Def), (got.Level, got.Hp, got.Atk, got.Def), "Level·HP·ATK·DEF");
            Assert.AreEqual((want.UpHp, want.UpAtk, want.UpDef), (got.UpHp, got.UpAtk, got.UpDef), "강화 스탯");
            Assert.AreEqual(want.Soulbound, got.Soulbound, "Soulbound");
            Assert.AreEqual(want.SacrificeCrystal, got.SacrificeCrystal, "희생 Crystal");
        }

        [Test]
        public void Fusion_MatchesFixture()
        {
            var want = StageOneFusion.Create();
            var got = Loaded().Fusion;
            foreach (var (recipe, a, b, result) in want.FixedRecipes)
            {
                Assert.IsTrue(got.TryGetFixed(a, b, out var gr, out var gres), recipe);
                Assert.AreEqual((recipe, result), (gr, gres));
            }
            foreach (var key in new[] { ("NEC", 1), ("NEC+NEU", 1), ("NEU", 1) })
            {
                CollectionAssert.AreEqual(want.Pool(key.Item1, key.Item2).ToArray(), got.Pool(key.Item1, key.Item2).ToArray(), key.ToString());
            }
            Assert.AreEqual(want.IsClassMimic("NEU_MON_002"), got.IsClassMimic("NEU_MON_002"));
            // 1단계 32장끼리의 레시피만 픽스처에 있다. 고정 레시피 중 재료가 32장 안에 둘 다 있는 것은 픽스처와 같아야 한다
            var stage1 = new HashSet<string>(Stage1Cards);
            var gotStage1 = got.FixedRecipes.Where(x => stage1.Contains(x.a) && stage1.Contains(x.b)).Select(x => x.recipe).OrderBy(x => x);
            CollectionAssert.AreEqual(want.FixedRecipes.Select(x => x.recipe).OrderBy(x => x).ToArray(), gotStage1.ToArray());
        }

        [Test]
        public void EnemyKits_MatchFixture()
        {
            var want = StageOneEnemies.Create();
            var got = Loaded().EnemyKits;
            foreach (var id in want.Ids.Where(x => !x.StartsWith("ENE_TEST")))
            {
                var w = want.Get(id);
                var g = got.Get(id);
                Assert.IsNotNull(g, id);
                Assert.AreEqual(w.Count, g.Count, id);
                for (int i = 0; i < w.Count; i++)
                {
                    string at = $"{id}[{i}]";
                    Assert.AreEqual((w[i].ActionId, w[i].Condition, w[i].Chance, w[i].ActionType, w[i].Value, w[i].Duration, w[i].Target, w[i].SpawnId, w[i].DrainRatio),
                        (g[i].ActionId, g[i].Condition, g[i].Chance, g[i].ActionType, g[i].Value, g[i].Duration, g[i].Target, g[i].SpawnId, g[i].DrainRatio), at);
                    if (w[i].ActionType != "Summon") Assert.AreEqual(w[i].Status, g[i].Status, at + " Status"); // 픽스처는 Summon 적 ID를 Status에 적었다
                }
            }
            Assert.AreEqual(want.SpawnStats("ENE_1F_GHO_001"), got.SpawnStats("ENE_1F_GHO_001"));
        }

        [Test]
        public void BossPatterns_MatchFixture()
        {
            var want = StageOneBosses.Create();
            var got = Loaded().BossPatterns;
            foreach (var id in want.Ids)
            {
                var w = want.Get(id);
                var g = got.Get(id);
                Assert.IsNotNull(g, id);
                Assert.AreEqual(w.Count, g.Count, id);
                for (int i = 0; i < w.Count; i++)
                {
                    Assert.AreEqual((w[i].Phase, w[i].TurnIndex, w[i].Action, w[i].Damage, w[i].StatusEffect, w[i].TargetRule, w[i].EnqueueTwice),
                        (g[i].Phase, g[i].TurnIndex, g[i].Action, g[i].Damage, g[i].StatusEffect, g[i].TargetRule, g[i].EnqueueTwice), $"{id}[{i}]");
                }
            }
        }
    }
}
