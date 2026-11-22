using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 동작 핸들러 등록 조회 (`38` §5 "동작 핸들러 없음", `38` §6)와 1단계 범위 목록 (`01` D-44·D-45·D-69). #76 2-1a.
    /// 임포터(#35)가 이 API로 필수 핸들러를 검사한다.
    /// </summary>
    [Category("Stage1")]
    public class HandlerRegistryTests
    {
        private static GameData Data => StageOneBattleSim.Data();

        [Test]
        public void Stage1Scope_Counts()
        {
            Assert.AreEqual(32, Stage1Scope.Cards.Count, "D-44");
            Assert.AreEqual(3, Stage1Scope.Curses.Count, "D-45: CUR_002·003·004");
            Assert.AreEqual(8, Stage1Scope.Relics.Count, "D-69: 핸들러 검사 8종");
            Assert.AreEqual(7, Stage1Scope.Events.Count, "PROG-06: 1~2층 합집합 7종");
            CollectionAssert.AreEqual(new[] { "REL_BOSS_2F" }, Stage1Scope.DisplayOnlyRelics.ToArray(), "D-69: 지급·표시만");
        }

        [Test]
        public void Stage1Scope_Cards_SameAsFixture32()
        {
            var fixture = StageOneCards.Create();
            CollectionAssert.AreEquivalent(fixture.Ids.ToArray(), Stage1Scope.Cards.ToArray());
        }

        [Test]
        public void Stage1Scope_Events_AreFloor1And2PoolWithPositiveWeight()
        {
            // Event_FloorPool Floor_1·Floor_2 중 하나라도 0보다 큰 이벤트 (D-23)
            var fromData = Data.Rows("Event_FloorPool").Where(r => r.Num("Floor_1") > 0 || r.Num("Floor_2") > 0).Select(r => r.Str("Event_ID"));
            CollectionAssert.AreEquivalent(fromData.ToArray(), Stage1Scope.Events.ToArray());
        }

        [Test]
        public void Stage1Scope_Relics_AreStartBoss1FAndElitePools1And2()
        {
            // 시작 피의 잔(D-06) + 1층 보스(D-16) + Reward_EliteRelic 1·2층 풀 A~C
            var pools = Data.Rows("Reward_EliteRelic").Where(r => r.Int("Floor") <= 2)
                .SelectMany(r => new[] { r.Str("Relic_Pool_A"), r.Str("Relic_Pool_B"), r.Str("Relic_Pool_C") });
            var expected = new[] { "REL_BLOOD_CHALICE", "REL_BOSS_1F" }.Concat(pools);
            CollectionAssert.AreEquivalent(expected.ToArray(), Stage1Scope.Relics.ToArray());
            var relicIds = Data.Rows("Relics_MVP").Select(r => r.Str("Relic_ID")).ToList();
            foreach (var id in Stage1Scope.Relics.Concat(Stage1Scope.DisplayOnlyRelics)) CollectionAssert.Contains(relicIds, id);
        }

        [Test]
        public void Stage1Scope_Curses_ExistInCurseCards()
        {
            var ids = Data.Rows("Curse_Cards").Select(r => r.Str("Curse_ID")).ToList();
            foreach (var id in Stage1Scope.Curses) CollectionAssert.Contains(ids, id);
        }

        [Test]
        public void Cards_AllStage1CardsHaveHandlers()
        {
            CollectionAssert.IsEmpty(HandlerRegistry.Missing(HandlerKind.Card, Stage1Scope.Cards, Data));
        }

        [TestCase("NEC_SKL_002", true, TestName = "Skill with CardEffects handler")]
        [TestCase("NEC_MON_001", true, TestName = "Monster without effect rows")]
        [TestCase("NEU_MON_001", true, TestName = "Monster with data-only effect (SacrificeCrystal)")]
        [TestCase("NEC_MON_010", true, TestName = "Monster with MonsterEffects handler")]
        [TestCase("NEU_MON_002", true, TestName = "Monster handled by CardEffects + FusionAsClass")]
        [TestCase("ELE_SKL_001", false, TestName = "Stage 2 skill without handler")]
        [TestCase("NEC_MON_011", false, TestName = "Stage 2 monster with effect rows, no handler")]
        [TestCase("NOPE_999", false, TestName = "Unknown card id")]
        public void Cards_Has(string id, bool expected)
        {
            Assert.AreEqual(expected, HandlerRegistry.Has(HandlerKind.Card, id, Data));
        }

        [Test]
        public void Cards_NeedData_NonCardKindsDoNot()
        {
            Assert.Throws<System.ArgumentNullException>(() => HandlerRegistry.Has(HandlerKind.Card, "NEC_SKL_002", null));
            Assert.IsFalse(HandlerRegistry.Has(HandlerKind.Relic, "REL_BLOOD_CHALICE", null));
        }

        [Test]
        public void CurseRelicEvent_NotImplementedYet_AllMissing()
        {
            // 2-3(저주·이벤트)·2-4(유물)에서 구현하면 이 목록이 줄어든다. 그때 이 테스트를 "비어 있음"으로 바꾼다
            CollectionAssert.AreEquivalent(Stage1Scope.Curses.ToArray(), HandlerRegistry.Missing(HandlerKind.Curse, Stage1Scope.Curses, Data).ToArray());
            CollectionAssert.AreEquivalent(Stage1Scope.Relics.ToArray(), HandlerRegistry.Missing(HandlerKind.Relic, Stage1Scope.Relics, Data).ToArray());
            CollectionAssert.AreEquivalent(Stage1Scope.Events.ToArray(), HandlerRegistry.Missing(HandlerKind.Event, Stage1Scope.Events, Data).ToArray());
        }

        [Test]
        public void Stage1Check_CardsAreErrors_OthersAreWarningsUntilImplemented()
        {
            var r = HandlerRegistry.CheckStage1(Data);
            CollectionAssert.IsEmpty(r.MissingCards);
            Assert.AreEqual(3 + 8 + 7, r.MissingPending.Count, "저주 3·유물 8·이벤트 7 — 구현 전");
            Assert.IsTrue(r.MissingPending.Contains("Relic:REL_BLOOD_CHALICE"));
            Assert.IsTrue(r.CardsOk);
        }
    }
}
