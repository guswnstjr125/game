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
        public void SaveJson_RoundTripsLargeRunSeedExactly_InVersionOneNumericFormat()
        {
            const long seed = 638995968123456789L;
            var run = RunFlow.NewRun("RUN-LONG-SEED", seed, "NEC", 80,
                StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            string json = SaveJson.Write(RunSave.Capture(run, RunSavePhase.Map));

            StringAssert.Contains("\"RngSeed\":638995968123456789", json);
            var restored = RunSave.Restore(SaveJson.Read(json));

            Assert.AreEqual(seed, restored.RngSeed);
        }

        [Test]
        public void SaveJson_RoundTripsPersistentRunAndShopFields()
        {
            var run = RunFlow.NewRun("RUN-EXTRA", 21, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.RelicUsedThisRun["REL_BLOOD_CHALICE"] = true;
            run.RelicCounters["REL_BLOOD_CHALICE"] = 3;
            run.FreeFusionUsed = true;
            run.FreeFusionPending = true;
            run.Consumables.Add("CON_TEST");
            run.ShopRemovalsUsed = 1;
            run.Shop = new ShopVisitDTO { PurchasedSlots = { 0, 5 }, RefreshCount = 2 };

            var restored = RunSave.Restore(SaveJson.Read(SaveJson.Write(RunSave.Capture(run, RunSavePhase.Map))));

            Assert.IsTrue(restored.RelicUsedThisRun["REL_BLOOD_CHALICE"]);
            Assert.AreEqual(3, restored.RelicCounters["REL_BLOOD_CHALICE"]);
            Assert.IsTrue(restored.FreeFusionUsed);
            Assert.IsTrue(restored.FreeFusionPending);
            CollectionAssert.AreEqual(new[] { "CON_TEST" }, restored.Consumables);
            Assert.AreEqual(1, restored.ShopRemovalsUsed);
            CollectionAssert.AreEqual(new[] { 0, 5 }, restored.Shop.PurchasedSlots);
            Assert.AreEqual(2, restored.Shop.RefreshCount);
        }

        [Test]
        public void SaveJson_ReadsExistingVersionOneNumericSeed()
        {
            var run = RunSave.Restore(SaveJson.Read(VersionOneSaveJson(7)));

            Assert.AreEqual(7L, run.RngSeed);
            Assert.AreEqual("F1-N1", run.CurrentNodeId);
        }

        [Test]
        public void RestPending_RestoresAppliedHealingWithoutApplyingItAgainAndCompletesOnce()
        {
            var data = StageOneBattleSim.Loaded();
            var run = RunFlow.NewRun("RUN-REST", 19, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.StageIndex = 7;
            run.CurrentHp = 50;
            int before = run.CurrentHp;
            Assert.IsTrue(RestFlow.Heal(run, data));
            var result = new RestScreenResult { NodeId = run.CurrentNodeId, Healed = true, HpBefore = before, HpAfter = run.CurrentHp, MaxHp = run.MaxHp };
            var saved = SaveJson.Read(SaveJson.Write(RunSave.CaptureRest(run, result)));
            var restored = RunSave.Restore(saved);
            int restoredHp = restored.CurrentHp;
            int completions = 0;
            var session = RestScreenSession.RestorePending(restored, data, saved.Rest, _ => { completions++; RunFlow.CompleteNode(restored); });

            Assert.AreEqual(RestScreenPhase.Result, session.Phase);
            Assert.AreEqual(restoredHp, restored.CurrentHp);
            Assert.IsTrue(session.Continue());
            Assert.IsFalse(session.Continue());
            Assert.AreEqual(restoredHp, restored.CurrentHp);
            Assert.AreEqual(1, completions);
            Assert.AreEqual(1, restored.VisitedNodeIds.Count(id => id == "F1-N8"));
            Assert.AreEqual(8, restored.StageIndex);
        }

        [Test]
        public void RestPending_RestoresAppliedUpgradeWithoutUpgradingAnotherTime()
        {
            var data = StageOneBattleSim.Loaded();
            var run = RunFlow.NewRun("RUN-REST-UPGRADE", 20, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.StageIndex = 7;
            var card = run.Deck.First(c => RunDeckRules.IsUpgradable(c, data.Cards));
            Assert.IsTrue(RestFlow.Upgrade(run, data, card.InstanceId));
            var result = new RestScreenResult { NodeId = run.CurrentNodeId, Upgraded = true, HpBefore = run.CurrentHp, HpAfter = run.CurrentHp, MaxHp = run.MaxHp, UpgradedInstanceId = card.InstanceId, UpgradedCardId = card.CardId };
            var save = SaveJson.Read(SaveJson.Write(RunSave.CaptureRest(run, result)));
            var restored = RunSave.Restore(save);
            var before = restored.Deck.Select(c => c.Upgraded).ToArray();
            int complete = 0;
            var session = RestScreenSession.RestorePending(restored, data, save.Rest, _ => { complete++; RunFlow.CompleteNode(restored); });

            Assert.IsTrue(session.Result.Upgraded);
            Assert.IsTrue(restored.Deck.Single(c => c.InstanceId == card.InstanceId).Upgraded);
            Assert.IsTrue(session.Continue());
            CollectionAssert.AreEqual(before, restored.Deck.Select(c => c.Upgraded).ToArray());
            Assert.AreEqual(1, complete);
        }

        [Test]
        public void RestSaveAndContinue_PersistResultThenAdvanceNodeOnce()
        {
            var data = StageOneBattleSim.Loaded();
            var run = RunFlow.NewRun("RUN-REST-STORE", 24, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.StageIndex = 7;
            run.CurrentHp = 50;
            int before = run.CurrentHp;
            Assert.IsTrue(RestFlow.Heal(run, data));
            var result = new RestScreenResult { NodeId = run.CurrentNodeId, Healed = true, HpBefore = before, HpAfter = run.CurrentHp, MaxHp = run.MaxHp };
            var store = new SaveFileStore("save", new MemorySaveFileSystem());

            Assert.IsTrue(store.SaveRestResult(run, result));
            Assert.AreEqual(RunSavePhase.RestPending, store.LoadRun().Phase);
            Assert.IsTrue(store.ContinueRestAndPersist(store.LoadRun()));
            var next = store.LoadRun();
            var restored = RunSave.Restore(next);
            Assert.AreEqual(RunSavePhase.Map, next.Phase);
            Assert.AreEqual("F1-N9", restored.CurrentNodeId);
            Assert.AreEqual(1, restored.VisitedNodeIds.Count(id => id == "F1-N8"));
            Assert.AreEqual(result.HpAfter, restored.CurrentHp);
        }

        static string VersionOneSaveJson(long seed) =>
            "{\"SaveVersion\":1,\"RunId\":\"RUN-V1\",\"TutorialEnabled\":false,\"TutorialCompleted\":false,\"Phase\":\"Map\",\"Reward\":null,\"Player\":{\"CurrentHp\":80,\"MaxHp\":80},\"Deck\":[{\"InstanceId\":1,\"CardId\":\"NEC_MON_001\",\"UpgradeLevel\":0,\"FromFusion\":false}],\"NextCardInstanceId\":2,\"Map\":{\"CurrentNodeId\":\"F1-N1\",\"VisitedNodeIds\":[],\"NodeEventIds\":{}},\"Relics\":[],\"RngSeed\":" + seed + ",\"StartClass\":\"NEC\",\"Floor\":1,\"StageIndex\":0,\"Gold\":0,\"FusionCharge\":0}";

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

        [Test]
        public void SaveJson_RejectsCorruptJsonAndUnknownVersion()
        {
            Assert.Throws<FormatException>(() => SaveJson.Read("{broken"));
            string json = VersionOneSaveJson(7).Replace("\"SaveVersion\":1", "\"SaveVersion\":99");
            Assert.Throws<InvalidOperationException>(() => RunSave.Restore(SaveJson.Read(json)));
            string exponentSeed = VersionOneSaveJson(7).Replace("\"RngSeed\":7", "\"RngSeed\":7e0");
            Assert.Throws<InvalidOperationException>(() => RunSave.Restore(SaveJson.Read(exponentSeed)));
        }

        [Test]
        public void SaveFileStore_DiscardsUnsupportedRunVersion()
        {
            var files = new MemorySaveFileSystem();
            var store = new SaveFileStore("save", files);
            files.WriteAllText(System.IO.Path.Combine("save", "save_slot_0.json"), "{\"SaveVersion\":2}");

            var error = Assert.Throws<UnsupportedRunSaveVersionException>(() => store.LoadRun());
            Assert.AreEqual(2, error.Version);
            Assert.IsFalse(files.Exists(System.IO.Path.Combine("save", "save_slot_0.json")));
        }

        [Test]
        public void MetaSaveJson_MigratesLowerVersionAndRejectsHigherVersion()
        {
            var migrated = MetaSaveJson.Read("{\"SaveVersion\":0,\"MetaSoul\":5,\"UnlockedClasses\":[\"NEC\"]}");
            Assert.AreEqual(1, migrated.SaveVersion);
            Assert.AreEqual(5, migrated.MetaSoul);
            CollectionAssert.AreEqual(new[] { "NEC" }, migrated.UnlockedClasses);
            Assert.Throws<InvalidOperationException>(() => MetaSaveJson.Read("{\"SaveVersion\":2}"));
        }

        [Test]
        public void SaveFileStore_ReplaceFailurePreservesPreviousValidSlot()
        {
            var files = new MemorySaveFileSystem();
            var store = new SaveFileStore("save", files);
            var first = RunSave.Capture(RunFlow.NewRun("RUN-OLD", 3, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" }), RunSavePhase.Map);
            Assert.IsTrue(store.SaveRun(first));
            var oldJson = files.ReadAllText(System.IO.Path.Combine("save", "save_slot_0.json"));
            files.FailReplace = true;
            first.Gold = 27;

            Assert.IsFalse(store.SaveRun(first));
            Assert.AreEqual(oldJson, files.ReadAllText(System.IO.Path.Combine("save", "save_slot_0.json")));
            Assert.AreEqual("RUN-OLD", store.LoadRun().RunId);
        }

        [Test]
        public void RewardPending_RoundTripsCandidatesAndPreventsDuplicateConfirmation()
        {
            var run = RunFlow.NewRun("RUN-REWARD", 9, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            var pending = new PendingReward { NodeId = run.CurrentNodeId, Source = RewardSource.Battle, Gold = 12, CardChoices = { "NEC_MON_004", "NEC_MON_006" } };
            var save = SaveJson.Read(SaveJson.Write(RunSave.CaptureRewardPending(run, pending, selectedCard: 1)));
            var restored = RunSave.Restore(save);
            var reward = RunSave.RestoreReward(save.Reward);

            Assert.AreEqual(RunSavePhase.RewardPending, save.Phase);
            Assert.AreEqual(1, save.Reward.SelectedCard);
            Assert.IsTrue(RewardFlow.Confirm(restored, reward, save.Reward.SelectedCard, null));
            Assert.IsFalse(RewardFlow.Confirm(restored, reward, save.Reward.SelectedCard, null));
            Assert.AreEqual(12, restored.Gold);
            Assert.AreEqual(11, restored.Deck.Count);
        }

        [Test]
        public void RewardConfirmation_PersistsPayoutAndNodeAdvanceAsOneRunTransition()
        {
            var run = RunFlow.NewRun("RUN-REWARD-STORE", 22, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            var reward = new PendingReward { NodeId = run.CurrentNodeId, Source = RewardSource.Battle, Gold = 12, CardChoices = { "NEC_MON_004" } };
            var pending = RunSave.CaptureRewardPending(run, reward);
            var store = new SaveFileStore("save", new MemorySaveFileSystem());

            Assert.IsTrue(store.SaveRun(pending));
            Assert.IsTrue(store.ConfirmRewardAndPersist(pending, 0, null));
            Assert.IsFalse(store.ConfirmRewardAndPersist(pending, null, null));
            var next = store.LoadRun();
            var restored = RunSave.Restore(next);
            Assert.AreEqual(RunSavePhase.Map, next.Phase);
            Assert.AreEqual("F1-N2", restored.CurrentNodeId);
            Assert.IsTrue(restored.VisitedNodeIds.Contains("F1-N1"));
            Assert.AreEqual(12, restored.Gold);
            Assert.AreEqual(11, restored.Deck.Count);
        }

        [Test]
        public void RewardConfirmation_WriteFailureLeavesPendingRewardForSafeRetry()
        {
            var run = RunFlow.NewRun("RUN-REWARD-FAIL", 23, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            var pending = RunSave.CaptureRewardPending(run, new PendingReward { NodeId = run.CurrentNodeId, Source = RewardSource.Battle, Gold = 7 });
            var files = new MemorySaveFileSystem();
            var store = new SaveFileStore("save", files);
            Assert.IsTrue(store.SaveRun(pending));
            string committed = files.ReadAllText(System.IO.Path.Combine("save", "save_slot_0.json"));
            files.FailReplace = true;

            Assert.IsFalse(store.ConfirmRewardAndPersist(pending, null, null));
            Assert.AreEqual(committed, files.ReadAllText(System.IO.Path.Combine("save", "save_slot_0.json")));
            Assert.AreEqual(RunSavePhase.RewardPending, store.LoadRun().Phase);
        }

        [Test]
        public void SettlementPending_RoundTripsAndMetaPayoutIsIdempotent()
        {
            var run = RunFlow.NewRun("RUN-SETTLE", 10, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            RunFlow.Forfeit(run);
            var settlement = new SettlementPendingDTO { RunId = run.RunId, Outcome = run.Outcome, ReachedFloor = 1, ReachedStageIndex = 3, MetaSoul = 9, Stats = new MetaStatsDTO { TotalFusion = 2, FloorClearCount = new[] { 1, 0, 0, 0, 0 } }, NewAchievements = { "ACH_TEST" } };
            var save = SaveJson.Read(SaveJson.Write(RunSave.CaptureSettlement(run, settlement)));
            Assert.AreEqual(RunOutcome.Forfeit, RunSave.Restore(save).Outcome);
            var files = new MemorySaveFileSystem();
            var store = new SaveFileStore("save", files);

            Assert.IsTrue(store.ApplySettlement(save.Settlement));
            Assert.IsTrue(store.ApplySettlement(save.Settlement));
            var meta = store.LoadMeta();
            Assert.AreEqual(9, meta.MetaSoul);
            Assert.AreEqual(2, meta.Stats.TotalFusion);
            Assert.AreEqual(1, meta.Stats.FloorClearCount[0]);
            Assert.IsTrue(meta.AchievementFlags["ACH_TEST"]);
            Assert.AreEqual("RUN-SETTLE", meta.LastSettledRunId);
        }

        [Test]
        public void TutorialCompletion_ReconcilesMetaOnceFromSavedRun()
        {
            var run = RunFlow.NewRun("RUN-TUTORIAL-SAVE", 26, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" }, tutorialEnabled: true);
            run.TutorialCompleted = true;
            var save = RunSave.Capture(run, RunSavePhase.Map);
            var store = new SaveFileStore("save", new MemorySaveFileSystem());

            Assert.IsTrue(store.SaveRun(save));
            Assert.IsTrue(store.LoadRun().TutorialCompleted);
            Assert.IsTrue(store.LoadRun().TutorialCompleted);
            Assert.IsTrue(store.LoadMeta().TutorialDone);
        }

        [Test]
        public void EventResult_RestoresAppliedOutcomeWithoutApplyingAgain()
        {
            var data = StageOneBattleSim.Loaded();
            var run = RunFlow.NewRun("RUN-EVENT", 11, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.StageIndex = 2;
            run.CurrentHp = 60;
            var session = new EventScreenSession(run.Floor, "EVT_FORGOTTEN_SHRINE", run, data);
            Assert.IsTrue(session.Choose(1));
            Assert.AreEqual(75, run.CurrentHp);
            var save = SaveJson.Read(SaveJson.Write(RunSave.CaptureEventPending(run, session.CapturePending())));
            var restoredRun = RunSave.Restore(save);
            int hp = restoredRun.CurrentHp;
            int continued = 0;
            var restoredSession = EventScreenSession.RestorePending(restoredRun, data, save.Reward.Event, _ => { continued++; RunFlow.CompleteNode(restoredRun); });

            Assert.AreEqual(EventScreenPhase.Result, restoredSession.Phase);
            Assert.AreEqual(75, restoredSession.Result.HpAfter);
            Assert.AreEqual(hp, restoredRun.CurrentHp);
            Assert.IsTrue(restoredSession.Continue());
            Assert.IsFalse(restoredSession.Continue());
            Assert.AreEqual(1, continued);
            Assert.AreEqual(1, restoredRun.VisitedNodeIds.Count(id => id == "F1-N3"));
        }

        [Test]
        public void EventPicking_RestoresChoiceAndAppliesCostOnlyOnFinalConfirmation()
        {
            var data = StageOneBattleSim.Loaded();
            var run = RunFlow.NewRun("RUN-EVENT-PICK", 12, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.StageIndex = 2;
            run.Gold = 9;
            var session = new EventScreenSession(run.Floor, "EVT_WANDER_MERCHANT", run, data);
            Assert.IsTrue(session.Choose(1));
            Assert.AreEqual(9, run.Gold, "선택 중에는 비용을 적용하지 않는다");
            Assert.IsTrue(session.SelectCard(1));
            var save = SaveJson.Read(SaveJson.Write(RunSave.CaptureEventPending(run, session.CapturePending())));
            var restored = RunSave.Restore(save);
            var pending = EventScreenSession.RestorePending(restored, data, save.Reward.Event);

            Assert.AreEqual(EventScreenPhase.Picking, pending.Phase);
            Assert.IsTrue(pending.CanConfirm);
            Assert.IsTrue(pending.Confirm());
            Assert.AreEqual(1, restored.Gold);
            Assert.AreEqual(11, restored.Deck.Count);
            Assert.IsFalse(pending.Confirm());
            Assert.AreEqual(1, restored.Gold);
            Assert.AreEqual(11, restored.Deck.Count);
        }

        [Test]
        public void ForcedEventBattleEntry_RestoresEventDecisionAndAlreadyAppliedRunState()
        {
            var run = RunFlow.NewRun("RUN-FORCED", 13, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.StageIndex = 2;
            run.CurrentHp = 68;
            run.NodeEventIds[run.CurrentNodeId] = "EVT_SOUL_PRISON";
            var pending = new EventPendingDTO { NodeId = run.CurrentNodeId, EventId = "EVT_SOUL_PRISON", Phase = EventScreenPhase.Result,
                Outcome = new EventOutcome { NodeId = run.CurrentNodeId, EventId = "EVT_SOUL_PRISON", Option = 1, HpDelta = -12, ForcedBattle = true, Confirmed = true },
                Choice = new EventChoice(), Result = new EventScreenResult { NodeId = run.CurrentNodeId, EventId = "EVT_SOUL_PRISON", Option = 1, HpBefore = 80, HpAfter = 68, MaxHpBefore = 80, MaxHpAfter = 80, ForcedBattle = true, RunOutcome = RunOutcome.InProgress } };
            var save = SaveJson.Read(SaveJson.Write(RunSave.CaptureForcedBattleEntry(run, pending)));
            var restored = RunSave.Restore(save);

            Assert.AreEqual(RunSavePhase.BattleEntry, save.Phase);
            Assert.AreEqual(68, restored.CurrentHp);
            Assert.IsTrue(save.BattleEntryEvent.Outcome.ForcedBattle);
            Assert.AreEqual(-12, save.BattleEntryEvent.Outcome.HpDelta);
            Assert.AreEqual(68, save.BattleEntryEvent.Result.HpAfter);
        }

        [Test]
        public void EventStore_ContinueAtomicallyPersistsForcedBattleEntry()
        {
            var run = RunFlow.NewRun("RUN-FORCED-STORE", 25, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.StageIndex = 2;
            run.NodeEventIds[run.CurrentNodeId] = "EVT_SOUL_PRISON";
            var pending = new EventPendingDTO { NodeId = run.CurrentNodeId, EventId = "EVT_SOUL_PRISON", Phase = EventScreenPhase.Result,
                Outcome = new EventOutcome { NodeId = run.CurrentNodeId, EventId = "EVT_SOUL_PRISON", Option = 1, ForcedBattle = true, Confirmed = true },
                Choice = new EventChoice(), Result = new EventScreenResult { NodeId = run.CurrentNodeId, EventId = "EVT_SOUL_PRISON", Option = 1, Text = "전투", HpBefore = 80, HpAfter = 80, MaxHpBefore = 80, MaxHpAfter = 80, ForcedBattle = true, RunOutcome = RunOutcome.InProgress } };
            var store = new SaveFileStore("save", new MemorySaveFileSystem());

            Assert.IsTrue(store.SaveEventResult(run, pending));
            Assert.IsTrue(store.ContinueEventAndPersist(store.LoadRun()));
            var save = store.LoadRun();
            Assert.AreEqual(RunSavePhase.BattleEntry, save.Phase);
            Assert.IsTrue(save.BattleEntryEvent.Outcome.ForcedBattle);
            Assert.AreEqual("F1-N3", RunSave.Restore(save).CurrentNodeId);
        }

        [Test]
        public void DeleteSettledRun_PreservesSlotUntilMetaPayoutIsCommitted()
        {
            var store = new SaveFileStore("save", new MemorySaveFileSystem());
            Assert.IsFalse(store.HasRunSlot);
            var run = Floor2BossEntry();
            RunFlow.Forfeit(run);
            var settlement = new SettlementPendingDTO { RunId = run.RunId, Outcome = run.Outcome };
            Assert.IsTrue(store.SaveRun(RunSave.CaptureSettlement(run, settlement)));
            Assert.Throws<InvalidOperationException>(() => store.DeleteSettledRun());
            Assert.IsTrue(store.HasRunSlot);

            Assert.IsTrue(store.ApplySettlement(settlement));
            store.DeleteSettledRun();
            Assert.IsFalse(store.HasRunSlot);
            Assert.AreEqual(run.RunId, store.LoadMeta().LastSettledRunId);
        }

        [Test]
        public void ReplaceActiveRun_PreservesSlotOnFailureAndRejectsRepeatedConfirmation()
        {
            var files = new MemorySaveFileSystem();
            var store = new SaveFileStore("save", files);
            var previous = RunSave.Capture(Floor2BossEntry(), RunSavePhase.Map);
            var newRun = RunFlow.NewRun("NEW-RUN", 99, "NEC", 80, StageOneBattleSim.NecStartDeck, Array.Empty<string>());
            var next = RunSave.Capture(newRun, RunSavePhase.Map);
            Assert.IsTrue(store.SaveRun(previous));
            Assert.IsTrue(store.SaveMeta(new MetaSaveDTO { MetaSoul = 17 }));
            files.FailReplace = true;

            Assert.IsFalse(store.ReplaceActiveRun(previous, next));
            Assert.AreEqual(previous.RunId, store.LoadRun().RunId);
            files.FailReplace = false;
            Assert.IsTrue(store.ReplaceActiveRun(previous, next));
            Assert.IsFalse(store.ReplaceActiveRun(previous, next));
            Assert.AreEqual(next.RunId, store.LoadRun().RunId);
            Assert.AreEqual(17, store.LoadMeta().MetaSoul);
            Assert.IsNull(store.LoadMeta().LastSettledRunId);
        }

        private sealed class MemorySaveFileSystem : ISaveFileSystem
        {
            readonly System.Collections.Generic.Dictionary<string, string> _files = new System.Collections.Generic.Dictionary<string, string>();
            public bool FailReplace;
            public void CreateDirectory(string path) { }
            public bool Exists(string path) => _files.ContainsKey(path);
            public string ReadAllText(string path) => _files[path];
            public void WriteAllText(string path, string contents) => _files[path] = contents;
            public void Move(string source, string destination) { _files[destination] = _files[source]; _files.Remove(source); }
            public void Replace(string source, string destination) { if (FailReplace) throw new System.IO.IOException("simulated replace failure"); _files[destination] = _files[source]; _files.Remove(source); }
            public void Delete(string path) => _files.Remove(path);
            public string[] GetFiles(string path, string pattern) => _files.Keys.Where(k => k.EndsWith(".tmp", StringComparison.Ordinal)).ToArray();
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
