using System;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 휴식 화면 (`37` §2.4·§2.9, D-31·D-69, `32` REST-01). 기대값은 HP 80, 회복 floor(80 × 0.3) = 24에서 손으로 계산했다.
    /// 확정 전에는 런이 바뀌지 않고, 회복·강화·계속은 한 번만 적용된다.
    /// </summary>
    [Category("Stage1")]
    public class RestScreenSessionTests
    {
        static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        /// <summary>F1-N8 휴식 노드의 NEC 새 런.</summary>
        static RunState NewRun(int hp)
        {
            var run = RunFlow.NewRun("RUN-1", 20260601L, "NEC", BattleScreenSession.StartHp, BattleScreenSession.NecStartDeck,
                new[] { BattleScreenSession.StartRelicId }, false);
            run.StageIndex = 7;
            run.CurrentHp = hp;
            return run;
        }

        static RestScreenSession Open(RunState run, Action<RestScreenResult> done = null) => new RestScreenSession(run, Data, done);

        [Test]
        public void Constructor_RejectsNonRestNodeAndFinishedRun()
        {
            var battle = NewRun(75);
            battle.StageIndex = 0;
            Assert.Throws<ArgumentException>(() => Open(battle), "F1-N1은 전투 노드");
            var over = NewRun(75);
            over.Outcome = RunOutcome.Defeat;
            Assert.Throws<ArgumentException>(() => Open(over));
        }

        [Test]
        public void Capture_ShowsOptionsAndPreview_WithoutChangingRun()
        {
            var run = NewRun(75);
            var snap = Open(run).Capture();

            Assert.AreEqual(RestScreenPhase.Choosing, snap.Phase);
            Assert.AreEqual("F1-N8", snap.NodeId);
            Assert.AreEqual(75, snap.Hp);
            Assert.AreEqual(80, snap.MaxHp);
            Assert.IsTrue(snap.CanHeal);
            Assert.AreEqual(24, snap.HealAmount, "floor(80 × 0.3)");
            Assert.AreEqual(80, snap.HealPreviewHp, "75 + 24 = 99, MaxHP 80으로 제한");
            Assert.IsTrue(snap.CanUpgrade);
            Assert.IsFalse(snap.ContinueOnly);
            Assert.AreEqual(75, run.CurrentHp);
        }

        [Test]
        public void Heal_DoesNothingUntilConfirmed_ThenAppliesOnce()
        {
            var run = NewRun(40);
            var session = Open(run);

            Assert.IsTrue(session.ChooseHeal());
            Assert.AreEqual(RestScreenPhase.PreviewHeal, session.Capture().Phase);
            Assert.AreEqual(64, session.Capture().HealPreviewHp, "40 + 24");
            Assert.AreEqual(40, run.CurrentHp, "확정 전에는 HP가 그대로");
            Assert.IsTrue(session.Back());
            Assert.AreEqual(RestScreenPhase.Choosing, session.Capture().Phase);
            Assert.AreEqual(40, run.CurrentHp);

            Assert.IsTrue(session.ChooseHeal());
            Assert.IsTrue(session.Confirm());
            Assert.AreEqual(64, run.CurrentHp);
            var result = session.Result;
            Assert.IsTrue(result.Healed);
            Assert.IsFalse(result.Upgraded);
            Assert.AreEqual(40, result.HpBefore);
            Assert.AreEqual(64, result.HpAfter);

            Assert.IsFalse(session.Confirm(), "두 번째 확정은 아무것도 바꾸지 않는다");
            Assert.AreEqual(64, run.CurrentHp, "중복 회복 없음");
            Assert.IsFalse(session.ChooseUpgrade(), "한 노드에서 선택지는 하나");
            Assert.IsFalse(session.ChooseHeal());
        }

        [Test]
        public void Heal_AtFullHp_IsDisabled()
        {
            var run = NewRun(80);
            var session = Open(run);

            Assert.IsFalse(session.Capture().CanHeal);
            Assert.IsFalse(session.ChooseHeal());
            Assert.AreEqual(RestScreenPhase.Choosing, session.Capture().Phase);
            Assert.IsFalse(session.Confirm());
        }

        [Test]
        public void Upgrade_ListsUpgradableCardsInDeckOrder_AndChangesNothingUntilConfirmed()
        {
            var run = NewRun(80);
            var session = Open(run);
            var expected = run.Deck.Where(c => RunDeckRules.IsUpgradable(c, Data.Cards)).Select(c => c.InstanceId).ToArray();
            Assert.Greater(expected.Length, 0, "시작 덱에 강화할 카드가 있다");

            Assert.IsTrue(session.ChooseUpgrade());
            var snap = session.Capture();
            Assert.AreEqual(RestScreenPhase.PickingUpgrade, snap.Phase);
            CollectionAssert.AreEqual(expected, snap.UpgradeCandidates.Select(c => c.InstanceId).ToArray(), "런 덱 순서");
            Assert.IsFalse(snap.CanConfirm, "고르기 전에는 확정할 수 없다");
            Assert.IsFalse(session.Confirm());
            Assert.IsFalse(session.SelectUpgrade(9999), "후보가 아닌 개체");

            int pick = expected[0];
            Assert.IsTrue(session.SelectUpgrade(pick));
            Assert.AreEqual(pick, session.Capture().SelectedUpgrade);
            Assert.IsTrue(session.Capture().CanConfirm);
            Assert.IsFalse(run.Deck.Single(c => c.InstanceId == pick).Upgraded, "확정 전에는 덱이 그대로");

            Assert.IsTrue(session.Back());
            Assert.AreEqual(-1, session.Capture().SelectedUpgrade, "돌아가면 선택이 사라진다");
            Assert.IsFalse(run.Deck.Single(c => c.InstanceId == pick).Upgraded);
        }

        [Test]
        public void Upgrade_SameCardIdTwice_UpgradesOnlyChosenInstance()
        {
            var run = NewRun(80);
            string id = run.Deck.First(c => RunDeckRules.IsUpgradable(c, Data.Cards)).CardId;
            var first = run.Deck.First(c => c.CardId == id);
            var second = run.AddCard(id);
            var session = Open(run);

            Assert.IsTrue(session.ChooseUpgrade());
            Assert.AreEqual(2, session.Capture().UpgradeCandidates.Count(c => c.CardId == id), "같은 ID 2장 모두 후보");
            Assert.IsTrue(session.SelectUpgrade(second.InstanceId));
            Assert.IsTrue(session.Confirm());

            Assert.IsTrue(second.Upgraded, "고른 개체만 강화");
            Assert.IsFalse(first.Upgraded);
            Assert.AreEqual(second.InstanceId, session.Result.UpgradedInstanceId);
            Assert.AreEqual(id, session.Result.UpgradedCardId);
            Assert.IsFalse(session.Confirm());
            Assert.IsFalse(session.SelectUpgrade(first.InstanceId), "확정 뒤에는 고를 수 없다");
            Assert.IsFalse(first.Upgraded, "중복 강화 없음");
        }

        [Test]
        public void ContinueOnly_WhenNothingToDo_CompletesOnceWithoutChange()
        {
            var run = NewRun(80);
            foreach (var card in run.Deck.Where(c => RunDeckRules.IsUpgradable(c, Data.Cards))) card.Upgraded = true;
            int calls = 0;
            RestScreenResult got = null;
            var session = Open(run, r => { calls++; got = r; });

            var snap = session.Capture();
            Assert.IsFalse(snap.CanHeal);
            Assert.IsFalse(snap.CanUpgrade);
            Assert.IsTrue(snap.ContinueOnly, "D-69: [계속]만 보인다");
            Assert.IsFalse(session.ChooseHeal());
            Assert.IsFalse(session.ChooseUpgrade());

            Assert.IsTrue(session.Continue());
            Assert.AreEqual(1, calls);
            Assert.IsTrue(got.ContinuedOnly);
            Assert.IsFalse(got.Healed);
            Assert.IsFalse(got.Upgraded);
            Assert.AreEqual(80, run.CurrentHp);
            Assert.IsFalse(session.Continue(), "두 번째 [계속]은 콜백을 다시 부르지 않는다");
            Assert.AreEqual(1, calls);
            Assert.IsTrue(session.Completed);
        }

        [Test]
        public void Continue_BeforeConfirm_IsRejected_AndAfterConfirmCompletesOnce()
        {
            var run = NewRun(40);
            int calls = 0;
            RestScreenResult got = null;
            var session = Open(run, r => { calls++; got = r; });

            Assert.IsFalse(session.Continue(), "선택지가 남아 있으면 [계속]으로 건너뛸 수 없다");
            session.ChooseHeal();
            Assert.IsFalse(session.Continue(), "확정 전");
            session.Confirm();
            Assert.AreEqual(0, calls, "확정만으로는 노드를 완료하지 않는다");

            Assert.IsTrue(session.Continue());
            Assert.AreEqual(1, calls);
            Assert.IsTrue(got.Healed);
            Assert.AreEqual("F1-N8", got.NodeId);
            Assert.IsFalse(session.Continue());
            Assert.AreEqual(1, calls);
            Assert.AreEqual(64, run.CurrentHp);
        }

        [Test]
        public void Screen_DoesNotCompleteTheNode()
        {
            var run = NewRun(40);
            var session = Open(run);
            session.ChooseHeal();
            session.Confirm();
            session.Continue();

            Assert.AreEqual(8, run.Node, "노드 완료는 맵이 한다");
            Assert.AreEqual(0, run.VisitedNodeIds.Count);
        }
    }
}
