using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 런 골격 (#44, #76 2-1): 9노드 템플릿(`03` §2, D-11·D-34), 진행·충전(D-03), 전투 생성·결과 반영(`14` §2·§3, D-61·D-72·D-73),
    /// 1단계 승리(D-66)·패배·포기, Mimic 폴백(PROG-04), 1단계 완주(PROG-01). 기대값은 손으로 적는다.
    /// </summary>
    [Category("Stage1")]
    public class RunTests
    {
        private static GameDataLoadResult Loaded => StageOneBattleSim.Loaded();

        private static RunState NewNecRun(long seed = 1) =>
            RunFlow.NewRun("RUN-TEST", seed, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });

        // ---------- 템플릿 ----------

        [TestCase(1, 1, NodeKind.Battle)]
        [TestCase(1, 2, NodeKind.Battle)]
        [TestCase(1, 3, NodeKind.Event)]
        [TestCase(1, 4, NodeKind.Battle)]
        [TestCase(1, 5, NodeKind.Elite)]
        [TestCase(1, 6, NodeKind.Event)]
        [TestCase(1, 7, NodeKind.Battle)]
        [TestCase(1, 8, NodeKind.Rest)]
        [TestCase(1, 9, NodeKind.Boss)]
        [TestCase(2, 6, NodeKind.Event)]
        public void Map_Stage1Template(int floor, int node, NodeKind kind)
        {
            Assert.AreEqual(kind, RunMap.KindOf(floor, node, stage1Build: true));
        }

        [Test]
        public void Map_Stage2_Floor2Node6IsShop_D34()
        {
            Assert.AreEqual(NodeKind.Shop, RunMap.KindOf(2, 6, stage1Build: false));
            Assert.AreEqual(NodeKind.Event, RunMap.KindOf(1, 6, stage1Build: false), "1층 노드 6은 항상 이벤트");
        }

        [Test]
        public void PROG_04_MimicInputFallsBackToTreasure_NoMimicGenerated()
        {
            Assert.AreEqual(NodeKind.Treasure, RunMap.Parse("Mimic"));
            Assert.AreEqual(NodeKind.Treasure, RunMap.Parse("Treasure"));
            Assert.AreEqual(NodeKind.Elite, RunMap.Parse("Elite"));
            for (int f = 1; f <= 5; f++)
                for (int n = 1; n <= 9; n++)
                    Assert.AreNotEqual(NodeKind.Treasure, RunMap.KindOf(f, n, stage1Build: false), "MVP 템플릿은 Treasure·Mimic을 만들지 않는다");
        }

        // ---------- 새 런·진행 ----------

        [Test]
        public void NewRun_StartState()
        {
            var run = NewNecRun();
            Assert.AreEqual((1, 0, "F1-N1"), (run.Floor, run.StageIndex, run.CurrentNodeId));
            Assert.AreEqual((80, 80, 0, 0), (run.CurrentHp, run.MaxHp, run.Gold, run.FusionCharge));
            CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToArray(), run.Deck.Select(c => c.InstanceId).ToArray(), "개체 ID 1~10");
            Assert.AreEqual(11, run.NextCardInstanceId);
            CollectionAssert.AreEqual(StageOneBattleSim.NecStartDeck, run.Deck.Select(c => c.CardId).ToArray());
            CollectionAssert.AreEqual(new[] { "REL_BLOOD_CHALICE" }, run.Relics);
            Assert.AreEqual(RunOutcome.InProgress, run.Outcome);
        }

        [Test]
        public void CompleteNode_Advances_ChargeOnNodes369_FloorTransition()
        {
            var run = NewNecRun();
            int[] chargeAfter = { 0, 0, 1, 1, 1, 2, 2, 2, 3 };
            for (int node = 1; node <= 9; node++)
            {
                Assert.AreEqual(RunState.NodeId(1, node), run.CurrentNodeId);
                RunFlow.CompleteNode(run);
                Assert.AreEqual(chargeAfter[node - 1], run.FusionCharge, $"F1-N{node} 완료 뒤 충전");
            }
            Assert.AreEqual((2, 0, "F2-N1"), (run.Floor, run.StageIndex, run.CurrentNodeId), "1층 보스 뒤 2층 노드 1");
            Assert.AreEqual(9, run.VisitedNodeIds.Count);
            Assert.AreEqual("F1-N9", run.VisitedNodeIds.Last());

            for (int node = 1; node <= 3; node++) RunFlow.CompleteNode(run);
            Assert.AreEqual(3, run.FusionCharge, "최대 3 (D-03)");
        }

        [Test]
        public void Stage1Victory_AfterFloor2Boss_D66()
        {
            var run = NewNecRun();
            for (int i = 0; i < 17; i++) RunFlow.CompleteNode(run);
            Assert.AreEqual("F2-N9", run.CurrentNodeId);
            Assert.AreEqual(RunOutcome.InProgress, run.Outcome);
            RunFlow.CompleteNode(run);
            Assert.AreEqual(RunOutcome.Victory, run.Outcome);
            Assert.AreEqual(18, run.VisitedNodeIds.Count);
            Assert.Throws<System.InvalidOperationException>(() => RunFlow.CompleteNode(run), "끝난 런은 진행하지 않는다");
        }

        [Test]
        public void Forfeit_EndsRun()
        {
            var run = NewNecRun();
            RunFlow.Forfeit(run);
            Assert.AreEqual(RunOutcome.Forfeit, run.Outcome);
        }

        // ---------- 전투 생성 (D-73) ----------

        [Test]
        public void Encounters_LoadedFromData()
        {
            var enc = Loaded.Encounters;
            CollectionAssert.AreEqual(new[] { "A", "B" }, enc.Variants(1, 5).Select(e => e.Variant).ToArray(), "1층 엘리트 A·B");
            var n7 = enc.Variants(1, 7).Single();
            Assert.AreEqual(2, n7.Waves.Count);
            Assert.AreEqual(("ENE_1F_GHO_001", 2), n7.Waves[2].Single());
            Assert.AreEqual(NodeKind.Boss, RunMap.Parse(enc.Variants(2, 9).Single().NodeType));
        }

        [Test]
        public void BattleNode_VariantThenWave2_FromNodeSeedStream_D73()
        {
            var run = NewNecRun(seed: 5);
            for (int i = 0; i < 4; i++) RunFlow.CompleteNode(run); // F1-N5 엘리트 (Wave 2 없음)
            int nodeSeed = Seeds.NodeSeed(5, 1, 4);
            bool expectB = new BattleRandom(nodeSeed).Chance(0.5); // 첫 값 = A/B

            var ctx = RunBattle.Start(run, Loaded);
            string expectedBoss = expectB ? "ENE_1F_ELT_002" : "ENE_1F_ELT_001";
            Assert.AreEqual(expectedBoss, ctx.Enemies[0].DataId);
            Assert.AreEqual(0, ctx.PendingWaveCount);

            for (int i = 0; i < 2; i++) RunFlow.CompleteNode(run); // F1-N7 일반 (Wave 2 있음, B 없음)
            nodeSeed = Seeds.NodeSeed(5, 1, 6);
            bool expectWave2 = new BattleRandom(nodeSeed).Chance(0.5); // A/B가 없으니 첫 값 = Wave 2
            ctx = RunBattle.Start(run, Loaded);
            Assert.AreEqual(expectWave2 ? 1 : 0, ctx.PendingWaveCount);
        }

        [Test]
        public void BattleStart_SameNodeSameBattle_AndRunStateCarriedIn()
        {
            var run = NewNecRun(seed: 9);
            run.CurrentHp = 50;
            run.FusionCharge = 2;
            var a = RunBattle.Start(run, Loaded);
            var b = RunBattle.Start(run, Loaded);
            a.StartPlayerTurn();
            b.StartPlayerTurn();
            CollectionAssert.AreEqual(a.Player.Deck.Hand.Select(c => c.InstanceId).ToArray(), b.Player.Deck.Hand.Select(c => c.InstanceId).ToArray(), "같은 노드 시드 → 같은 손패");
            Assert.AreEqual((50, 80), (a.Player.CurrentHP, a.Player.MaxHP.FinalValue));
            Assert.AreEqual(2, a.Player.FusionCharge);
            Assert.AreEqual(1, a.FirstSacrificeBonus, "피의 잔 (D-04·D-06)");
            // F1-N1: 굶주린 구울 ×2, 스테이지 인덱스 0 → Enemies_Master 그대로 14/5/1
            Assert.AreEqual(2, a.Enemies.Count);
            Assert.AreEqual((14, 5, 1), (a.Enemies[0].MaxHP.FinalValue, a.Enemies[0].Attack.FinalValue, a.Enemies[0].Defense.FinalValue));
        }

        [Test]
        public void BattleStart_EnemiesScaledByStageIndex()
        {
            var run = NewNecRun();
            for (int i = 0; i < 6; i++) RunFlow.CompleteNode(run); // F1-N7, 스테이지 인덱스 6
            var ctx = RunBattle.Start(run, Loaded);
            // 구울 HP 12 × (1 + 0.023 × 6) = 13.656 → 14, ATK 6 × 1.168 = 7.008 → 7, DEF 0 (`16` §2, EnemyScaler)
            var ghoul = ctx.Enemies.First(e => e.DataId == "ENE_1F_GHO_002");
            Assert.AreEqual((14, 7, 0), (ghoul.MaxHP.FinalValue, ghoul.Attack.FinalValue, ghoul.Defense.FinalValue));
        }

        // ---------- 결과 반영 ----------

        [Test]
        public void Finish_Victory_HpCarried_FusionAppliedByInstanceId_NewIdNotReused()
        {
            var run = RunFlow.NewRun("RUN-F", 3, "NEC", 80, new[] { "NEC_MON_002", "NEC_MON_002", "NEC_MON_001", "NEC_MON_006", "NEC_SKL_003" }, new string[0]);
            run.Deck.RemoveAll(c => c.InstanceId == 5); // 개체 5를 런에서 뺐다 → 전투 덱 최댓값은 4지만 새 ID는 6이어야 한다
            run.FusionCharge = 1;
            var ctx = RunBattle.Start(run, Loaded);
            ctx.Fusion = StageOneFusion.Create();
            ctx.StartPlayerTurn();
            var ghouls = ctx.Player.Deck.Hand.Where(c => c.CardId == "NEC_MON_002").ToList();
            Assert.AreEqual(2, ghouls.Count);
            var fused = ctx.Fuse(ghouls[0], ghouls[1]);
            Assert.AreEqual("NEC_MON_013", fused.Result.CardId, "FUS_001");
            Assert.AreEqual(6, fused.Result.InstanceId, "런의 NextCardInstanceId부터 (D-73)");

            ctx.Player.CurrentHP = 61;
            foreach (var e in ctx.Enemies.ToList()) ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 999, IsStatusTick = true });
            ctx.Queue.Process();
            Assert.AreEqual(BattleOutcome.Victory, ctx.EvaluateOutcome());
            RunBattle.Finish(run, ctx);

            Assert.AreEqual(61, run.CurrentHp);
            Assert.AreEqual(0, run.FusionCharge, "충전 소모가 남는다");
            CollectionAssert.AreEqual(new[] { 3, 4, 6 }, run.Deck.Select(c => c.InstanceId).OrderBy(x => x).ToArray(), "재료 1·2 제거, 결과 6 추가");
            var result = run.Deck.Single(c => c.InstanceId == 6);
            Assert.AreEqual(("NEC_MON_013", true), (result.CardId, result.FromFusion));
            Assert.AreEqual(7, run.NextCardInstanceId);
        }

        [Test]
        public void Finish_Defeat_EndsRun_DeckUnchanged()
        {
            var run = NewNecRun();
            var ctx = RunBattle.Start(run, Loaded);
            ctx.Player.CurrentHP = 0;
            Assert.AreEqual(BattleOutcome.Defeat, ctx.EvaluateOutcome());
            RunBattle.Finish(run, ctx);
            Assert.AreEqual(RunOutcome.Defeat, run.Outcome);
            Assert.AreEqual(0, run.CurrentHp);
            Assert.AreEqual(10, run.Deck.Count);
        }

        // ---------- PROG-01 ----------

        [Test]
        public void PROG_01_Stage1Run_18Nodes_Floor2BossVictory()
        {
            var run = NewNecRun(seed: 2026);
            var bot = new SimBot();
            while (run.Outcome == RunOutcome.InProgress)
            {
                var kind = RunMap.KindOf(run.Floor, run.Node, stage1Build: true);
                if (kind == NodeKind.Battle || kind == NodeKind.Elite || kind == NodeKind.Boss)
                {
                    var ctx = RunBattle.Start(run, Loaded);
                    while (ctx.Outcome == BattleOutcome.None)
                    {
                        ctx.StartPlayerTurn();
                        bot.PlayMainPhase(ctx);
                        ctx.EndPlayerTurn();
                    }
                    RunBattle.Finish(run, ctx);
                    if (run.Outcome != RunOutcome.InProgress) break;
                    // 보상 (2-2): 첫 카드, 유물 후보가 있으면 첫 유물
                    var reward = RewardFlow.ForBattle(run, kind, Loaded);
                    Assert.IsTrue(RewardFlow.Confirm(run, reward, 0, reward.RelicChoices.Count > 0 ? 0 : (int?)null));
                }
                else if (kind == NodeKind.Rest)
                {
                    // 휴식 (2-3): 회복할 수 있으면 회복, 아니면 첫 강화 가능 카드
                    if (!RestFlow.Heal(run, Loaded))
                    {
                        var up = run.Deck.FirstOrDefault(c => RunDeckRules.IsUpgradable(c, Loaded.Cards));
                        if (up != null) RestFlow.Upgrade(run, Loaded, up.InstanceId);
                    }
                }
                else if (kind == NodeKind.Event)
                {
                    // 이벤트 (2-3): 들어가서 떠난다 (옵션 3)
                    string ev = EventFlow.Enter(run, Loaded);
                    Assert.IsTrue(EventFlow.Confirm(run, Loaded, EventFlow.Choose(run, Loaded, ev, 3), new EventChoice()));
                }
                RunFlow.CompleteNode(run);
            }
            TestContext.Progress.WriteLine($"[PROG-01] seed 2026: {run.Outcome}, HP {run.CurrentHp}/{run.MaxHp}, nodes {run.VisitedNodeIds.Count}, charge {run.FusionCharge}, gold {run.Gold}, deck {run.Deck.Count}, relics {string.Join(",", run.Relics)}");
            Assert.AreEqual(RunOutcome.Victory, run.Outcome);
            Assert.AreEqual(18, run.VisitedNodeIds.Count);
            Assert.AreEqual("F2-N9", run.VisitedNodeIds.Last());
            Assert.AreEqual(10 + 12, run.Deck.Count, "전투 노드 12개(일반 8·엘리트 2·보스 2)마다 카드 1장");
            Assert.AreEqual(4, run.NodeEventIds.Count, "이벤트 노드 4개 (1·2층 노드 3·6)");
            Assert.AreEqual(4, run.NodeEventIds.Values.Distinct().Count(), "이번 런에서 겹치지 않는다 (D-69)");
            CollectionAssert.IsSubsetOf(new[] { "REL_BOSS_1F", "REL_BOSS_2F" }, run.Relics);
            Assert.That(run.Gold, Is.InRange(4 * 5 + 4 * 10 + 15 + 30 + 25 + 50, 4 * 10 + 4 * 20 + 25 + 50 + 40 + 80), "골드 합: 1층 일반 4×5~10, 2층 일반 4×10~20, 엘리트 15~25·30~50, 보스 25~40·50~80");
        }
    }
}
