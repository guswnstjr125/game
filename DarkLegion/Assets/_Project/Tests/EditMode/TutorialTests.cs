using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 1층 노드 1 희생 안내 전투의 코어 상태 (#44, #76 2-5): `01` D-32·D-76, `14` §2·§3. 화면 안내는 #46.
    /// </summary>
    [Category("Stage1")]
    public class TutorialTests
    {
        private static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        private static RunState Run(bool tutorial, long seed = 3) =>
            RunFlow.NewRun("RUN-T", seed, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" }, tutorialEnabled: tutorial);

        private static readonly string[] FixedHand = { "NEU_MON_001", "NEC_MON_001", "NEC_MON_002", "NEC_SKL_001", "NEC_ACT_001" };

        [Test]
        public void NewRun_TutorialFlags()
        {
            var run = Run(true);
            Assert.AreEqual((true, false), (run.TutorialEnabled, run.TutorialCompleted));
            Assert.IsTrue(RunBattle.IsTutorialBattle(run));
            Assert.IsFalse(RunBattle.IsTutorialBattle(Run(false)), "TutorialDone이면 안내 없음");
        }

        [Test]
        public void FirstTurnHand_IsFixedFive_FirstInstances_D32_D76()
        {
            var run = Run(true);
            // 시작 덱 개체 ID: NEC_MON_001=1, 002=2, 003=3, 004=4, NEC_SKL_001=5, SKL_003=6, NEC_ACT_001=7, NEU_MON_001=8, NEU_MON_003=9, NEU_ACT_001=10
            var ctx = RunBattle.Start(run, Data);
            Assert.IsTrue(ctx.IsTutorial);
            ctx.StartPlayerTurn();
            CollectionAssert.AreEquivalent(FixedHand, ctx.Player.Deck.Hand.Select(c => c.CardId).ToArray());
            CollectionAssert.AreEquivalent(new[] { 8, 1, 2, 5, 7 }, ctx.Player.Deck.Hand.Select(c => c.InstanceId).ToArray(), "각 카드 ID의 첫 개체");
            Assert.AreEqual(5, ctx.Player.Deck.DrawPile.Count, "나머지 5장은 뽑을 더미");
        }

        [Test]
        public void TutorialSteps_Crystal3To5_Draw2_ThenSummon()
        {
            var run = Run(true);
            var ctx = RunBattle.Start(run, Data);
            ctx.StartPlayerTurn();
            var hand = ctx.Player.Deck.Hand;
            // ① 파편 소환 (0 Crystal)
            Assert.AreEqual(PlayResult.Ok, ctx.PlayCard(hand.First(c => c.CardId == "NEU_MON_001"), null, SlotPosition.Back));
            ctx.Queue.Process();
            var shard = ctx.Allies.Single(a => a.DataId == "NEU_MON_001");
            // ② 영혼 추출 → 파편 희생: Crystal 3 + 파편 1 + 피의 잔 1 = 5, 드로우 2
            Assert.AreEqual(PlayResult.Ok, ctx.PlayCard(hand.First(c => c.CardId == "NEC_SKL_001"), shard));
            ctx.Queue.Process();
            int handBefore = ctx.Player.Deck.Hand.Count;
            Assert.AreEqual(SacrificeResult.Ok, ctx.Sacrifice(shard));
            ctx.Queue.Process();
            Assert.AreEqual(5, ctx.Player.CurrentCrystal, "D-32: 3 → 5");
            Assert.AreEqual(handBefore + 2, ctx.Player.Deck.Hand.Count, "드로우 2");
            // ③ 뼈 병사·구울 소환 (1 + 1)
            Assert.AreEqual(PlayResult.Ok, ctx.PlayCard(ctx.Player.Deck.Hand.First(c => c.CardId == "NEC_MON_001"), null, SlotPosition.Front));
            Assert.AreEqual(PlayResult.Ok, ctx.PlayCard(ctx.Player.Deck.Hand.First(c => c.CardId == "NEC_MON_002"), null, SlotPosition.Middle));
            ctx.Queue.Process();
            Assert.AreEqual(3, ctx.Player.CurrentCrystal);
        }

        [Test]
        public void SameSeed_SameTutorialBattle_AndRestIsShuffled()
        {
            var a = RunBattle.Start(Run(true, seed: 7), Data);
            var b = RunBattle.Start(Run(true, seed: 7), Data);
            CollectionAssert.AreEqual(a.Player.Deck.DrawPile.Select(c => c.InstanceId).ToArray(), b.Player.Deck.DrawPile.Select(c => c.InstanceId).ToArray());
        }

        [Test]
        public void NotTutorial_NormalShuffledHand()
        {
            var run = Run(false);
            var ctx = RunBattle.Start(run, Data);
            Assert.IsFalse(ctx.IsTutorial);
            ctx.StartPlayerTurn();
            Assert.AreEqual(5, ctx.Player.Deck.Hand.Count);
        }

        [Test]
        public void Finish_VictoryOrDefeat_MarksCompleted_Once()
        {
            var run = Run(true);
            var ctx = RunBattle.Start(run, Data);
            ctx.StartPlayerTurn();
            foreach (var e in ctx.Enemies.ToList()) ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 999, IsStatusTick = true });
            ctx.Queue.Process();
            Assert.AreEqual(BattleOutcome.Victory, ctx.EvaluateOutcome());
            RunBattle.Finish(run, ctx);
            Assert.IsTrue(run.TutorialCompleted, "안내 전투 승리 → 완료 (`14` §3)");
            Assert.IsFalse(RunBattle.IsTutorialBattle(run));

            var lost = Run(true);
            var c2 = RunBattle.Start(lost, Data);
            c2.Player.CurrentHP = 0;
            c2.EvaluateOutcome();
            RunBattle.Finish(lost, c2);
            Assert.IsTrue(lost.TutorialCompleted, "패배도 완료 (`14` §3)");
        }

        [Test]
        public void OtherNodes_NeverTutorial()
        {
            var run = Run(true);
            RunFlow.CompleteNode(run); // F1-N2
            Assert.IsFalse(RunBattle.IsTutorialBattle(run));
            Assert.IsFalse(RunBattle.Start(run, Data).IsTutorial);
        }
    }
}
