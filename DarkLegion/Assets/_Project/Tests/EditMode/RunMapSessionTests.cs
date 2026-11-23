using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 18노드 맵과 전투 결과·보상 선택 화면의 값 (`37` §1·§2, `03` §2, D-16·D-37·D-66·D-67·D-73·D-75).
    /// 기대값은 노드 템플릿(`RunMap.KindOf`, 1단계 빌드)과 NEC 시작 값(HP 80, 덱 10장, 피의 잔),
    /// `Reward_EliteRelic` 1층 후보(A·B·C)에서 손으로 계산했다.
    /// </summary>
    [Category("Stage1")]
    public class RunMapSessionTests
    {
        static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        static RunMapSession NewMap(bool tutorial = false) => RunMapSession.NewNecRun(Data, 20260601L, tutorial);

        static BattleResultView Win(RunMapSession map)
        {
            var session = map.EnterBattle();
            Assert.AreEqual(BattleOutcome.Victory, session.Context.EvaluateOutcome(session.Context.Player.CurrentHP, 0));
            return map.Result();
        }

        [Test]
        public void NewRun_ShowsEighteenNodes_FirstOneCurrent()
        {
            var snap = NewMap().Capture();

            Assert.AreEqual(18, snap.Nodes.Length, "2층 × 9노드");
            Assert.AreEqual(1, snap.Floor);
            Assert.AreEqual(1, snap.Node);
            Assert.AreEqual(80, snap.Hp);
            Assert.AreEqual(80, snap.MaxHp);
            Assert.AreEqual(0, snap.Gold);
            Assert.AreEqual(10, snap.DeckCount, "NEC 시작 덱 10장");
            CollectionAssert.AreEqual(new[] { "REL_BLOOD_CHALICE" }, snap.Relics);
            Assert.AreEqual(RunOutcome.InProgress, snap.Outcome);

            // 노드 템플릿: 전투 1·2, 이벤트 3, 전투 4, 엘리트 5, 이벤트 6, 전투 7, 휴식 8, 보스 9 (두 층 같음)
            var kinds = new[]
            {
                NodeKind.Battle, NodeKind.Battle, NodeKind.Event, NodeKind.Battle, NodeKind.Elite,
                NodeKind.Event, NodeKind.Battle, NodeKind.Rest, NodeKind.Boss
            };
            for (int f = 0; f < 2; f++)
                for (int n = 0; n < 9; n++)
                    Assert.AreEqual(kinds[n], snap.Nodes[f * 9 + n].Kind, "F" + (f + 1) + "-N" + (n + 1));

            Assert.AreEqual("F1-N1", snap.Nodes[0].Id);
            Assert.AreEqual("F2-N9", snap.Nodes[17].Id);
            Assert.AreEqual(MapNodeState.Current, snap.Nodes[0].State);
            Assert.IsTrue(snap.Nodes[0].Enterable);
            Assert.IsTrue(snap.Nodes.Skip(1).All(x => x.State == MapNodeState.Upcoming && !x.Enterable));
        }

        [Test]
        public void CurrentEvent_IsEnterable_ButCannotOpenAsBattle()
        {
            var map = NewMap();
            map.Run.StageIndex = 2; // F1-N3 이벤트

            var snap = map.Capture();
            Assert.AreEqual(MapNodeState.Current, snap.Nodes[2].State);
            Assert.AreEqual(NodeKind.Event, snap.Nodes[2].Kind);
            Assert.IsTrue(snap.Nodes[2].Enterable);
            Assert.IsTrue(map.CanEnter(1, 3));
            Assert.IsFalse(map.CanEnter(1, 4), "현재 노드가 아니면 들어갈 수 없다");
            Assert.Throws<System.InvalidOperationException>(() => map.EnterBattle());
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[0].State);
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[1].State);
        }

        [TestCase(3, 4)]
        [TestCase(6, 7)]
        public void EventContinue_CompletesNodeAndAddsChargeOnce(int node, int next)
        {
            var map = NewMap();
            map.Run.StageIndex = node - 1;
            int callbacks = 0;
            var screen = map.EnterEvent(forced =>
            {
                Assert.IsNull(forced);
                callbacks++;
            });

            Assert.AreEqual(map.Run.NodeEventIds[map.Run.CurrentNodeId], screen.EventId);
            Assert.IsFalse(map.CanEnter(1, node));
            Assert.Throws<System.InvalidOperationException>(() => map.EnterEvent(_ => { }));
            Assert.IsTrue(screen.Choose(3));
            Assert.AreEqual(node, map.Run.Node);
            Assert.AreEqual(0, map.Run.FusionCharge);
            Assert.IsTrue(screen.Continue());
            Assert.IsFalse(screen.Continue());
            Assert.AreEqual((next, 1, 1), (map.Run.Node, map.Run.FusionCharge, callbacks));
            Assert.IsTrue(map.CanEnter(1, next));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ForcedEventBattle_OnlyVictoryCanConfirmNormalReward(bool win)
        {
            var map = NewMap();
            map.Run.StageIndex = 2;
            SetEventLossBranch(map, "EVT_SOUL_PRISON");
            BattleScreenSession battle = null;
            var screen = map.EnterEvent(session => battle = session);
            Assert.IsTrue(screen.Choose(1));
            Assert.IsTrue(screen.Result.ForcedBattle);
            Assert.IsTrue(screen.Continue());
            Assert.IsFalse(screen.Continue());
            Assert.NotNull(battle);
            Assert.AreSame(map.Run, battle.Run);
            Assert.IsFalse(battle.Context.IsTutorial);
            Assert.AreEqual(2, battle.Context.Enemies.Count, "1층 노드 1 편성");
            Assert.AreEqual((3, 0, 0), (map.Run.Node, map.Run.Gold, map.Run.FusionCharge));

            if (!win) battle.Context.Player.CurrentHP = 0;
            battle.Context.EvaluateOutcome(battle.Context.Player.CurrentHP, win ? 0 : 2);
            var result = map.Result();
            Assert.AreEqual("F1-N3", result.NodeId);
            Assert.AreEqual(NodeKind.Battle, result.Kind);
            Assert.AreEqual(win, result.CanConfirm);
            Assert.AreEqual(win ? 3 : 0, result.CardChoices.Length);
            Assert.IsFalse(result.NeedsRelicChoice);
            if (win)
            {
                Assert.That(result.RewardGold, Is.InRange(5, 10));
                Assert.IsTrue(map.ConfirmReward(0, null));
                Assert.IsFalse(map.ConfirmReward(0, null));
                Assert.AreEqual((4, result.RewardGold, 1, 11), (map.Run.Node, map.Run.Gold, map.Run.FusionCharge, map.Run.Deck.Count));
            }
            else
            {
                Assert.IsFalse(map.ConfirmReward(null, null));
                Assert.AreEqual((RunOutcome.Defeat, 3, 0, 0), (map.Run.Outcome, map.Run.Node, map.Run.Gold, map.Run.FusionCharge));
                CollectionAssert.IsEmpty(map.Run.VisitedNodeIds);
            }
        }

        [Test]
        public void ForcedBattle_LethalOpeningDraw_PreservesEntryHpInResult()
        {
            var map = NewMap();
            map.Run.StageIndex = 2;
            map.Run.CurrentHp = 3;
            map.Run.Deck.Clear();
            map.Run.AddCard("CUR_004");
            SetEventLossBranch(map, "EVT_SOUL_PRISON");
            BattleScreenSession battle = null;
            var screen = map.EnterEvent(session => battle = session);

            Assert.IsTrue(screen.Choose(1));
            Assert.IsTrue(screen.Continue());
            Assert.NotNull(battle);
            var result = map.Result();
            Assert.AreEqual((BattleOutcome.Defeat, 3, 0), (result.Battle, result.HpBefore, result.HpAfter));
            Assert.AreEqual((3, 0), (map.Run.Node, map.Run.FusionCharge));
        }

        [Test]
        public void FatalEvent_DoesNotCompleteNodeOrStartBattle()
        {
            var map = NewMap();
            map.Run.Floor = 2;
            map.Run.StageIndex = 5;
            map.Run.CurrentHp = 12;
            SetEventLossBranch(map, "EVT_DARK_WAGER");
            int callbacks = 0;
            var screen = map.EnterEvent(battle =>
            {
                Assert.IsNull(battle);
                callbacks++;
            });

            Assert.IsTrue(screen.Choose(1));
            Assert.IsTrue(screen.Continue());
            Assert.IsFalse(screen.Continue());
            Assert.AreEqual((RunOutcome.Defeat, 0, 6, 0, 1), (map.Run.Outcome, map.Run.CurrentHp, map.Run.Node, map.Run.FusionCharge, callbacks));
            CollectionAssert.IsEmpty(map.Run.VisitedNodeIds);
        }

        [TestCase("heal")]
        [TestCase("upgrade")]
        [TestCase("continue")]
        public void RestContinue_AdvancesOnce_AfterAvailableOption(string choice)
        {
            var map = NewMap();
            map.Run.StageIndex = 7;
            if (choice == "heal") map.Run.CurrentHp = 50;
            if (choice == "continue")
                foreach (var card in map.Run.Deck) card.Upgraded = true;
            int callbacks = 0;
            var screen = map.EnterRest(() => callbacks++);
            Assert.IsFalse(map.CanEnter(1, 8));
            Assert.Throws<System.InvalidOperationException>(() => map.EnterRest());
            if (choice == "heal") Assert.IsTrue(screen.ChooseHeal());
            if (choice == "upgrade")
            {
                Assert.IsTrue(screen.ChooseUpgrade());
                Assert.IsTrue(screen.SelectUpgrade(screen.Capture().UpgradeCandidates[0].InstanceId));
            }
            if (choice != "continue")
            {
                Assert.IsTrue(screen.Confirm());
                Assert.IsFalse(screen.Confirm());
            }
            else Assert.IsTrue(screen.Capture().ContinueOnly);
            Assert.AreEqual(8, map.Run.Node);
            Assert.IsTrue(screen.Continue());
            Assert.IsFalse(screen.Continue());
            Assert.AreEqual((9, 1), (map.Run.Node, callbacks));
            Assert.AreEqual(choice == "heal" ? 74 : 80, map.Run.CurrentHp);
            Assert.AreEqual(choice == "continue" ? 10 : choice == "upgrade" ? 1 : 0, map.Run.Deck.Count(c => c.Upgraded));
            Assert.IsTrue(map.CanEnter(1, 9));
        }

        static void SetEventLossBranch(RunMapSession map, string eventId)
        {
            map.Run.NodeEventIds[map.Run.CurrentNodeId] = eventId;
            for (long seed = 1; seed <= 40; seed++)
            {
                map.Run.RngSeed = seed;
                var rng = new BattleRandom(map.Run.NodeSeed);
                rng.NextFloat();
                if (!rng.Chance(0.5)) return;
            }
            Assert.Fail("50% 두 번째 분기의 테스트 시드를 찾지 못했습니다");
        }

        [Test]
        public void SecondFloor_CurrentNodeIsInSecondRow()
        {
            var map = NewMap();
            map.Run.Floor = 2;
            map.Run.StageIndex = 4; // F2-N5 엘리트

            var snap = map.Capture();
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[0].State, "1층은 모두 지나갔다");
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[8].State);
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[9 + 3].State, "F2-N4");
            Assert.AreEqual(MapNodeState.Current, snap.Nodes[9 + 4].State);
            Assert.IsTrue(map.CanEnter(2, 5), "엘리트 전투는 들어갈 수 있다");
            Assert.AreEqual(MapNodeState.Upcoming, snap.Nodes[9 + 5].State);
        }

        [Test]
        public void EnterBattle_UsesRunStateAndNodeSeed()
        {
            var map = NewMap();
            var session = map.EnterBattle();

            Assert.AreSame(map.Run, session.Run);
            Assert.AreEqual(1, session.Capture().Floor);
            Assert.AreEqual(1, session.Capture().Node);
            Assert.AreEqual(80, session.Capture().Hp);
            Assert.AreEqual(BattleOutcome.None, session.Context.Outcome);
            Assert.Throws<System.InvalidOperationException>(() => map.EnterBattle(), "전투가 끝나기 전에 다시 들어갈 수 없다");
        }

        [Test]
        public void Victory_ShowsRewardChoices_WithoutAdvancing()
        {
            var map = NewMap();
            var result = Win(map);

            Assert.AreEqual(BattleOutcome.Victory, result.Battle);
            Assert.AreEqual("F1-N1", result.NodeId);
            Assert.AreEqual(NodeKind.Battle, result.Kind);
            Assert.AreEqual(80, result.HpBefore);
            Assert.AreEqual(80, result.HpAfter);
            Assert.IsTrue(result.CanConfirm);
            Assert.IsFalse(result.NeedsRelicChoice);
            Assert.AreEqual(3, result.CardChoices.Length, "카드 3택1 (D-37)");
            var expected = RewardFlow.ForBattle(map.Run, NodeKind.Battle, Data);
            CollectionAssert.AreEqual(expected.CardChoices, result.CardChoices, "보상 난수는 노드 시드에서 같은 값 (D-75)");
            Assert.AreEqual(expected.Gold, result.RewardGold);
            var row = Data.Rewards.Row(NodeKind.Battle, 1);
            Assert.That(result.RewardGold, Is.InRange(row.GoldMin, row.GoldMax));
            Assert.AreEqual(1, map.Run.Node, "결과를 보는 동안 노드는 그대로");
            Assert.AreEqual(0, map.Run.Gold, "확정 전에는 골드도 그대로");
            Assert.AreEqual(10, map.Run.Deck.Count);
            CollectionAssert.AreEqual(result.CardChoices, map.Result().CardChoices, "다시 읽어도 같은 후보");
        }

        [Test]
        public void ConfirmCard_AddsChosenCardAndGold_AdvancesOnce()
        {
            var map = NewMap();
            var result = Win(map);

            Assert.IsTrue(map.ConfirmReward(1, null));
            var snap = map.Capture();
            Assert.AreEqual(2, snap.Node);
            Assert.AreEqual(result.RewardGold, snap.Gold);
            Assert.AreEqual(11, snap.DeckCount, "10장 + 고른 카드 1장");
            Assert.AreEqual(result.CardChoices[1], map.Run.Deck.Last().CardId, "고른 카드가 덱 끝에 들어간다");
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[0].State);
            Assert.AreEqual(MapNodeState.Current, snap.Nodes[1].State);

            Assert.IsFalse(map.ConfirmReward(1, null), "두 번째 확정은 아무것도 바꾸지 않는다");
            Assert.AreEqual(2, map.Capture().Node);
            Assert.AreEqual(result.RewardGold, map.Capture().Gold, "골드 중복 지급 없음");
            Assert.AreEqual(11, map.Capture().DeckCount);
        }

        [Test]
        public void ConfirmSkip_TakesGoldOnly()
        {
            var map = NewMap();
            var result = Win(map);

            Assert.IsTrue(map.ConfirmReward(null, null));
            Assert.AreEqual(10, map.Capture().DeckCount);
            Assert.AreEqual(result.RewardGold, map.Capture().Gold);
            Assert.AreEqual(2, map.Capture().Node);
        }

        [Test]
        public void ConfirmReward_WithBadInput_ChangesNothingAndCanRetry()
        {
            var map = NewMap();
            var result = Win(map);

            Assert.IsFalse(map.ConfirmReward(3, null), "카드 후보는 0~2");
            Assert.IsFalse(map.ConfirmReward(-1, null));
            Assert.IsFalse(map.ConfirmReward(null, 0), "유물 후보가 없는 보상에 유물을 고를 수 없다");
            Assert.AreEqual(1, map.Capture().Node);
            Assert.AreEqual(0, map.Capture().Gold);
            Assert.AreEqual(10, map.Capture().DeckCount);

            Assert.IsTrue(map.ConfirmReward(0, null), "잘못된 입력 뒤에도 바로 확정할 수 있다");
            Assert.AreEqual(2, map.Capture().Node);
            Assert.AreEqual(result.RewardGold, map.Capture().Gold);
        }

        [Test]
        public void Defeat_EndsRun_NoRewardToConfirm()
        {
            var map = NewMap();
            var session = map.EnterBattle();
            session.Context.Player.CurrentHP = 0;
            Assert.AreEqual(BattleOutcome.Defeat, session.Context.EvaluateOutcome(0, 2));

            var result = map.Result();
            Assert.AreEqual(BattleOutcome.Defeat, result.Battle);
            Assert.AreEqual(RunOutcome.Defeat, result.Run);
            Assert.AreEqual(0, result.HpAfter);
            Assert.IsFalse(result.CanConfirm);
            Assert.AreEqual(0, result.CardChoices.Length);
            Assert.AreEqual(0, result.RewardGold);

            Assert.IsFalse(map.ConfirmReward(null, null));
            Assert.AreEqual(1, map.Capture().Node, "패배하면 노드가 넘어가지 않는다");
            Assert.AreEqual(RunOutcome.Defeat, map.Capture().Outcome);
            Assert.IsTrue(map.Capture().Nodes.All(x => !x.Enterable));
        }

        [Test]
        public void Result_BeforeBattleEnds_Throws()
        {
            var map = NewMap();
            Assert.Throws<System.InvalidOperationException>(() => map.Result(), "전투를 시작하기 전");
            map.EnterBattle();
            Assert.Throws<System.InvalidOperationException>(() => map.Result(), "전투가 끝나기 전");
        }

        [Test]
        public void EliteVictory_RequiresRelicChoice_ThenAddsIt()
        {
            var map = NewMap();
            map.Run.StageIndex = 4; // F1-N5 엘리트
            var result = Win(map);

            Assert.AreEqual(NodeKind.Elite, result.Kind);
            Assert.IsTrue(result.NeedsRelicChoice);
            CollectionAssert.AreEqual(new[] { "REL_COM_001", "REL_COM_002", "REL_COM_003" }, result.RelicChoices, "Reward_EliteRelic 1층 A·B·C (시작 유물과 겹치지 않음)");
            Assert.AreEqual(3, result.CardChoices.Length);

            Assert.IsFalse(map.ConfirmReward(null, null), "유물을 고르지 않으면 확정되지 않는다");
            Assert.IsFalse(map.ConfirmReward(0, null));
            Assert.IsFalse(map.ConfirmReward(null, 3));
            Assert.AreEqual(5, map.Capture().Node);
            Assert.AreEqual(0, map.Run.Gold, "확정하지 않았으므로 골드도 그대로");
            Assert.AreEqual(1, map.Run.Relics.Count);

            Assert.IsTrue(map.ConfirmReward(2, 1));
            Assert.AreEqual(6, map.Capture().Node);
            CollectionAssert.AreEqual(new[] { "REL_BLOOD_CHALICE", "REL_COM_002" }, map.Run.Relics);
            Assert.AreEqual(11, map.Capture().DeckCount);
            Assert.AreEqual(result.RewardGold, map.Capture().Gold);
            Assert.IsFalse(map.ConfirmReward(null, 1), "유물 중복 지급 없음");
            Assert.AreEqual(2, map.Run.Relics.Count);
        }

        [Test]
        public void BossVictoryOnLastFloor_GivesBossRelicAndEndsRunAsVictory()
        {
            var map = NewMap();
            map.Run.Floor = 2;
            map.Run.StageIndex = 8; // F2-N9 보스
            var result = Win(map);

            Assert.AreEqual(NodeKind.Boss, result.Kind);
            Assert.IsFalse(result.NeedsRelicChoice, "보스 유물은 자동 지급 (D-16)");
            Assert.IsNotNull(result.AutoRelic);
            Assert.AreEqual(Data.Rewards.BossRelic(2), result.AutoRelic);
            Assert.IsTrue(map.ConfirmReward(null, null));
            Assert.AreEqual(RunOutcome.Victory, map.Capture().Outcome, "D-66: 2층 보스를 잡고 보상을 확정하면 런 승리");
            CollectionAssert.AreEqual(new[] { "REL_BLOOD_CHALICE", result.AutoRelic }, map.Capture().Relics);
            Assert.IsTrue(map.Capture().Nodes.All(x => !x.Enterable));
        }

        [Test]
        public void TwoFloorRun_TraversesAllEighteenNodesAndRestoresAfterEveryCompletion()
        {
            var map = NewMap();
            var kinds = new[]
            {
                NodeKind.Battle, NodeKind.Battle, NodeKind.Event, NodeKind.Battle, NodeKind.Elite,
                NodeKind.Event, NodeKind.Battle, NodeKind.Rest, NodeKind.Boss,
                NodeKind.Battle, NodeKind.Battle, NodeKind.Event, NodeKind.Battle, NodeKind.Elite,
                NodeKind.Event, NodeKind.Battle, NodeKind.Rest, NodeKind.Boss
            };

            for (int i = 0; i < kinds.Length; i++)
            {
                var current = map.Capture().Nodes.Single(n => n.State == MapNodeState.Current);
                Assert.IsTrue(current.Enterable, current.Id + " 진입 가능");
                Assert.AreEqual(kinds[i], current.Kind, current.Id);

                if (RunMapSession.IsBattleKind(current.Kind))
                {
                    var battle = map.EnterBattle();
                    while (battle.Context.Outcome == BattleOutcome.None)
                    {
                        foreach (var enemy in battle.Context.Enemies.Where(e => !e.IsDead).ToArray())
                            battle.Context.Queue.EnqueueMain(new DamageAction { Target = enemy, BaseDamage = 100000, IsStatusTick = true, IgnoreShield = true });
                        battle.Context.Queue.Process();
                        if (battle.Context.Outcome == BattleOutcome.None)
                        {
                            if (battle.Context.PendingWaveCount > 0) battle.Context.EndTurn();
                            else battle.Context.EvaluateOutcome();
                        }
                    }
                    Assert.AreEqual(BattleOutcome.Victory, battle.Context.Outcome, current.Id + " 전투 승리");
                    var reward = map.Result();
                    Assert.IsTrue(map.ConfirmReward(reward.CardChoices.Length == 0 ? (int?)null : 0,
                        reward.NeedsRelicChoice ? (int?)0 : null), current.Id + " 보상 확정");
                    Assert.IsFalse(map.ConfirmReward(0, reward.NeedsRelicChoice ? (int?)0 : null), current.Id + " 보상 중복 차단");
                }
                else if (current.Kind == NodeKind.Event)
                {
                    int completed = 0;
                    BattleScreenSession forced = null;
                    var eventScreen = map.EnterEvent(session => { completed++; forced = session; });
                    Assert.IsTrue(eventScreen.Choose(3), current.Id + " 이벤트 안전 선택");
                    Assert.AreEqual(EventScreenPhase.Result, eventScreen.Phase);
                    Assert.IsTrue(eventScreen.Continue(), current.Id + " 이벤트 계속");
                    Assert.IsFalse(eventScreen.Continue(), current.Id + " 이벤트 중복 진행 차단");
                    Assert.AreEqual(1, completed, current.Id + " 노드 완료 한 번");
                    Assert.IsNull(forced, current.Id + " 강제 전투 없음");
                }
                else
                {
                    int completed = 0;
                    var restScreen = map.EnterRest(() => completed++);
                    var rest = restScreen.Capture();
                    if (rest.CanHeal) Assert.IsTrue(restScreen.ChooseHeal(), current.Id + " 회복 선택");
                    else if (rest.CanUpgrade)
                    {
                        Assert.IsTrue(restScreen.ChooseUpgrade(), current.Id + " 강화 선택");
                        int instanceId = restScreen.Capture().UpgradeCandidates[0].InstanceId;
                        Assert.IsTrue(restScreen.SelectUpgrade(instanceId), current.Id + " 강화 대상");
                    }
                    if (restScreen.Capture().ContinueOnly)
                    {
                        Assert.IsTrue(restScreen.Continue(), current.Id + " 선택지 없음 계속");
                    }
                    else
                    {
                        Assert.IsTrue(restScreen.Confirm(), current.Id + " 휴식 확정");
                        Assert.IsFalse(restScreen.Confirm(), current.Id + " 휴식 효과 중복 차단");
                        Assert.IsTrue(restScreen.Continue(), current.Id + " 휴식 계속");
                    }
                    Assert.IsFalse(restScreen.Continue(), current.Id + " 휴식 중복 진행 차단");
                    Assert.AreEqual(1, completed, current.Id + " 노드 완료 한 번");
                }

                Assert.AreEqual(i + 1, map.Run.VisitedNodeIds.Count, current.Id + " 완료 카운트");
                if (i < kinds.Length - 1)
                {
                    var checkpoint = RunSave.Capture(map.Run, RunSavePhase.Map);
                    map = new RunMapSession(RunSave.Restore(SaveJson.Read(SaveJson.Write(checkpoint))), Data);
                    var restored = map.Capture().Nodes.Single(n => n.State == MapNodeState.Current);
                    Assert.AreEqual(i < 8 ? "F1-N" + (i + 2) : "F2-N" + (i - 7), restored.Id, "복구 후 다음 노드");
                }
            }

            Assert.AreEqual((RunOutcome.Victory, 18), (map.Run.Outcome, map.Run.VisitedNodeIds.Count));
            Assert.AreEqual("F2-N9", map.Run.VisitedNodeIds.Last());
            Assert.IsTrue(map.Capture().Nodes.All(n => !n.Enterable), "승리 뒤 진행 가능한 노드가 없다");
        }

        [Test]
        public void Restart_StartsNewRunWithStartValues()
        {
            var map = NewMap();
            var session = map.EnterBattle();
            session.Context.Player.CurrentHP = 0;
            session.Context.EvaluateOutcome(0, 2);
            map.Result();

            map.Restart(7L);

            var snap = map.Capture();
            Assert.AreEqual(RunOutcome.InProgress, snap.Outcome);
            Assert.AreEqual(1, snap.Floor);
            Assert.AreEqual(1, snap.Node);
            Assert.AreEqual(80, snap.Hp);
            Assert.AreEqual(7L, map.Run.RngSeed);
            Assert.IsTrue(snap.Nodes[0].Enterable);
        }
    }
}
