using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 전투 화면 입력·잠금·1층 노드 1 안내 (`37` §2.1·§3, `04` §3, D-27·D-28·D-32).
    /// 기대 수는 카드·적 스탯과 D-01·D-27로 손으로 계산했다.
    /// </summary>
    [Category("Stage1")]
    public class BattleScreenTests
    {
        static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        [Test]
        public void PhaseAllows_MainAndCommand_FollowsScreenTable()
        {
            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.Summon, BattleState.PlayerPhase));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.Summon, BattleState.CommandPhase));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.Summon, BattleState.EnemyPhase));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.Fusion, BattleState.Resolving));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.Move, BattleState.TurnEnd));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.Sacrifice, BattleState.WaitingDiscard));

            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.ToggleCommand, BattleState.PlayerPhase));
            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.ToggleCommand, BattleState.CommandPhase));
            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.SetAttackTarget, BattleState.CommandPhase));
            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.EndTurn, BattleState.CommandPhase));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.EndTurn, BattleState.EnemyPhase));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.ToggleCommand, BattleState.Resolving));

            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.Inspect, BattleState.EnemyPhase));
            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.ChooseDiscard, BattleState.WaitingDiscard));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.ChooseDiscard, BattleState.PlayerPhase));
            Assert.IsTrue(BattleScreenRules.PhaseAllows(BattleScreenInput.ChooseReplace, BattleState.WaitingReplace));
            Assert.IsFalse(BattleScreenRules.PhaseAllows(BattleScreenInput.ChooseReplace, BattleState.PlayerPhase));
        }

        [Test]
        public void StartDeck_MatchesStageOneNecDeck()
        {
            CollectionAssert.AreEqual(StageOneBattleSim.NecStartDeck, BattleScreenSession.NecStartDeck);
        }

        [Test]
        public void NotTutorial_OpensEveryMainInput_Hand5_Crystal3()
        {
            var session = BattleScreenSession.Start(NewRun(false), Data);
            Assert.IsFalse(session.Context.IsTutorial);
            Assert.AreEqual(0, session.TutorialStep);
            Assert.IsNull(session.TutorialMessage);
            Assert.AreEqual(5, session.Context.Player.Deck.Hand.Count, "시작 패 5 (D-30)");
            Assert.AreEqual(3, session.Context.Player.CurrentCrystal, "MaxCrystal 3");
            Assert.IsTrue(session.Allows(BattleScreenInput.EndTurn));
            Assert.IsTrue(session.Allows(BattleScreenInput.Summon));
            Assert.IsFalse(session.Capture().FusionEnabled, "충전 0이면 융합 비활성");
        }

        [Test]
        public void Tutorial_StepsLockInputs_AndCrystalHandDefMatch()
        {
            var session = BattleScreenSession.Start(NewRun(true), Data);
            var snap = session.Capture();
            Assert.AreEqual((1, 1, 1), (snap.Floor, snap.Node, snap.Turn));
            Assert.AreEqual(30, snap.MaxTurn);
            Assert.AreEqual(80, snap.Hp);
            Assert.AreEqual(80, snap.MaxHp);
            Assert.AreEqual(1, session.TutorialStep);
            Assert.IsTrue(session.TutorialBlocking);
            Assert.IsFalse(snap.EndTurnEnabled);
            Assert.IsFalse(snap.FusionEnabled);
            Assert.AreEqual("NEU_MON_001", snap.HighlightCardId);
            Assert.AreEqual(SlotPosition.Front, snap.HighlightAllySlot);
            Assert.IsTrue(snap.Hand.Single(c => c.CardId == "NEU_MON_001").Playable);
            Assert.IsFalse(snap.Hand.Single(c => c.CardId == "NEC_MON_001").Playable);
            Assert.AreEqual("단일 공격", snap.Enemies[0].IntentLabel);
            Assert.AreEqual("단일 공격", snap.Enemies[1].IntentLabel);
            Assert.IsTrue(snap.Enemies[3].Hidden);
            Assert.IsTrue(snap.Enemies[4].Hidden);
            Assert.AreEqual(14, snap.Enemies[0].Hp, "스테이지 0은 배율 1, 구울 HP 14");
            Assert.AreEqual(14, snap.Enemies[1].Hp);

            int crystal = session.Context.Player.CurrentCrystal;
            Assert.IsFalse(session.TryEndTurn());
            Assert.IsFalse(session.TrySummon(Id(session, "NEC_MON_001"), SlotPosition.Back));
            Assert.IsFalse(session.TryFuse(Id(session, "NEC_MON_001"), Id(session, "NEC_MON_002")));
            Assert.AreEqual(crystal, session.Context.Player.CurrentCrystal);
            Assert.AreEqual(1, session.Context.Turn);
            Assert.AreEqual(5, session.Context.Player.Deck.Hand.Count);

            Assert.IsTrue(session.TrySummon(Id(session, "NEU_MON_001"), SlotPosition.Front));
            var shard = session.Context.Allies.Single();
            Assert.AreEqual(2, session.TutorialStep);
            Assert.AreEqual(3, session.Context.Player.CurrentCrystal, "파편 비용 0");
            Assert.AreEqual(4, session.Context.Player.Deck.Hand.Count);
            Assert.AreEqual(5, shard.CurrentHP);
            Assert.AreEqual(0, session.Context.EffectiveAttack(shard));
            Assert.AreEqual(1, shard.EffectiveDefense, "DEF 0 + Front 1");
            Assert.IsFalse(session.Allows(BattleScreenInput.EndTurn));
            Assert.IsFalse(session.Allows(BattleScreenInput.Fusion));

            Assert.AreEqual(2, session.SacrificeGain(shard.UnitId), "파편 1 + 피의 잔 1");
            Assert.IsFalse(session.TrySacrifice(shard.UnitId), "추출 전에 희생 불가");
            Assert.IsTrue(session.TryPlay(Id(session, "NEC_SKL_001"), shard.UnitId));
            Assert.AreEqual(3, session.Context.Player.CurrentCrystal, "영혼 추출 비용 0");
            Assert.AreEqual(3, session.Context.Player.Deck.Hand.Count, "5 - 파편 - 추출");
            Assert.AreEqual(1, session.Context.Player.Deck.DiscardPile.Count);

            int handBefore = session.Context.Player.Deck.Hand.Count;
            Assert.IsTrue(session.TrySacrifice(shard.UnitId));
            Assert.AreEqual(5, session.Context.Player.CurrentCrystal, "3 + 1 + 1");
            Assert.AreEqual(handBefore + 2, session.Context.Player.Deck.Hand.Count, "사망 드로우 2");
            Assert.AreEqual(3, session.TutorialStep);
            Assert.AreEqual(3, session.Context.Player.Deck.DrawPile.Count, "5장 중 2장");
            Assert.AreEqual(2, session.Context.Player.Deck.DiscardPile.Count, "추출 + 파편");
            Assert.IsFalse(session.Allows(BattleScreenInput.EndTurn));
            Assert.IsFalse(session.Allows(BattleScreenInput.Fusion));

            Assert.IsTrue(session.TrySummon(Id(session, "NEC_MON_001"), SlotPosition.Front));
            Assert.AreEqual(4, session.Context.Player.CurrentCrystal, "뼈 병사 비용 1");
            Assert.AreEqual(3, session.TutorialStep, "둘 다 나오기 전에는 단계 3");
            Assert.IsTrue(session.TrySummon(Id(session, "NEC_MON_002"), SlotPosition.Middle));
            Assert.AreEqual(3, session.Context.Player.CurrentCrystal, "구울 비용 1, 5 - 1 - 1");
            Assert.AreEqual(0, session.TutorialStep);
            Assert.IsFalse(session.TutorialBlocking);
            Assert.IsTrue(session.Allows(BattleScreenInput.EndTurn));
            var bone = Ally(session, "NEC_MON_001");
            var ghoul = Ally(session, "NEC_MON_002");
            Assert.AreEqual(3, session.Context.EffectiveAttack(bone));
            Assert.AreEqual(2, bone.EffectiveDefense, "DEF 1 + Front 1");
            Assert.AreEqual(2, session.Context.EffectiveAttack(ghoul));
            Assert.AreEqual(0, ghoul.EffectiveDefense);
            Assert.AreEqual(3, session.Context.Player.Deck.Hand.Count);
            Assert.AreEqual(0, session.Context.Player.Deck.ExhaustPile.Count);
            Assert.AreEqual(0, session.Context.Player.FusionCharge);
        }

        [Test]
        public void AfterGuidance_Move_ChangesOnlyAdjacentEmpty_AndFrontDef()
        {
            var session = Guide(SlotPosition.Middle, SlotPosition.Front);
            var bone = Ally(session, "NEC_MON_001");
            var ghoul = Ally(session, "NEC_MON_002");
            int crystal = session.Context.Player.CurrentCrystal;

            Assert.IsFalse(session.TryMove(bone.UnitId, SlotPosition.Front), "가운데에서 앞은 인접하지만 구울이 있다");
            Assert.AreEqual(SlotPosition.Middle, session.Context.Field.PositionOf(bone));
            Assert.IsTrue(session.TryMove(bone.UnitId, SlotPosition.Back));
            Assert.AreEqual(1, bone.EffectiveDefense, "DEF 1, Front 보너스 없음");
            Assert.IsFalse(session.TryMove(bone.UnitId, SlotPosition.Front), "뒤와 앞은 인접하지 않다");
            Assert.AreEqual(SlotPosition.Back, session.Context.Field.PositionOf(bone));
            Assert.AreEqual(1, ghoul.EffectiveDefense, "앞줄 DEF 0 + 1");
            Assert.IsTrue(session.TryMove(ghoul.UnitId, SlotPosition.Middle));
            Assert.AreEqual(0, ghoul.EffectiveDefense);
            Assert.AreEqual(crystal, session.Context.Player.CurrentCrystal, "위치 변경은 Crystal을 쓰지 않는다");
        }

        [Test]
        public void CommandPhase_AllowsCommandAndEndTurn_NotSummon()
        {
            var session = Guide(SlotPosition.Front, SlotPosition.Middle);
            var bone = Ally(session, "NEC_MON_001");
            session.Context.State = BattleState.CommandPhase;
            int crystal = session.Context.Player.CurrentCrystal;
            Assert.IsFalse(session.TrySummon(session.Context.Player.Deck.Hand[0].InstanceId, SlotPosition.Back));
            Assert.AreEqual(crystal, session.Context.Player.CurrentCrystal);
            Assert.IsTrue(session.ToggleCommand(bone.UnitId));
            Assert.AreEqual(UnitCommand.Defend, bone.Command);
            Assert.AreEqual("방어", session.Capture().Allies[0].CommandLabel);

            Assert.IsTrue(session.TryEndTurn());
            // 뼈 병사는 방어라 공격하지 않는다. 구울 ATK 2 - 앞 적 DEF 2 = 0 → 최소 1. 14 - 1 = 13.
            Assert.AreEqual(13, session.Context.Field.At(TeamType.Enemy, SlotPosition.Front).CurrentHP);
            Assert.AreEqual(14, session.Context.Field.At(TeamType.Enemy, SlotPosition.Middle).CurrentHP);
            Assert.AreEqual(80, session.Context.Player.CurrentHP, "아군이 있으면 단일 공격은 플레이어를 맞히지 않는다");
            Assert.AreEqual(2, session.Context.Turn);
            Assert.AreEqual(4, session.TutorialStep);
        }

        [Test]
        public void EnemyPhase_BlocksCommandAndEndTurn()
        {
            var session = Guide(SlotPosition.Front, SlotPosition.Middle);
            var bone = Ally(session, "NEC_MON_001");
            session.Context.State = BattleState.EnemyPhase;
            Assert.IsFalse(session.ToggleCommand(bone.UnitId));
            Assert.AreEqual(UnitCommand.Attack, bone.Command);
            Assert.IsFalse(session.TryEndTurn());
            Assert.AreEqual(1, session.Context.Turn);
            Assert.IsTrue(session.Allows(BattleScreenInput.Inspect));
        }

        [Test]
        public void FirstAttack_DealsTwo_ToFrontEnemy_AndOpensIntentTip()
        {
            var session = Guide(SlotPosition.Front, SlotPosition.Middle);
            var front = session.Context.Enemies.Single(e => session.Context.Field.PositionOf(e) == SlotPosition.Front);
            var back = session.Context.Enemies.Single(e => session.Context.Field.PositionOf(e) == SlotPosition.Middle);
            Assert.IsTrue(session.TrySetAttackTarget(Ally(session, "NEC_MON_001").UnitId, front.UnitId));
            Assert.IsTrue(session.TryEndTurn());
            // 뼈 ATK 3 - DEF 2 = 1, 구울 ATK 2 - DEF 2 = 0 → 1. 합 2. 14 - 2 = 12. 가운데 적은 0.
            Assert.AreEqual(12, front.CurrentHP);
            Assert.AreEqual(14, back.CurrentHP);
            Assert.AreEqual(80, session.Context.Player.CurrentHP);
            Assert.AreEqual(2, session.Context.Turn);
            Assert.AreEqual(3, session.Context.Player.CurrentCrystal, "턴 시작 MaxCrystal 3");
            var snap = session.Capture();
            Assert.AreEqual(4, snap.TutorialStep);
            StringAssert.Contains("행동의 종류", snap.TutorialMessage);
            Assert.IsFalse(snap.TutorialBlocking);
            Assert.IsTrue(snap.EndTurnEnabled);
            Assert.AreEqual("단일 공격", snap.Enemies[0].IntentLabel);
        }

        [Test]
        public void Tips_CloseAfterThreeSeconds_OrClick_ThenDone()
        {
            var session = Guide(SlotPosition.Front, SlotPosition.Middle);
            Assert.IsFalse(session.DismissTip());
            Assert.IsTrue(session.TryEndTurn());
            Assert.AreEqual(4, session.TutorialStep);
            session.AdvanceTipClock(2f);
            Assert.AreEqual(4, session.TutorialStep, "3초 전에는 닫히지 않는다");
            session.AdvanceTipClock(1f);
            Assert.AreEqual(5, session.TutorialStep);
            StringAssert.Contains("공격과 방어", session.TutorialMessage);
            Assert.IsTrue(session.Allows(BattleScreenInput.EndTurn), "팁은 입력을 막지 않는다");
            session.AdvanceTipClock(3f);
            Assert.AreEqual(0, session.TutorialStep);
            Assert.IsNull(session.TutorialMessage);
        }

        [Test]
        public void Fusion_PreviewAndCost_AfterGuidance()
        {
            var session = Guide(SlotPosition.Front, SlotPosition.Middle);
            session.Context.Player.FusionCharge = 1;
            var bone = session.Context.Cards.Get("NEC_MON_001").CreateCard(100, false);
            var ghoulA = session.Context.Cards.Get("NEC_MON_002").CreateCard(101, false);
            var ghoulB = session.Context.Cards.Get("NEC_MON_002").CreateCard(102, false);
            session.Context.Player.Deck.AddToHand(bone);
            session.Context.Player.Deck.AddToHand(ghoulA);
            var random = session.PreviewFusion(100, 101);
            Assert.AreEqual(2, random.Cost, "Lv1 융합 비용 2");
            Assert.IsTrue(random.Random, "뼈 병사 + 구울은 고정 레시피가 아니다");
            Assert.IsNull(random.ResultCardId);

            session.Context.Player.Deck.AddToHand(ghoulB);
            var fixedRecipe = session.PreviewFusion(101, 102);
            Assert.AreEqual("NEC_MON_013", fixedRecipe.ResultCardId, "FUS_001 식인 구울");
            Assert.IsFalse(fixedRecipe.Random);
            Assert.AreEqual(2, fixedRecipe.Cost);
            Assert.IsTrue(fixedRecipe.CanConfirm);

            Assert.IsTrue(session.TryFuse(101, 102));
            Assert.AreEqual(1, session.Context.Player.CurrentCrystal, "3 - 2");
            Assert.AreEqual(0, session.Context.Player.FusionCharge, "충전 1 - 1");
            Assert.IsTrue(session.Context.Player.Deck.Hand.Any(c => c.CardId == "NEC_MON_013"));
            Assert.IsFalse(session.Context.Player.Deck.Hand.Any(c => c.InstanceId == 101));
            Assert.IsFalse(session.Context.Player.Deck.Hand.Any(c => c.InstanceId == 102));
        }

        [Test]
        public void BattleEnd_MatchesCoreOutcome_AndDoesNotAdvanceNode()
        {
            var session = Guide(SlotPosition.Front, SlotPosition.Middle);
            for (int i = 0; i < 40 && session.Context.Outcome == BattleOutcome.None; i++)
                Assert.IsTrue(session.TryEndTurn(), "안내가 끝난 뒤에는 턴 종료가 된다");

            Assert.AreNotEqual(BattleOutcome.None, session.Context.Outcome);
            Assert.AreEqual(session.Context.Outcome, session.Capture().Outcome);
            Assert.IsTrue(session.Run.TutorialCompleted);
            Assert.AreEqual(System.Math.Max(0, session.Context.Player.CurrentHP), session.Run.CurrentHp);
            Assert.AreEqual(1, session.Run.Floor);
            Assert.AreEqual(1, session.Run.Node);
            if (session.Context.Outcome == BattleOutcome.Defeat)
                Assert.AreEqual(RunOutcome.Defeat, session.Run.Outcome);
            else
                Assert.AreEqual(RunOutcome.InProgress, session.Run.Outcome, "승리 보상 화면 전에는 노드를 넘기지 않는다");
            Assert.IsFalse(session.TryEndTurn());
        }

        static BattleScreenSession Guide(SlotPosition boneSlot, SlotPosition ghoulSlot)
        {
            var session = BattleScreenSession.Start(NewRun(true), Data);
            Assert.IsTrue(session.TrySummon(Id(session, "NEU_MON_001"), SlotPosition.Front));
            var shard = Ally(session, "NEU_MON_001");
            Assert.IsTrue(session.TryPlay(Id(session, "NEC_SKL_001"), shard.UnitId));
            Assert.IsTrue(session.TrySacrifice(shard.UnitId));
            Assert.IsTrue(session.TrySummon(Id(session, "NEC_MON_001"), boneSlot));
            Assert.IsTrue(session.TrySummon(Id(session, "NEC_MON_002"), ghoulSlot));
            Assert.AreEqual(0, session.TutorialStep);
            return session;
        }

        static RunState NewRun(bool tutorial)
        {
            return RunFlow.NewRun("RUN-T", 3, "NEC", BattleScreenSession.StartHp, BattleScreenSession.NecStartDeck,
                new[] { BattleScreenSession.StartRelicId }, tutorialEnabled: tutorial);
        }

        static int Id(BattleScreenSession session, string cardId)
        {
            return session.Context.Player.Deck.Hand.First(c => c.CardId == cardId).InstanceId;
        }

        static RuntimeMonster Ally(BattleScreenSession session, string dataId)
        {
            return session.Context.Allies.Single(a => a.DataId == dataId && !a.IsDead);
        }
    }
}
