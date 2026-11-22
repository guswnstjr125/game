using System;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 런 저장 왕복 (#47, PR #89의 1단계 RunState 변환, D-67). 기대 시드·위치는 손으로 계산한다.
    /// 노드 시드 = (RngSeed * 31 + Floor) * 31 + StageIndex (`14` §3).
    /// </summary>
    [Category("Stage1")]
    public class RunSaveTests
    {
        [Test]
        public void PROG_07_MapAndBattleEntryJsonRestoreSameRunAndBossOpening()
        {
            var data = StageOneBattleSim.Loaded();
            var run = Floor2BossEntry();
            // 시드 7, 2층, StageIndex 8: (7 * 31 + 2) * 31 + 8 = 6797
            Assert.AreEqual(6797, run.NodeSeed);
            Assert.AreEqual("F2-N9", run.CurrentNodeId);

            var map = SaveJson.Read(SaveJson.Write(RunSave.Capture(run, RunSavePhase.Map)));
            var battle = SaveJson.Read(SaveJson.Write(RunSave.Capture(run, RunSavePhase.BattleEntry)));
            Assert.AreEqual(RunSavePhase.Map, map.Phase);
            Assert.AreEqual(RunSavePhase.BattleEntry, battle.Phase);
            Assert.IsNull(map.Reward);
            Assert.IsNull(battle.Reward);

            var fromMap = RunSave.Restore(map);
            var fromBattle = RunSave.Restore(battle);
            AssertRun(fromMap);
            AssertRun(fromBattle);
            Assert.AreEqual(6797, fromBattle.NodeSeed);

            var first = Opening(run, data);
            var again = Opening(fromBattle, data);
            CollectionAssert.AreEqual(first.Hand, again.Hand);
            Assert.AreEqual("ENE_2F_BOS_001", first.BossId);
            Assert.AreEqual(first.BossId, again.BossId);
            Assert.AreEqual(first.Intent, again.Intent);
            Assert.AreEqual(first.ActionId, again.ActionId);
            Assert.AreEqual(5, first.Hand.Length);
        }

        [Test]
        public void Restore_RejectsReusedInstanceIdAndBadPosition()
        {
            var save = RunSave.Capture(Floor2BossEntry(), RunSavePhase.Map);
            save.NextCardInstanceId = save.Deck.Max(c => c.InstanceId);
            Assert.Throws<InvalidOperationException>(() => RunSave.Restore(save));

            save = RunSave.Capture(Floor2BossEntry(), RunSavePhase.Map);
            save.Deck[1].InstanceId = save.Deck[0].InstanceId;
            Assert.Throws<InvalidOperationException>(() => RunSave.Restore(save));

            save = RunSave.Capture(Floor2BossEntry(), RunSavePhase.Map);
            save.Floor = 0;
            Assert.Throws<InvalidOperationException>(() => RunSave.Restore(save));

            save = RunSave.Capture(Floor2BossEntry(), RunSavePhase.Map);
            save.StageIndex = 9;
            Assert.Throws<InvalidOperationException>(() => RunSave.Restore(save));

            save = RunSave.Capture(Floor2BossEntry(), RunSavePhase.Map);
            save.SaveVersion = 2;
            Assert.Throws<InvalidOperationException>(() => RunSave.Restore(save));
        }

        private static RunState Floor2BossEntry()
        {
            var run = RunFlow.NewRun("RUN-PROG07", 7, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" }, tutorialEnabled: true);
            run.Floor = 2;
            run.StageIndex = 8;
            run.VisitedNodeIds.Add("F1-N9");
            run.VisitedNodeIds.Add("F2-N8");
            run.NodeEventIds["F2-N3"] = "EVT_DARK_BET";
            run.CurrentHp = 55;
            run.Gold = 17;
            run.FusionCharge = 1;
            run.Deck[0].Upgraded = true;
            run.Deck[1].FromFusion = true;
            return run;
        }

        private static void AssertRun(RunState run)
        {
            Assert.AreEqual("RUN-PROG07", run.RunId);
            Assert.AreEqual(7L, run.RngSeed);
            Assert.AreEqual("NEC", run.StartClass);
            Assert.AreEqual(2, run.Floor);
            Assert.AreEqual(8, run.StageIndex);
            Assert.AreEqual("F2-N9", run.CurrentNodeId);
            Assert.AreEqual(55, run.CurrentHp);
            Assert.AreEqual(80, run.MaxHp);
            Assert.AreEqual(17, run.Gold);
            Assert.AreEqual(1, run.FusionCharge);
            Assert.AreEqual(11, run.NextCardInstanceId);
            Assert.IsTrue(run.TutorialEnabled);
            Assert.IsFalse(run.TutorialCompleted);
            Assert.IsTrue(run.Stage1Build);
            Assert.AreEqual(2, run.LastFloor);
            Assert.AreEqual(RunOutcome.InProgress, run.Outcome);
            CollectionAssert.AreEqual(new[] { "F1-N9", "F2-N8" }, run.VisitedNodeIds);
            Assert.AreEqual("EVT_DARK_BET", run.NodeEventIds["F2-N3"]);
            CollectionAssert.AreEqual(new[] { "REL_BLOOD_CHALICE" }, run.Relics);
            Assert.AreEqual(10, run.Deck.Count);
            Assert.AreEqual(1, run.Deck[0].InstanceId);
            Assert.AreEqual("NEC_MON_001", run.Deck[0].CardId);
            Assert.IsTrue(run.Deck[0].Upgraded);
            Assert.IsFalse(run.Deck[0].FromFusion);
            Assert.AreEqual(2, run.Deck[1].InstanceId);
            Assert.AreEqual("NEC_MON_002", run.Deck[1].CardId);
            Assert.IsFalse(run.Deck[1].Upgraded);
            Assert.IsTrue(run.Deck[1].FromFusion);
        }

        private static OpeningView Opening(RunState run, GameDataLoadResult data)
        {
            var ctx = RunBattle.Start(run, data);
            TurnFlow.StartPlayerTurn(ctx);
            var boss = ctx.Enemies[0];
            var action = ctx.Intents[boss][0];
            return new OpeningView
            {
                Hand = ctx.Player.Deck.Hand.Select(c => c.InstanceId + ":" + c.CardId).ToArray(),
                BossId = boss.DataId,
                ActionId = action.ActionId,
                Intent = ctx.IntentOf(boss)
            };
        }

        private struct OpeningView
        {
            public string[] Hand;
            public string BossId;
            public string ActionId;
            public IntentKind Intent;
        }
    }
}
