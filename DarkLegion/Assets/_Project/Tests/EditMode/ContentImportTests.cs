using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 1단계에 남은 시트 변환 (`38` §3·§5). 기대값은 `04`·`06`·`08`·`09`·`12`·`14` Excel을 손으로 읽은 숫자 (`32` §11.2).
    /// Lv·Y/N·백분율은 내보내기 규칙(`38` §2)을 거친 JSON 값이다.
    /// </summary>
    [Category("Stage1")]
    public class ContentImportTests
    {
        private static DataRow R(params (string col, object val)[] kv)
        {
            var d = new Dictionary<string, object>();
            foreach (var (col, val) in kv) d[col] = val is int i ? (double)i : val;
            return new DataRow(d);
        }

        private static DataSheet S(string name, params DataRow[] rows) => new DataSheet(name, name, rows.ToList());

        private static ContentImportResult Real()
        {
            string dir = DesignDataParityTests.FindDesignData();
            Assert.IsNotNull(dir, "DarkLegion/DesignData");
            var texts = Directory.GetFiles(dir, "*.json").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => File.ReadAllText(f));
            return ContentImport.Read(GameData.FromJson(texts));
        }

        [Test]
        public void RealSheets_MatchHandCheckedExcelValues()
        {
            var got = Real();
            Assert.IsEmpty(got.Errors, string.Join("\n", got.Errors));
            Assert.AreEqual(97, got.Monsters.Count, "09 Monsters_Master");
            var bone = got.Monsters.First(m => m.Id == "MON_NEC_L1_001");
            Assert.AreEqual(("뼈 병사", "NEC", 1, "NEC_MON_001", 10, 3, 1, 1),
                (bone.Name, bone.Class, bone.Level, bone.CardId, bone.Hp, bone.Atk, bone.Def, bone.Crystal));
            var ghoul = got.Monsters.First(m => m.CardId == "NEC_MON_002");
            Assert.AreEqual(("OnDeath", "Heal", "RandomAlly", 3.0),
                (ghoul.Effects[0].Slot, ghoul.Effects[0].Effect, ghoul.Effects[0].Target, ghoul.Effects[0].Value));
            var shard = got.Monsters.First(m => m.CardId == "NEU_MON_001");
            Assert.AreEqual(("SacrificeCrystal", 1.0), (shard.Effects[0].Effect, shard.Effects[0].Value));
            var wraith = got.Monsters.First(m => m.CardId == "NEC_MON_005");
            Assert.AreEqual("Battle", wraith.Effects[0].DurationText);
            Assert.IsFalse(wraith.Effects[0].HasDuration);
            var beast = got.Monsters.First(m => m.CardId == "BEA_MON_006");
            Assert.AreEqual("2/1", beast.Effects.First(e => e.Effect == "ApplyStatus:Poison+Vulnerable").ValueText);

            Assert.AreEqual(5, got.Bosses.Count, "14 Boss_List");
            Assert.AreEqual(39, got.Bosses.Sum(b => b.Steps.Count), "14 Boss_TurnCycle");
            var floor1 = got.Bosses.First(b => b.BossId == "BOS_1F_001");
            Assert.AreEqual(("ENE_1F_BOS_001", 1, 47, 8, 1, 3, "REL_BOSS_1F"),
                (floor1.EnemyId, floor1.Floor, floor1.Hp, floor1.Atk, floor1.Def, floor1.PhaseCount, floor1.BossRelicId));
            Assert.AreEqual((50, 100), (floor1.Phases[0].HpPctMin, floor1.Phases[0].HpPctMax));
            Assert.AreEqual((0, 20), (floor1.Phases[2].HpPctMin, floor1.Phases[2].HpPctMax));
            Assert.AreEqual(("SingleAttack", 8, "Front", false, false, 0, ""),
                (floor1.Steps[0].Action, floor1.Steps[0].Damage, floor1.Steps[0].TargetRule, floor1.Steps[0].EnqueueTwice, floor1.Steps[0].OnceOnPhaseEnter, floor1.Steps[0].ThresholdPct, floor1.Steps[0].CurseId));
            var doubled = floor1.Steps.First(s => s.Phase == 3);
            Assert.AreEqual((8, true), (doubled.Damage, doubled.EnqueueTwice));
            Assert.IsTrue(floor1.HasGoldReward);
            Assert.AreEqual((50, 75), (floor1.GoldRewardMin, floor1.GoldRewardMax), "Boss_List 참고 골드. 지급은 Reward_BattleTable");

            var floor2 = got.Bosses.First(b => b.BossId == "BOS_2F_001");
            Assert.AreEqual(("ApplyBurn", 0, "Burn+2", "All", false),
                (floor2.Steps[0].Action, floor2.Steps[0].Damage, floor2.Steps[0].StatusEffect, floor2.Steps[0].TargetRule, floor2.Steps[0].EnqueueTwice));
            var twice = floor2.Steps.First(s => s.EnqueueTwice);
            Assert.AreEqual((2, 1, 9), (twice.Phase, twice.TurnIndex, twice.Damage));
            var summonOnce = got.Bosses.First(b => b.BossId == "BOS_3F_001").Steps.First(s => s.OnceOnPhaseEnter);
            Assert.AreEqual((2, 0, "Summon", "FLS_003 ×1"), (summonOnce.Phase, summonOnce.TurnIndex, summonOnce.Action, summonOnce.Note));
            var execute = got.Bosses.First(b => b.BossId == "BOS_4F_001").Steps.First(s => s.Action == "Execute");
            Assert.AreEqual(30, execute.ThresholdPct);
            Assert.AreEqual(25, got.Bosses.First(b => b.BossId == "BOS_4F_001").Steps.First(s => s.Action == "ExecuteAura").ThresholdPct);
            var curse = got.Bosses.First(b => b.BossId == "BOS_5F_001").Steps.First(s => s.Action == "AddCurseCard");
            Assert.AreEqual("CUR_001", curse.CurseId);

            Assert.AreEqual(20, got.Relics.Count, "06 Relics_MVP 전체 행");
            var chalice = got.Relics.First(r => r.Id == "REL_BLOOD_CHALICE");
            Assert.AreEqual(("피의 잔", "Uncommon", "OnSacrifice"), (chalice.Name, chalice.Rarity, chalice.Trigger));
            CollectionAssert.AreEquivalent(Stage1Scope.Relics.ToArray(), got.Relics.Select(r => r.Id).Where(id => Stage1Scope.Relics.Contains(id)).ToArray());
            Assert.AreEqual("재의 심장", got.Relics.First(r => r.Id == "REL_BOSS_2F").Name);

            Assert.AreEqual(8, got.Events.Count);
            Assert.AreEqual(24, got.Events.Sum(e => e.Options.Count), "06 Events_MVP");
            var pact = got.Events.First(e => e.Id == "EVT_DEVIL_PACT");
            Assert.AreEqual("악마 계약", pact.Name);
            Assert.AreEqual((1, "최대 HP > 20", "최대 HP -10 (현재 HP는 새 상한까지) → 카드 3택1"),
                (pact.Options[0].Option, pact.Options[0].Condition, pact.Options[0].Result));
            Assert.AreEqual("", pact.Options[2].Condition, "Excel —");
            Assert.AreEqual(0.14, pact.Floor1, "14%");
            Assert.AreEqual(0.22, got.Events.First(e => e.Id == "EVT_FORGOTTEN_SHRINE").Floor1, "22%");
            Assert.AreEqual(0.22, got.Events.First(e => e.Id == "EVT_WANDER_MERCHANT").Floor1);
            for (int floor = 1; floor <= 5; floor++)
                Assert.AreEqual(1.0, got.Events.Sum(e => e.Weight(floor)), 1e-6, "Floor_" + floor);

            var brand = got.Curses.First(c => c.Id == "CUR_002");
            Assert.AreEqual((0, true, "80G", 2.0, "Ethereal", false), (brand.Cost, brand.Removable, brand.RemoveCost, brand.Value1, brand.Keyword, brand.HasDuration));
            var price = got.Curses.First(c => c.Id == "CUR_004");
            Assert.AreEqual((false, "", 3.0), (price.Removable, price.RemoveCost, price.Value1));
            var will = got.Curses.First(c => c.Id == "CUR_003");
            Assert.AreEqual((2, true, 2), (will.Cost, will.HasDuration, will.Duration));

            Assert.AreEqual(18, got.Keywords.Count);
            var exhaust = got.Keywords.First(k => k.Id == "KW_EXHAUST");
            Assert.AreEqual(("Exhaust", "소멸", "사용하면 소멸 더미로 간다. 이번 전투에서 다시 나오지 않는다."), (exhaust.Term, exhaust.Name, exhaust.Tooltip));

            Assert.AreEqual(14, got.FusionRecipes.Count);
            var fus = got.FusionRecipes.First(r => r.Id == "FUS_001");
            Assert.AreEqual(("MON_NEC_L1_002", "MON_NEC_L1_002", "MON_NEC_L2_005", "Fixed", 100, true),
                (fus.MaterialA, fus.MaterialB, fus.ResultMonsterId, fus.Type, fus.Weight, fus.IsFixedRecipe));
            Assert.IsFalse(got.FusionRecipes.First(r => r.Id == "FUS_002").IsFixedRecipe, "Weight 0");
            Assert.IsFalse(got.FusionRecipes.First(r => r.Id == "FUS_006").IsFixedRecipe, "Random");
            var pool = got.FusionPools.First(p => p.Id == "POOL_NEC_L1_001");
            Assert.AreEqual(("NEC", 1, "NEC", 2, "MON_NEC_L2_005", 40),
                (pool.InputClass, pool.InputLevel, pool.OutputClass, pool.OutputLevel, pool.ResultMonsterId, pool.Weight));
            Assert.AreEqual(55, got.FusionPools.Count);

            var normal = got.Rewards.First(r => r.BattleType == "Normal" && r.Floor == 1);
            Assert.AreEqual((5, 10, 0.6, 0.28, 0.1, 0.02), (normal.GoldMin, normal.GoldMax, normal.Common, normal.Uncommon, normal.Rare, normal.Legendary));
            var bossPay = got.Rewards.First(r => r.BattleType == "Boss" && r.Floor == 2);
            Assert.AreEqual((50, 80), (bossPay.GoldMin, bossPay.GoldMax), "지급 골드. Boss_List 65~90이 아니다");
            Assert.AreEqual(1.0, normal.Common + normal.Uncommon + normal.Rare + normal.Legendary, 1e-9);
            var elite = got.EliteRelics.First(r => r.Floor == 1);
            Assert.AreEqual(("REL_COM_001", "REL_COM_002", "REL_COM_003", "Rare×2"), (elite.PoolA, elite.PoolB, elite.PoolC, elite.RarityRule));

            Assert.AreEqual(8, got.Statuses.Count);
            var poison = got.Statuses.First(s => s.Id == "STATUS_POISON");
            Assert.AreEqual(("Poison", "Stack", "TurnEnd", true, 20, false, false),
                (poison.Name, poison.StackType, poison.TickTiming, poison.HasMaxStack, poison.MaxStack, poison.HasDuration, poison.DurationUnspecified));
            var weak = got.Statuses.First(s => s.Id == "STATUS_WEAK");
            Assert.AreEqual((false, true, 2, false), (weak.HasMaxStack, weak.HasDuration, weak.DurationTurns, weak.DurationUnspecified));
            var bind = got.Statuses.First(s => s.Id == "STATUS_BIND");
            Assert.AreEqual((false, false, true), (bind.HasDuration, bind.HasMaxStack, bind.DurationUnspecified), "DurationTurns N");
            Assert.AreEqual(1, got.Statuses.First(s => s.Id == "STATUS_STUN").DurationTurns);
            Assert.AreEqual(99, got.Statuses.First(s => s.Id == "STATUS_SHIELD").MaxStack);

            Assert.AreEqual(5, got.ClassStarts.Count, "04 Player_Stats");
            var nec = got.ClassStarts.First(c => c.ClassName == "NEC");
            Assert.AreEqual((80, 80, 0, 3), (nec.StartHp, nec.MaxHp, nec.StartGold, nec.StartCrystal));
            Assert.AreEqual(4, got.ClassStarts.First(c => c.ClassName == "ELE").StartCrystal);
            Assert.AreEqual(85, got.ClassStarts.First(c => c.ClassName == "HOL").StartHp);
            Assert.AreEqual(40, got.ClassStarts.First(c => c.ClassName == "NEU").StartGold);
        }

        [Test]
        public void BossSteps_MatchGameDataLoader()
        {
            string dir = DesignDataParityTests.FindDesignData();
            var texts = Directory.GetFiles(dir, "*.json").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => File.ReadAllText(f));
            var data = GameData.FromJson(texts);
            var content = ContentImport.Read(data);
            var loaded = GameDataLoader.Load(data);
            Assert.IsEmpty(content.Errors, string.Join("\n", content.Errors));
            foreach (var boss in content.Bosses)
            {
                var steps = loaded.BossPatterns.Get(boss.EnemyId);
                Assert.AreEqual(boss.Steps.Count, steps.Count, boss.BossId);
                for (int i = 0; i < steps.Count; i++)
                {
                    Assert.AreEqual(
                        (boss.Steps[i].Phase, boss.Steps[i].TurnIndex, boss.Steps[i].Action, boss.Steps[i].Damage, boss.Steps[i].EnqueueTwice, boss.Steps[i].OnceOnPhaseEnter, boss.Steps[i].ThresholdPct, boss.Steps[i].CurseId),
                        (steps[i].Phase, steps[i].TurnIndex, steps[i].Action, steps[i].Damage, steps[i].EnqueueTwice, steps[i].OnceOnPhaseEnter, steps[i].ThresholdPct, steps[i].CurseId),
                        boss.BossId + "[" + i + "]");
                }
            }
        }

        [Test]
        public void RejectsDuplicateMonsterZeroHpAndMissingCard()
        {
            var data = new GameData(
                S("Cards_Master", R(("ID", "NEC_MON_001"))),
                S("Monsters_Master",
                    R(("MON_ID", "MON_NEC_L1_001"), ("Name_KO", "뼈 병사"), ("Class", "NEC"), ("Level", 1), ("Card_ID", "NEC_MON_001"), ("HP", 0), ("ATK", 3), ("DEF", 1), ("Crystal", 1)),
                    R(("MON_ID", "MON_NEC_L1_001"), ("Name_KO", "뼈 병사"), ("Class", "NEC"), ("Level", 1), ("Card_ID", "NEC_MON_001"), ("HP", 10), ("ATK", 3), ("DEF", 1), ("Crystal", 1)),
                    R(("MON_ID", "MON_NEC_L1_002"), ("Name_KO", "구울"), ("Class", "NEC"), ("Level", 1), ("Card_ID", "MISSING"), ("HP", 10), ("ATK", 3), ("DEF", 1), ("Crystal", 1))));
            var got = ContentImport.Read(data);
            Assert.IsTrue(got.Errors.Any(e => e.Contains("HP")));
            Assert.IsTrue(got.Errors.Any(e => e.Contains("중복")));
            Assert.IsTrue(got.Errors.Any(e => e.Contains("MISSING")));
        }

        [Test]
        public void RejectsRewardSumAndInvertedGold()
        {
            var data = new GameData(S("Reward_BattleTable",
                R(("BattleType", "Normal"), ("Floor", 1), ("GoldMin", 10), ("GoldMax", 5), ("Drop_Common", 0.5), ("Drop_Uncommon", 0), ("Drop_Rare", 0), ("Drop_Legendary", 0))));
            var got = ContentImport.Read(data);
            Assert.IsTrue(got.Errors.Any(e => e.Contains("Gold")));
            Assert.IsTrue(got.Errors.Any(e => e.Contains("희귀도")));
        }

        [Test]
        public void BindDurationNIsNotZeroTurns()
        {
            var data = new GameData(S("Status_Master",
                R(("Status_ID", "STATUS_BIND"), ("Name_KO", "Bind"), ("StackType", "Duration"), ("TickTiming", "TurnStart"), ("DurationTurns", false)),
                R(("Status_ID", "STATUS_WEAK"), ("Name_KO", "Weak"), ("StackType", "Duration"), ("DurationTurns", 2))));
            var got = ContentImport.Read(data);
            Assert.IsEmpty(got.Errors, string.Join("\n", got.Errors));
            var bind = got.Statuses.First(s => s.Id == "STATUS_BIND");
            Assert.IsTrue(bind.DurationUnspecified);
            Assert.IsFalse(bind.HasDuration);
            Assert.AreEqual(0, bind.DurationTurns);
            Assert.AreEqual(2, got.Statuses.First(s => s.Id == "STATUS_WEAK").DurationTurns);
        }

        [Test]
        public void RelicSheetMustContainTheStageOneEight()
        {
            var data = new GameData(S("Relics_MVP",
                R(("Relic_ID", "REL_BLOOD_CHALICE"), ("Name_KO", "피의 잔"), ("Rarity", "Uncommon"), ("Trigger", "OnSacrifice"), ("Effect", "요약"), ("Text_KO", "문구"))));
            var got = ContentImport.Read(data);
            Assert.IsFalse(got.Errors.Any(e => e.Contains("REL_BLOOD_CHALICE") && e.Contains("핸들러")));
            Assert.AreEqual(7, got.Errors.Count(e => e.Contains("1단계 유물") && e.Contains("없습니다")));
        }
    }
}
