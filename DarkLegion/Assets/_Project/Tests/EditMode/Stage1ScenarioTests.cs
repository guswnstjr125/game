using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// `32` §10 1단계 Must/Should Pass 중 코어로 확인할 수 있고 ID가 붙은 테스트가 없던 시나리오.
    /// AI-01·AI-03·CARD-01·REST-01·UPG-01·TUT-01·EVT-02·EVT-03 (저장·화면 쪽 절반은 #46·#47).
    /// </summary>
    [Category("Stage1")]
    public class Stage1ScenarioTests
    {
        private static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        private static RunState Run(long seed = 1, bool tutorial = false) =>
            RunFlow.NewRun("RUN-S1", seed, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" }, tutorialEnabled: tutorial);

        private static RuntimeMonster Ally(BattleContext ctx, SlotPosition pos, int hp)
        {
            var m = new RuntimeMonster("A" + pos, "NEC_MON_001", hp, 3, 0, null);
            ctx.State = BattleState.PlayerPhase;
            ctx.Field.TrySummonAlly(m, pos, ctx);
            ctx.Queue.Process();
            return m;
        }

        private static BattleContext EmptyBattle()
        {
            var ctx = new BattleContext(5) { Cards = Data.Cards };
            ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], ctx.Random));
            return ctx;
        }

        // ---------- AI-01 / AI-03 (Should Pass) ----------

        [Test]
        public void AI_01_StandardTargetRules()
        {
            var ctx = EmptyBattle();
            var front = Ally(ctx, SlotPosition.Front, 20);
            var middle = Ally(ctx, SlotPosition.Middle, 5);
            var back = Ally(ctx, SlotPosition.Back, 30);
            Assert.AreEqual(front, EnemyAi.SingleTarget(ctx, "FrontLine"));
            Assert.AreEqual(back, EnemyAi.SingleTarget(ctx, "Back"));
            Assert.AreEqual(middle, EnemyAi.SingleTarget(ctx, "LowestHp"));
            back.Statuses.Apply(StatusType.Taunt, 1, ctx);
            Assert.AreEqual(back, EnemyAi.SingleTarget(ctx, "FrontLine"), "Taunt 우선");
        }

        [Test]
        public void AI_03_NoTargetFallback()
        {
            var ctx = EmptyBattle();
            Assert.AreEqual(ctx.Player, EnemyAi.SingleTarget(ctx, "FrontLine"), "아군 0 → 플레이어 (D-29)");
            var middle = Ally(ctx, SlotPosition.Middle, 10);
            Assert.AreEqual(middle, EnemyAi.SingleTarget(ctx, "FrontLine"), "Front가 비면 앞줄 = Middle");
            Assert.AreEqual(middle, EnemyAi.SingleTarget(ctx, "Back"), "Back이 비면 가장 뒤의 아군");
            var front = Ally(ctx, SlotPosition.Front, 10);
            front.CurrentHP = 0;
            front.Statuses.Apply(StatusType.Shield, 0, ctx);
            ctx.Queue.EnqueueMain(new DamageAction { Target = front, BaseDamage = 99, IsStatusTick = true }); // 사망 처리 대기
            ctx.Queue.Process();
            Assert.AreEqual(middle, EnemyAi.SingleTarget(ctx, "FrontLine"), "죽은 유닛은 고르지 않는다");
        }

        // ---------- CARD-01 ----------

        [Test]
        public void CARD_01_EveryStage1SkillActionAndCursePlaysById()
        {
            foreach (var id in Stage1Scope.Cards.Where(id => Data.Cards.Get(id).Type != CardType.Monster).Concat(new[] { "CUR_003" }))
            {
                var run = RunFlow.NewRun("RUN-C1", 3, "NEC", 80, new[] { id, "NEC_MON_001", "NEC_MON_002" }, new string[0]);
                var ctx = RunBattle.Start(run, Data);
                ctx.StartPlayerTurn();
                ctx.Player.CurrentCrystal = 10;
                var card = ctx.Player.Deck.Hand.First(c => c.CardId == id);
                var mon = ctx.Player.Deck.Hand.First(c => c.CardId == "NEC_MON_001");
                Assert.AreEqual(PlayResult.Ok, ctx.PlayCard(mon, null, SlotPosition.Front));
                ctx.Queue.Process();
                var ally = ctx.Allies[0];
                var enemy = ctx.Enemies[0];
                var other = ctx.Player.Deck.Hand.First(c => c.CardId == "NEC_MON_002");
                PlayResult r;
                switch (id)
                {
                    case "NEC_SKL_001":
                    case "NEC_ACT_001":
                    case "CUR_003": r = ctx.PlayCard(card, ally); break;
                    case "NEC_SKL_003":
                    case "NEC_ACT_004": r = ctx.PlayCard(card, enemy); break;
                    case "NEU_SKL_001": r = ctx.PlayCard(card, ally, SlotPosition.Middle); break;
                    case "NEU_ACT_002": r = ctx.PlayCard(card, null, chosenCard: other); break;
                    case "NEC_SKL_004":
                        ctx.Player.Deck.DiscardFromHand(other);
                        r = ctx.PlayCard(card, null, chosenCard: other);
                        break;
                    default: r = ctx.PlayCard(card, null); break;
                }
                Assert.AreEqual(PlayResult.Ok, r, id);
                ctx.Queue.Process();
                while (ctx.State == BattleState.WaitingDiscard) ctx.ResolveDiscard(ctx.Player.Deck.Hand.First());
            }
        }

        // ---------- REST-01 ----------

        [TestCase(80, 24)]
        [TestCase(70, 21)]
        public void REST_01_HealFloor30Percent(int maxHp, int heal)
        {
            var run = Run();
            run.MaxHp = maxHp;
            run.CurrentHp = 1;
            Assert.AreEqual(heal, RestFlow.Options(run, Data).HealAmount);
            run.CurrentHp = maxHp - 1;
            RestFlow.Heal(run, Data);
            Assert.AreEqual(maxHp, run.CurrentHp, "MaxHP 초과 없음");
        }

        [Test]
        public void REST_01_ContinueOnly_CompletesNode8_NoChargeChange()
        {
            var run = Run();
            for (int i = 0; i < 7; i++) RunFlow.CompleteNode(run);
            Assert.AreEqual("F1-N8", run.CurrentNodeId);
            Assert.AreEqual(NodeKind.Rest, RunMap.KindOf(1, 8, true));
            foreach (var c in run.Deck) c.Upgraded = true;
            Assert.IsTrue(RestFlow.Options(run, Data).ContinueOnly);
            int charge = run.FusionCharge;
            RunFlow.CompleteNode(run);
            Assert.AreEqual(("F1-N9", charge), (run.CurrentNodeId, run.FusionCharge), "노드 8은 충전 없음");
        }

        // ---------- UPG-01 (코어 절반: Cards_Upgrade 카드별 값) ----------

        [Test]
        public void UPG_01_RestUpgradeUsesCardsUpgradeValuesInBattle()
        {
            var run = RunFlow.NewRun("RUN-U", 2, "NEC", 80, new[] { "NEC_MON_001", "NEC_ACT_004" }, new string[0]);
            Assert.IsTrue(RestFlow.Upgrade(run, Data, 1));
            Assert.IsTrue(RestFlow.Upgrade(run, Data, 2));
            var ctx = RunBattle.Start(run, Data);
            ctx.StartPlayerTurn();
            ctx.Player.CurrentCrystal = 10;
            ctx.PlayCard(ctx.Player.Deck.Hand.First(c => c.CardId == "NEC_MON_001"), null, SlotPosition.Front);
            ctx.Queue.Process();
            Assert.AreEqual(12, ctx.Allies[0].MaxHP.FinalValue, "뼈 병사 Up_Params HP=12");
            var target = ctx.Enemies[0];
            int hp = target.CurrentHP;
            ctx.PlayCard(ctx.Player.Deck.Hand.First(c => c.CardId == "NEC_ACT_004"), target);
            ctx.Queue.Process();
            // 생명력 흡수 Up_Params Damage=19 − Front 구울(DEF 1 + 1) = 17 → HP 14가 0 이하로
            Assert.AreEqual(System.Math.Max(0, hp - 17), System.Math.Max(0, target.CurrentHP));
        }

        // ---------- TUT-01 (코어 절반) ----------

        [Test]
        public void TUT_01_FirstRunOnly_AfterCompletionNormal()
        {
            var first = Run(tutorial: true);
            var ctx = RunBattle.Start(first, Data);
            Assert.IsTrue(ctx.IsTutorial);
            foreach (var e in ctx.Enemies.ToList()) ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 999, IsStatusTick = true });
            ctx.Queue.Process();
            ctx.EvaluateOutcome();
            RunBattle.Finish(first, ctx);
            Assert.IsTrue(first.TutorialCompleted);
            Assert.IsFalse(RunBattle.Start(first, Data).IsTutorial, "같은 런에서 다시 열어도 일반 전투");
            var second = Run(seed: 2, tutorial: false); // 두 번째 런: meta.TutorialDone = true → tutorialEnabled false
            Assert.IsFalse(RunBattle.Start(second, Data).IsTutorial);
        }

        // ---------- EVT-02 ----------

        private static RunState AtEvent(int floor = 1, int node = 3)
        {
            var run = Run();
            run.Floor = floor;
            run.StageIndex = node - 1;
            return run;
        }

        [Test]
        public void EVT_02_Boundaries_Gold7_8_Hp4_5_MaxHp20_21()
        {
            var run = AtEvent();
            run.Gold = 7;
            Assert.IsFalse(EventFlow.IsEnabled(run, Data, "EVT_WANDER_MERCHANT", 1));
            run.Gold = 8;
            Assert.IsTrue(EventFlow.IsEnabled(run, Data, "EVT_WANDER_MERCHANT", 1));
            run.CurrentHp = 4;
            Assert.IsFalse(EventFlow.IsEnabled(run, Data, "EVT_WANDER_MERCHANT", 2));
            run.CurrentHp = 5;
            Assert.IsTrue(EventFlow.IsEnabled(run, Data, "EVT_WANDER_MERCHANT", 2));
            run.MaxHp = 20;
            Assert.IsFalse(EventFlow.IsEnabled(run, Data, "EVT_DEVIL_PACT", 1));
            run.MaxHp = 21;
            Assert.IsTrue(EventFlow.IsEnabled(run, Data, "EVT_DEVIL_PACT", 1));
        }

        [Test]
        public void EVT_02_RelicCandidates_3_1_0()
        {
            var run = AtEvent();
            Assert.AreEqual(3, EventFlow.Choose(run, Data, "EVT_DEVIL_PACT", 2).RelicChoices.Count);
            run.Relics.AddRange(new[] { "REL_COM_001", "REL_COM_002" });
            Assert.AreEqual(1, EventFlow.Choose(run, Data, "EVT_DEVIL_PACT", 2).RelicChoices.Count);
            run.Relics.Add("REL_COM_003");
            Assert.IsFalse(EventFlow.IsEnabled(run, Data, "EVT_DEVIL_PACT", 2));
            Assert.IsFalse(EventFlow.IsEnabled(run, Data, "EVT_CURSED_CHEST", 1));
        }

        [Test]
        public void EVT_02_SoulPrisonOneBranch_ChoiceStableOnRestart()
        {
            for (long seed = 1; seed <= 50; seed++)
            {
                var run = Run(seed);
                run.StageIndex = 2;
                var o = EventFlow.Choose(run, Data, "EVT_SOUL_PRISON", 1);
                Assert.AreNotEqual(o.ForcedBattle, o.CardChoices.Count > 0, "한 분기만");
                var again = EventFlow.Choose(Run(seed).Also(r => r.StageIndex = 2), Data, "EVT_SOUL_PRISON", 1);
                Assert.AreEqual(o.ForcedBattle, again.ForcedBattle, "재시작해도 같은 분기");
                CollectionAssert.AreEqual(o.CardChoices, again.CardChoices);
            }
        }

        // ---------- EVT-03 ----------

        [Test]
        public void EVT_03_SameSeedSameEvents_10000Runs_NoRepeat()
        {
            int repeats = 0;
            for (long seed = 1; seed <= 10000; seed++)
            {
                var run = Run(seed);
                var seen = new List<string>();
                foreach (var (f, n) in new[] { (1, 3), (1, 6), (2, 3), (2, 6) })
                {
                    run.Floor = f;
                    run.StageIndex = n - 1;
                    seen.Add(EventFlow.Enter(run, Data));
                }
                if (seen.Distinct().Count() < 4) repeats++;
                if (seed <= 20)
                {
                    var twin = Run(seed);
                    var twinSeen = new List<string>();
                    foreach (var (f, n) in new[] { (1, 3), (1, 6), (2, 3), (2, 6) })
                    {
                        twin.Floor = f;
                        twin.StageIndex = n - 1;
                        twinSeen.Add(EventFlow.Enter(twin, Data));
                    }
                    CollectionAssert.AreEqual(seen, twinSeen, "같은 런 시드면 같은 이벤트");
                }
            }
            Assert.AreEqual(0, repeats);
        }

        [Test]
        public void EVT_03_AfterDevilPactAtNode3_Node6Weights14of86And22of86()
        {
            var run = Run();
            run.NodeEventIds["F1-N3"] = "EVT_DEVIL_PACT";
            run.StageIndex = 5;
            var cands = EventFlow.Candidates(run, Data).ToDictionary(c => c.id, c => c.probability);
            Assert.AreEqual(5, cands.Count);
            Assert.IsFalse(cands.ContainsKey("EVT_DEVIL_PACT"));
            foreach (var id in new[] { "EVT_BLOOD_ALTAR", "EVT_CURSED_CHEST", "EVT_SOUL_PRISON" }) Assert.AreEqual(14.0 / 86, cands[id], 1e-9, id);
            foreach (var id in new[] { "EVT_WANDER_MERCHANT", "EVT_FORGOTTEN_SHRINE" }) Assert.AreEqual(22.0 / 86, cands[id], 1e-9, id);
        }
    }

    internal static class TestExtensions
    {
        public static T Also<T>(this T value, System.Action<T> action)
        {
            action(value);
            return value;
        }
    }
}
