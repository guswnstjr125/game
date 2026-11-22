using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 18노드 맵과 전투 결과 화면의 값 (`37` §1·§2, `03` §2, D-66·D-67·D-73·D-75).
    /// 기대값은 노드 템플릿(`RunMap.KindOf`, 1단계 빌드)과 NEC 시작 값(HP 80, 덱 10장, 피의 잔)에서 손으로 계산했다.
    /// </summary>
    [Category("Stage1")]
    public class RunMapSessionTests
    {
        static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        static RunMapSession NewMap(bool tutorial = false) => RunMapSession.NewNecRun(Data, 20260601L, tutorial);

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
        public void NodeWithoutScreen_IsCurrentButNotEnterable()
        {
            var map = NewMap();
            map.Run.StageIndex = 2; // F1-N3 이벤트: 이벤트 화면은 이번 범위 밖

            var snap = map.Capture();
            Assert.AreEqual(MapNodeState.Current, snap.Nodes[2].State);
            Assert.AreEqual(NodeKind.Event, snap.Nodes[2].Kind);
            Assert.IsFalse(snap.Nodes[2].Enterable);
            Assert.IsFalse(map.CanEnter(1, 3));
            Assert.IsFalse(map.CanEnter(1, 4), "현재 노드가 아니면 들어갈 수 없다");
            Assert.Throws<System.InvalidOperationException>(() => map.EnterBattle());
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[0].State);
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[1].State);
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
        public void Victory_ShowsResult_ThenContinueAdvancesToNextNode()
        {
            var map = NewMap();
            var session = map.EnterBattle();
            Assert.AreEqual(BattleOutcome.Victory, session.Context.EvaluateOutcome(session.Context.Player.CurrentHP, 0));

            var result = map.Result();
            Assert.AreEqual(BattleOutcome.Victory, result.Battle);
            Assert.AreEqual("F1-N1", result.NodeId);
            Assert.AreEqual(NodeKind.Battle, result.Kind);
            Assert.AreEqual(80, result.HpBefore);
            Assert.AreEqual(80, result.HpAfter);
            Assert.IsTrue(result.CanContinue);
            var row = Data.Rewards.Row(NodeKind.Battle, 1);
            Assert.That(result.RewardGold, Is.InRange(row.GoldMin, row.GoldMax), "D-75 보상 골드 범위");
            Assert.AreEqual(1, map.Run.Node, "결과를 보는 동안 노드는 그대로");
            Assert.AreEqual(0, map.Run.Gold);

            Assert.IsTrue(map.Continue());
            var snap = map.Capture();
            Assert.AreEqual(2, snap.Node);
            Assert.AreEqual(result.RewardGold, snap.Gold, "보상 골드가 한 번만 들어간다");
            Assert.AreEqual(10, snap.DeckCount, "카드는 건너뛴다 (D-37)");
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[0].State);
            Assert.AreEqual(MapNodeState.Current, snap.Nodes[1].State);
            Assert.IsFalse(map.Continue(), "두 번째 호출은 아무것도 바꾸지 않는다");
            Assert.AreEqual(2, map.Capture().Node);
        }

        [Test]
        public void Defeat_EndsRun_ContinueDoesNotAdvance()
        {
            var map = NewMap();
            var session = map.EnterBattle();
            session.Context.Player.CurrentHP = 0;
            Assert.AreEqual(BattleOutcome.Defeat, session.Context.EvaluateOutcome(0, 2));

            var result = map.Result();
            Assert.AreEqual(BattleOutcome.Defeat, result.Battle);
            Assert.AreEqual(RunOutcome.Defeat, result.Run);
            Assert.AreEqual(0, result.HpAfter);
            Assert.IsFalse(result.CanContinue);
            Assert.AreEqual(0, result.RewardGold);

            Assert.IsFalse(map.Continue());
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
        public void EliteVictory_NeedsRelicChoice_SoContinueIsHeld()
        {
            var map = NewMap();
            map.Run.StageIndex = 4; // F1-N5 엘리트
            var session = map.EnterBattle();
            session.Context.EvaluateOutcome(session.Context.Player.CurrentHP, 0);

            var result = map.Result();
            Assert.AreEqual(NodeKind.Elite, result.Kind);
            Assert.IsTrue(result.NeedsRelicChoice, "엘리트 유물 선택 화면은 다음 작업");
            Assert.IsFalse(result.CanContinue);
            Assert.IsFalse(map.Continue());
            Assert.AreEqual(5, map.Capture().Node);
            Assert.AreEqual(0, map.Run.Gold, "확정하지 않았으므로 골드도 그대로");
        }

        [Test]
        public void BossVictoryOnLastFloor_EndsRunAsVictory()
        {
            var map = NewMap();
            map.Run.Floor = 2;
            map.Run.StageIndex = 8; // F2-N9 보스
            var session = map.EnterBattle();
            session.Context.EvaluateOutcome(session.Context.Player.CurrentHP, 0);

            var result = map.Result();
            Assert.AreEqual(NodeKind.Boss, result.Kind);
            Assert.IsTrue(result.CanContinue, "보스 유물은 자동 지급 (D-16)");
            Assert.IsTrue(map.Continue());
            Assert.AreEqual(RunOutcome.Victory, map.Capture().Outcome, "D-66: 2층 보스를 잡고 보상을 확정하면 런 승리");
            Assert.AreEqual(1, map.Capture().Relics.Length - 1, "시작 유물에 보스 유물 1개가 더해진다");
            Assert.IsTrue(map.Capture().Nodes.All(x => !x.Enterable));
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
