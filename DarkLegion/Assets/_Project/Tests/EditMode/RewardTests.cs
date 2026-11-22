using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 전투 보상 (#44, #76 2-2): `06` Reward_BattleTable·Reward_EliteRelic, `14` Boss_List, `08` Cards_FloorUnlock,
    /// D-16·D-35·D-37·D-44·D-49·D-66·D-67·D-75. PROG-02·08·09. 기대값은 데이터에서 손으로 적는다.
    /// </summary>
    [Category("Stage1")]
    public class RewardTests
    {
        private static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        private static RunState Run(long seed = 1, string startClass = "NEC", bool stage1 = true)
        {
            var run = RunFlow.NewRun("RUN-R", seed, startClass, 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.Stage1Build = stage1;
            return run;
        }

        private static void GoTo(RunState run, int floor, int node)
        {
            run.Floor = floor;
            run.StageIndex = node - 1;
        }

        // ---------- 표 ----------

        [Test]
        public void Tables_RewardRowsFromData()
        {
            var n1 = Data.Rewards.Row(NodeKind.Battle, 1);
            Assert.AreEqual((5, 10), (n1.GoldMin, n1.GoldMax));
            Assert.AreEqual(0.60, n1.Rarity["C"], 1e-9);
            Assert.AreEqual(0.28, n1.Rarity["U"], 1e-9);
            Assert.AreEqual(0.10, n1.Rarity["R"], 1e-9);
            Assert.AreEqual(0.02, n1.Rarity["L"], 1e-9);
            Assert.AreEqual((25, 40), (Data.Rewards.Row(NodeKind.Boss, 1).GoldMin, Data.Rewards.Row(NodeKind.Boss, 1).GoldMax), "PROG-02 1층 보스");
            Assert.AreEqual((50, 80), (Data.Rewards.Row(NodeKind.Boss, 2).GoldMin, Data.Rewards.Row(NodeKind.Boss, 2).GoldMax), "PROG-02 2층 보스");
            CollectionAssert.AreEqual(new[] { "REL_COM_001", "REL_COM_002", "REL_COM_003" }, Data.Rewards.EliteRelicPool(1).ToArray());
            CollectionAssert.AreEqual(new[] { "REL_UNC_001", "REL_UNC_002", "REL_UNC_003" }, Data.Rewards.EliteRelicPool(2).ToArray());
            Assert.AreEqual("REL_BOSS_1F", Data.Rewards.BossRelic(1));
            Assert.AreEqual("REL_BOSS_2F", Data.Rewards.BossRelic(2));
        }

        [Test]
        public void Cards_RarityAndClassFromCardsMaster()
        {
            var c = Data.Cards.Get("NEC_ACT_004");
            Assert.AreEqual(("R", "NEC"), (c.Rarity, c.Class));
            Assert.AreEqual(("C", "NEU"), (Data.Cards.Get("NEU_MON_001").Rarity, Data.Cards.Get("NEU_MON_001").Class));
        }

        // ---------- 후보 풀 (D-75) ----------

        [Test]
        public void Pool_Stage1_30Cards_15C_14U_1R_NoFusionOnly()
        {
            var pool = CardPool.Candidates(Data.Cards, 1, "NEC", NodeKind.Battle, stage1: true);
            Assert.AreEqual(30, pool.Count);
            Assert.AreEqual((15, 14, 1, 0), (pool.Count(c => c.Rarity == "C"), pool.Count(c => c.Rarity == "U"), pool.Count(c => c.Rarity == "R"), pool.Count(c => c.Rarity == "L")));
            Assert.IsFalse(pool.Any(c => c.Id == "NEC_MON_013" || c.Id == "NEC_MON_014"), "융합 전용은 드롭 풀에 없다");
            Assert.AreEqual(30, CardPool.Candidates(Data.Cards, 2, "NEC", NodeKind.Boss, stage1: true).Count, "2층도 32장 제한 안의 30장");
        }

        [Test]
        public void Pool_FullBuild_NecStart_32Then48_EleStart50()
        {
            // `03` §8: NEC·NEU 시작 1층 32장, 2층 +16. ELE 시작은 ELE 카드 유효 해금층 1 → 1층 50 (D-07·D-17)
            Assert.AreEqual(32, CardPool.Candidates(Data.Cards, 1, "NEC", NodeKind.Battle, stage1: false).Count);
            Assert.AreEqual(48, CardPool.Candidates(Data.Cards, 2, "NEC", NodeKind.Battle, stage1: false).Count);
            Assert.AreEqual(50, CardPool.Candidates(Data.Cards, 1, "ELE", NodeKind.Battle, stage1: false).Count);
        }

        // ---------- 3택1 채우기 (D-49·D-75) ----------

        [Test]
        public void Roll_RarityRenormalizedWithoutEmpty_FillsDistinctWithinRoll()
        {
            var pool = CardPool.Candidates(Data.Cards, 1, "NEC", NodeKind.Battle, stage1: true);
            var row = Data.Rewards.Row(NodeKind.Battle, 1);
            var rolls = new List<string>();
            var rng = new BattleRandom(123);
            int rewards = 10000;
            int rareRewards = 0;
            for (int i = 0; i < rewards; i++)
            {
                var log = new List<string>();
                var cards = CardRewardRoller.Roll(rng, pool, row, 3, log);
                Assert.AreEqual(3, cards.Count);
                rolls.AddRange(log);
                if (log[0] == "C" || log[0] == "U")
                {
                    Assert.AreEqual(1, log.Count, "후보가 3장 이상이면 한 번에 채운다");
                    Assert.AreEqual(3, cards.Distinct().Count(), "한 번 굴린 희귀도 안에서는 겹치지 않는다");
                }
                if (log[0] == "R")
                {
                    rareRewards++;
                    Assert.AreEqual("NEC_ACT_004", cards[0], "Rare 칸은 유일한 Rare 후보 (PROG-09)");
                    Assert.Greater(log.Count, 1, "나머지 칸은 다시 굴린다");
                }
            }
            // PROG-09: Legendary 0회, 굴림의 희귀도 비율 = 빈 희귀도 제외 후 정규화 (C .60/.98, U .28/.98, R .10/.98) ±1.5%p
            Assert.AreEqual(0, rolls.Count(r => r == "L"));
            Assert.AreEqual(0.60 / 0.98, rolls.Count(r => r == "C") / (double)rolls.Count, 0.015);
            Assert.AreEqual(0.28 / 0.98, rolls.Count(r => r == "U") / (double)rolls.Count, 0.015);
            Assert.AreEqual(0.10 / 0.98, rolls.Count(r => r == "R") / (double)rolls.Count, 0.015);
            Assert.Greater(rareRewards, 0);
        }

        [Test]
        public void PROG_08_RewardsNeverLeaveStage1Pool()
        {
            var allowed = new HashSet<string>(Stage1Scope.Cards);
            int n = 0;
            for (long seed = 1; n < 10000; seed++)
            {
                foreach (var (floor, node, kind) in new[] { (1, 1, NodeKind.Battle), (1, 5, NodeKind.Elite), (1, 9, NodeKind.Boss), (2, 2, NodeKind.Battle), (2, 5, NodeKind.Elite), (2, 9, NodeKind.Boss) })
                {
                    var run = Run(seed);
                    GoTo(run, floor, node);
                    var r = RewardFlow.ForBattle(run, kind, Data);
                    foreach (var c in r.CardChoices) Assert.IsTrue(allowed.Contains(c), c);
                    n++;
                }
            }
        }

        // ---------- 전투 보상 ----------

        [Test]
        public void Battle_GoldInRange_BothEndsReachable_ThreeCards_NoRelic()
        {
            var golds = new HashSet<int>();
            for (long seed = 1; seed <= 300; seed++)
            {
                var run = Run(seed);
                var r = RewardFlow.ForBattle(run, NodeKind.Battle, Data);
                Assert.That(r.Gold, Is.InRange(5, 10));
                golds.Add(r.Gold);
                Assert.AreEqual(3, r.CardChoices.Count);
                Assert.IsEmpty(r.RelicChoices);
                Assert.IsNull(r.AutoRelic);
                Assert.AreEqual("F1-N1", r.NodeId);
            }
            CollectionAssert.AreEquivalent(new[] { 5, 6, 7, 8, 9, 10 }, golds.ToArray(), "GoldMin~GoldMax 양 끝 포함 (D-75)");
        }

        [Test]
        public void Reward_SameNodeSameReward_SeparateFromBattleStream_D75()
        {
            var a = RewardFlow.ForBattle(Run(7), NodeKind.Battle, Data);
            var b = RewardFlow.ForBattle(Run(7), NodeKind.Battle, Data);
            Assert.AreEqual(a.Gold, b.Gold);
            CollectionAssert.AreEqual(a.CardChoices, b.CardChoices);
            // 보상 난수 = BattleRandom(NodeSeed * 31 + 17)의 첫 값이 골드
            int nodeSeed = Seeds.NodeSeed(7, 1, 0);
            Assert.AreEqual(unchecked(nodeSeed * 31 + 17), Seeds.RewardSeed(nodeSeed));
            Assert.AreEqual(new BattleRandom(Seeds.RewardSeed(nodeSeed)).Next(5, 11), a.Gold);
        }

        [Test]
        public void Elite_UnownedRelicsInPoolOrder_MustPickOne()
        {
            var run = Run(3);
            GoTo(run, 1, 5);
            run.Relics.Add("REL_COM_002");
            var r = RewardFlow.ForBattle(run, NodeKind.Elite, Data);
            Assert.That(r.Gold, Is.InRange(15, 25));
            CollectionAssert.AreEqual(new[] { "REL_COM_001", "REL_COM_003" }, r.RelicChoices, "미보유만, A→C 순서 (D-68·D-75)");
            Assert.AreEqual(3, r.CardChoices.Count);

            Assert.IsFalse(RewardFlow.Confirm(run, r, cardIndex: null, relicIndex: null), "후보가 있으면 유물은 반드시 고른다");
            Assert.IsFalse(RewardFlow.Confirm(run, r, cardIndex: null, relicIndex: 2), "범위 밖");
            Assert.IsTrue(RewardFlow.Confirm(run, r, cardIndex: null, relicIndex: 1));
            CollectionAssert.Contains(run.Relics, "REL_COM_003");
        }

        [Test]
        public void Elite_AllOwned_NoRelicStep()
        {
            var run = Run(3);
            GoTo(run, 2, 5);
            run.Relics.AddRange(new[] { "REL_UNC_001", "REL_UNC_002", "REL_UNC_003" });
            var r = RewardFlow.ForBattle(run, NodeKind.Elite, Data);
            Assert.IsEmpty(r.RelicChoices, "후보 0이면 생략 (D-68)");
            Assert.IsTrue(RewardFlow.Confirm(run, r, cardIndex: 0, relicIndex: null));
        }

        [Test]
        public void Boss_GoldCardAndFixedBossRelic_ThenStage1Victory()
        {
            var run = Run(11);
            GoTo(run, 2, 9);
            var r = RewardFlow.ForBattle(run, NodeKind.Boss, Data);
            Assert.That(r.Gold, Is.InRange(50, 80));
            Assert.AreEqual("REL_BOSS_2F", r.AutoRelic, "Boss_List 고정 (D-16)");
            Assert.IsEmpty(r.RelicChoices);
            int gold = run.Gold;
            Assert.IsTrue(RewardFlow.Confirm(run, r, cardIndex: null, relicIndex: null), "카드 건너뛰기 (D-37)");
            Assert.AreEqual(gold + r.Gold, run.Gold);
            CollectionAssert.Contains(run.Relics, "REL_BOSS_2F");
            Assert.AreEqual(10, run.Deck.Count, "건너뛰면 카드 없음");
            RunFlow.CompleteNode(run);
            Assert.AreEqual(RunOutcome.Victory, run.Outcome, "보스 보상 확정 후 승리 (D-66)");
        }

        [Test]
        public void Confirm_AddsCardAsNewInstance_OnlyOnce_D67()
        {
            var run = Run(5);
            var r = RewardFlow.ForBattle(run, NodeKind.Battle, Data);
            Assert.IsTrue(RewardFlow.Confirm(run, r, cardIndex: 2, relicIndex: null));
            var added = run.Deck.Last();
            Assert.AreEqual((11, r.CardChoices[2], false), (added.InstanceId, added.CardId, added.Upgraded), "개체 ID 11 (NextCardInstanceId)");
            Assert.AreEqual(12, run.NextCardInstanceId);
            int gold = run.Gold;
            Assert.IsFalse(RewardFlow.Confirm(run, r, cardIndex: 0, relicIndex: null), "같은 확인이 다시 와도 처리하지 않는다 (D-67)");
            Assert.AreEqual((gold, 11), (run.Gold, run.Deck.Count));
            Assert.IsFalse(RewardFlow.Confirm(run, RewardFlow.ForBattle(run, NodeKind.Battle, Data), cardIndex: 3, relicIndex: null), "카드 번호 범위 밖");
        }

        [Test]
        public void Rest_Event_NotBattleRewards()
        {
            Assert.Throws<System.ArgumentException>(() => RewardFlow.ForBattle(Run(), NodeKind.Event, Data));
        }
    }
}
