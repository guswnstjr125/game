using System;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 이벤트 선택 화면 세션 (`37` §2.5, `24` §2, `40`, D-45·D-51·D-52·D-67·D-68·D-75·D-77). 규칙은 EventFlow가 정본이고
    /// 여기서는 화면 흐름(선택 → 대상 고르기 → 확정 → 결과 → 계속)과 표시 값을 본다.
    /// 기대값은 NEC 시작 값(HP 80, 골드 0, 덱 10장)과 `06` Events_MVP·`40` 문구에서 손으로 계산했다.
    /// </summary>
    [Category("Stage1")]
    public class EventScreenSessionTests
    {
        static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        static RunState NewRun(int floor, long seed = 1)
        {
            var run = RunFlow.NewRun("RUN-ES", seed, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
            run.Floor = floor;
            run.StageIndex = 2;
            return run;
        }

        static EventScreenSession Open(RunState run, string eventId, Action<EventScreenResult> done = null) =>
            new EventScreenSession(run.Floor, eventId, run, Data, done);

        [Test]
        public void Open_RejectsOutOfScopeInput()
        {
            Assert.Throws<ArgumentException>(() => Open(NewRun(3), "EVT_DARK_WAGER"), "3층은 이 화면 범위 밖");
            Assert.Throws<ArgumentException>(() => Open(NewRun(2), "EVT_WANDER_MERCHANT"), "방랑 상인 2층 가중치 0");
            Assert.Throws<ArgumentException>(() => Open(NewRun(1), "EVT_DARK_WAGER"), "어둠의 내기 1층 가중치 0");
            Assert.Throws<ArgumentException>(() => Open(NewRun(1), "EVT_GRAVE_WHISPER"), "묘지의 속삭임은 3·4층");
            Assert.Throws<ArgumentException>(() => new EventScreenSession(2, "EVT_FORGOTTEN_SHRINE", NewRun(1), Data), "입력 층과 런 층이 다름");
            var over = NewRun(1);
            over.Outcome = RunOutcome.Defeat;
            Assert.Throws<ArgumentException>(() => Open(over, "EVT_FORGOTTEN_SHRINE"));
        }

        [Test]
        public void TextTable_CoversExactlyTheImplementedEvents_WithThreeLabels()
        {
            CollectionAssert.AreEquivalent(EventFlow.Implemented.ToArray(), EventScreenText.EventIds.ToArray());
            foreach (var id in EventFlow.Implemented)
            {
                Assert.IsNotEmpty(EventScreenText.Intro(id), id);
                for (int i = 1; i <= 3; i++)
                    foreach (bool stage1 in new[] { true, false })
                        Assert.IsNotEmpty(EventScreenText.OptionLabel(id, i, stage1), id + " " + i);
            }
        }

        [Test]
        public void Capture_Initial_ShowsTitleIntroOptionsAndStatus()
        {
            var run = NewRun(1);
            run.Gold = 7;
            var snap = Open(run, "EVT_WANDER_MERCHANT").Capture();

            Assert.AreEqual((1, "F1-N3", "방랑 상인"), (snap.Floor, snap.NodeId, snap.Title));
            StringAssert.StartsWith("얼굴 없는 상인이", snap.Intro);
            Assert.AreEqual((80, 80, 7), (snap.Hp, snap.MaxHp, snap.Gold));
            Assert.AreEqual(EventScreenPhase.Choosing, snap.Phase);
            CollectionAssert.AreEqual(new[] { false, true, true }, snap.Options.Select(o => o.Enabled).ToArray(), "골드 7 < 8: 1번만 어둡게");
            Assert.AreEqual("골드 8 이상", snap.Options[0].Condition);
            Assert.AreEqual("HP 5 이상", snap.Options[1].Condition);
            Assert.AreEqual("", snap.Options[2].Condition);
            Assert.AreEqual("금으로 산다 (골드 -8, 카드 3택1)", snap.Options[0].Label);
        }

        [Test]
        public void Choose_DisabledOption_IsRejected_AndRunUntouched()
        {
            var run = NewRun(1);
            var s = Open(run, "EVT_WANDER_MERCHANT");
            Assert.IsFalse(s.Choose(1), "골드 0");
            Assert.IsFalse(s.Choose(0));
            Assert.IsFalse(s.Choose(4));
            Assert.AreEqual(EventScreenPhase.Choosing, s.Phase);
            run.CurrentHp = 4;
            Assert.IsFalse(s.Choose(2), "HP 4");
            Assert.AreEqual((4, 0, 10), (run.CurrentHp, run.Gold, run.Deck.Count));
        }

        [Test]
        public void Shrine_1_Heal15_ShowsResultThenContinueFiresOnce()
        {
            var run = NewRun(1);
            run.CurrentHp = 60;
            EventScreenResult got = null;
            int calls = 0;
            var s = Open(run, "EVT_FORGOTTEN_SHRINE", r => { got = r; calls++; });

            Assert.IsFalse(s.Continue(), "결과 전에는 계속할 수 없다");
            Assert.IsTrue(s.Choose(1));
            Assert.AreEqual(EventScreenPhase.Result, s.Phase, "대상 고르기가 없으면 바로 확정");
            var r = s.Capture().Result;
            Assert.AreEqual((60, 75, 15), (r.HpBefore, r.HpAfter, r.HpDelta), "60 + 15 = 75 (최대 80 이하)");
            Assert.AreEqual((0, 0), (r.GoldDelta, r.MaxHpDelta));
            Assert.AreEqual("오래전 버린 기도가 입에서 흘러나온다. 몸이 조금 가벼워진다.", r.Text);
            Assert.AreEqual(75, run.CurrentHp);
            Assert.AreEqual(0, calls);

            Assert.IsTrue(s.Continue());
            Assert.IsFalse(s.Continue(), "두 번째는 무시");
            Assert.AreEqual(1, calls);
            Assert.AreSame(r, got);
            Assert.AreEqual("F1-N3", got.NodeId);
            Assert.AreEqual(RunOutcome.InProgress, got.RunOutcome);
        }

        [Test]
        public void Shrine_1_HealCappedAtMax()
        {
            var run = NewRun(1);
            run.CurrentHp = 72;
            var s = Open(run, "EVT_FORGOTTEN_SHRINE");
            s.Choose(1);
            Assert.AreEqual((72, 80, 8), (s.Result.HpBefore, s.Result.HpAfter, s.Result.HpDelta), "72 + 15 → 최대 80");
        }

        [Test]
        public void CursedChest_2_Stage1Hp6_Stage2Gold40_WithMatchingText()
        {
            var a = NewRun(1);
            a.CurrentHp = 50;
            var sa = Open(a, "EVT_CURSED_CHEST");
            Assert.AreEqual("사슬을 풀고 숨을 고른다 (HP +6)", sa.Capture().Options[1].Label);
            sa.Choose(2);
            Assert.AreEqual((50, 56, 0), (sa.Result.HpBefore, sa.Result.HpAfter, sa.Result.GoldDelta));
            Assert.AreEqual("사슬이 풀리자 숨을 고를 틈이 생긴다.", sa.Result.Text);

            var b = NewRun(2);
            b.Stage1Build = false;
            var sb = Open(b, "EVT_CURSED_CHEST");
            Assert.AreEqual("사슬만 풀어 판다 (골드 40)", sb.Capture().Options[1].Label);
            sb.Choose(2);
            Assert.AreEqual((0, 40, 0), (sb.Result.GoldBefore, sb.Result.GoldAfter, sb.Result.HpDelta));
            Assert.AreEqual("녹슨 사슬도 지옥에서는 값이 나간다.", sb.Result.Text);
        }

        [Test]
        public void Option3_EveryEvent_ChangesNothing()
        {
            foreach (var id in EventFlow.Implemented)
            {
                int floor = Data.Events.Floor(1).Any(e => e.id == id) ? 1 : 2;
                var run = NewRun(floor);
                run.CurrentHp = 50;
                run.Gold = 20;
                var s = Open(run, id);
                Assert.IsTrue(s.Choose(3), id);
                Assert.AreEqual(EventScreenPhase.Result, s.Phase, id);
                Assert.AreEqual((50, 80, 20, 10), (run.CurrentHp, run.MaxHp, run.Gold, run.Deck.Count), id);
                Assert.AreEqual((0, 0, 0), (s.Result.HpDelta, s.Result.GoldDelta, s.Result.CardsGained.Length), id);
                Assert.IsNull(s.Result.RelicGained, id);
                Assert.IsFalse(s.Result.ForcedBattle, id);
            }
        }

        [Test]
        public void Merchant_1_PicksCardThenConfirm_Gold8Paid()
        {
            var run = NewRun(1);
            run.Gold = 9;
            var s = Open(run, "EVT_WANDER_MERCHANT");

            Assert.IsTrue(s.Choose(1));
            Assert.AreEqual(EventScreenPhase.Picking, s.Phase);
            var snap = s.Capture();
            Assert.IsTrue(snap.NeedsCard);
            Assert.AreEqual(3, snap.CardChoices.Length);
            Assert.IsTrue(snap.CanConfirm, "카드는 건너뛸 수 있다 (D-75)");
            Assert.AreEqual(9, run.Gold, "확정 전에는 런이 바뀌지 않는다");

            Assert.IsFalse(s.SelectCard(3));
            Assert.IsTrue(s.SelectCard(1));
            Assert.AreEqual(1, s.Capture().SelectedCard);
            Assert.IsTrue(s.Confirm());
            var r = s.Result;
            Assert.AreEqual((9, 1, -8), (r.GoldBefore, r.GoldAfter, r.GoldDelta));
            CollectionAssert.AreEqual(new[] { snap.CardChoices[1] }, r.CardsGained);
            Assert.AreEqual(11, run.Deck.Count);
            Assert.AreEqual(snap.CardChoices[1], run.Deck.Last().CardId);
        }

        [Test]
        public void Merchant_1_Skip_PaysGoldButGainsNoCard()
        {
            var run = NewRun(1);
            run.Gold = 9;
            var s = Open(run, "EVT_WANDER_MERCHANT");
            s.Choose(1);
            Assert.IsTrue(s.SelectCard(null));
            Assert.IsTrue(s.Confirm());
            Assert.AreEqual((1, 10, 0), (run.Gold, run.Deck.Count, s.Result.CardsGained.Length));
        }

        [Test]
        public void Merchant_2_Hp4_GivesOneCardWithoutPicking()
        {
            var run = NewRun(1);
            var s = Open(run, "EVT_WANDER_MERCHANT");
            Assert.IsTrue(s.Choose(2));
            Assert.AreEqual(EventScreenPhase.Result, s.Phase, "카드 1장은 고르지 않고 받는다");
            Assert.AreEqual((80, 76, 1), (s.Result.HpBefore, s.Result.HpAfter, s.Result.CardsGained.Length));
            Assert.AreEqual(11, run.Deck.Count);
        }

        [Test]
        public void Back_FromPicking_ReturnsToChoosing_RunUntouched()
        {
            var run = NewRun(1);
            run.Gold = 9;
            var s = Open(run, "EVT_WANDER_MERCHANT");
            s.Choose(1);
            Assert.IsTrue(s.Back());
            Assert.AreEqual(EventScreenPhase.Choosing, s.Phase);
            Assert.AreEqual((80, 9, 10), (run.CurrentHp, run.Gold, run.Deck.Count));
            Assert.IsFalse(s.Back());
            Assert.IsTrue(s.Choose(3));
        }

        [Test]
        public void BloodAltar_1_Lose20_ChosenCardUpgraded()
        {
            var run = NewRun(1);
            var s = Open(run, "EVT_BLOOD_ALTAR");
            Assert.IsTrue(s.Choose(1));
            var snap = s.Capture();
            Assert.IsTrue(snap.NeedsUpgrade);
            Assert.IsFalse(snap.NeedsSacrifice);
            Assert.IsFalse(snap.CanConfirm, "강화할 카드를 골라야 한다");
            Assert.IsFalse(s.Confirm());
            Assert.IsFalse(s.SelectUpgrade(999), "후보에 없는 개체");
            var target = run.Deck.First(c => c.CardId == "NEC_SKL_003");
            Assert.IsTrue(snap.UpgradeCandidates.Any(c => c.InstanceId == target.InstanceId));
            Assert.IsTrue(s.SelectUpgrade(target.InstanceId));
            Assert.IsTrue(s.Confirm());
            Assert.AreEqual((80, 60, -20), (s.Result.HpBefore, s.Result.HpAfter, s.Result.HpDelta));
            CollectionAssert.AreEqual(new[] { "NEC_SKL_003" }, s.Result.UpgradedCardIds);
            Assert.IsTrue(target.Upgraded);
        }

        [Test]
        public void BloodAltar_1_DisabledAtHp20()
        {
            var run = NewRun(1);
            run.CurrentHp = 20;
            var s = Open(run, "EVT_BLOOD_ALTAR");
            Assert.IsFalse(s.Capture().Options[0].Enabled, "HP 21 이상 (D-51)");
            Assert.IsFalse(s.Choose(1));
        }

        [Test]
        public void Shrine_2_SacrificeThenUpgrade_RemovedCardIsNotAnUpgradeCandidate()
        {
            var run = NewRun(1);
            var s = Open(run, "EVT_FORGOTTEN_SHRINE");
            Assert.IsTrue(s.Choose(2));
            var victim = run.Deck.First(c => c.CardId == "NEC_MON_003");
            var up = run.Deck.First(c => c.CardId == "NEC_ACT_001");

            Assert.IsTrue(s.Capture().NeedsSacrifice && s.Capture().NeedsUpgrade);
            Assert.IsTrue(s.SelectUpgrade(victim.InstanceId), "고르는 순서는 자유");
            Assert.IsTrue(s.SelectSacrifice(victim.InstanceId));
            Assert.AreEqual(-1, s.Capture().SelectedUpgrade, "제거할 카드로 정하면 강화 선택이 풀린다");
            Assert.IsFalse(s.Capture().UpgradeCandidates.Any(c => c.InstanceId == victim.InstanceId));
            Assert.IsFalse(s.SelectUpgrade(victim.InstanceId));
            Assert.IsFalse(s.Confirm());
            Assert.IsTrue(s.SelectUpgrade(up.InstanceId));
            Assert.IsTrue(s.Confirm());

            Assert.AreEqual("NEC_MON_003", s.Result.SacrificedCardId);
            CollectionAssert.AreEqual(new[] { "NEC_ACT_001" }, s.Result.UpgradedCardIds);
            Assert.AreEqual(9, run.Deck.Count);
            Assert.IsFalse(run.Deck.Contains(victim));
            Assert.IsTrue(up.Upgraded);
        }

        [Test]
        public void BloodAltar_2_SacrificeOnly_OneRandomUpgrade()
        {
            var run = NewRun(1, seed: 6);
            var s = Open(run, "EVT_BLOOD_ALTAR");
            Assert.IsTrue(s.Choose(2));
            Assert.IsTrue(s.Capture().NeedsSacrifice);
            Assert.IsFalse(s.Capture().NeedsUpgrade, "강화 카드는 무작위");
            var victim = run.Deck.First(c => c.CardId == "NEU_MON_001");
            Assert.IsTrue(s.SelectSacrifice(victim.InstanceId));
            Assert.IsTrue(s.Confirm());
            Assert.AreEqual(9, run.Deck.Count);
            Assert.AreEqual(1, s.Result.UpgradedCardIds.Length);
            Assert.AreEqual(1, run.Deck.Count(c => c.Upgraded));
        }

        [Test]
        public void DevilPact_1_MaxHpMinus10_ThreeCards_Skippable()
        {
            var run = NewRun(1);
            var s = Open(run, "EVT_DEVIL_PACT");
            Assert.IsTrue(s.Choose(1));
            Assert.AreEqual(3, s.Capture().CardChoices.Length);
            Assert.IsTrue(s.Confirm(), "건너뛰기");
            var r = s.Result;
            Assert.AreEqual((80, 70, -10), (r.MaxHpBefore, r.MaxHpAfter, r.MaxHpDelta));
            Assert.AreEqual((80, 70), (r.HpBefore, r.HpAfter), "현재 HP도 새 상한까지");
            Assert.AreEqual(0, r.CardsGained.Length);
            Assert.AreEqual("계약서가 살 속으로 녹아든다. 몸은 가벼워지고, 손에 새 힘이 쥐어진다.", r.Text);
        }

        [Test]
        public void DevilPact_2_CurseThenRelicMustBePicked()
        {
            var run = NewRun(1);
            var s = Open(run, "EVT_DEVIL_PACT", null);
            Assert.IsTrue(s.Choose(2));
            var snap = s.Capture();
            Assert.IsTrue(snap.NeedsRelic);
            CollectionAssert.AreEqual(new[] { "REL_COM_001", "REL_COM_002", "REL_COM_003" }, snap.RelicChoices);
            Assert.IsFalse(snap.CanConfirm, "유물은 반드시 1개");
            Assert.IsFalse(s.Confirm());
            Assert.IsFalse(s.SelectRelic(3));
            Assert.IsTrue(s.SelectRelic(2));
            Assert.IsTrue(s.Confirm());
            var r = s.Result;
            Assert.AreEqual("REL_COM_003", r.RelicGained);
            Assert.AreEqual(1, r.CursesGained.Length);
            CollectionAssert.Contains(new[] { "CUR_002", "CUR_004" }, r.CursesGained[0], "D-45");
            Assert.AreEqual(11, run.Deck.Count);
            CollectionAssert.Contains(run.Relics, "REL_COM_003");
        }

        [Test]
        public void DevilPact_2_CurseNameFillsResultText()
        {
            var run = NewRun(1);
            var s = new EventScreenSession(1, "EVT_DEVIL_PACT", run, Data, null, id => "<" + id + ">");
            s.Choose(2);
            s.SelectRelic(0);
            s.Confirm();
            Assert.AreEqual("그림자가 웃는다. '<" + s.Result.CursesGained[0] + ">. 오래 지니게 될 거다.' 대가로 내민 상자에서 빛이 샌다.", s.Result.Text);
        }

        [Test]
        public void DarkWager_1_Floor2_WinHp18_OrLoseHp12_BySeed()
        {
            int wins = 0, losses = 0;
            for (long seed = 1; seed <= 40; seed++)
            {
                var run = NewRun(2, seed);
                run.CurrentHp = 50;
                var rng = new BattleRandom(run.NodeSeed);
                rng.NextFloat();
                bool win = rng.Chance(0.5);
                var s = Open(run, "EVT_DARK_WAGER");
                Assert.IsTrue(s.Choose(1));
                Assert.AreEqual(win ? 68 : 38, run.CurrentHp, "50 + 18 / 50 - 12, seed " + seed);
                Assert.AreEqual(0, s.Result.GoldDelta, "1단계 빌드는 골드가 아니라 HP");
                Assert.AreEqual(win ? "해골이 내민 잔을 마시자 상처가 아문다." : "주사위가 등을 돌린다. 해골이 뼈 칼로 값을 받아 간다.", s.Result.Text);
                if (win) wins++; else losses++;
            }
            Assert.Greater(wins, 0);
            Assert.Greater(losses, 0);
        }

        [Test]
        public void DarkWager_1_Stage2Build_WinGivesGold25()
        {
            for (long seed = 1; seed <= 40; seed++)
            {
                var run = NewRun(2, seed);
                run.Stage1Build = false;
                var rng = new BattleRandom(run.NodeSeed);
                rng.NextFloat();
                if (!rng.Chance(0.5)) continue;
                var s = Open(run, "EVT_DARK_WAGER");
                s.Choose(1);
                Assert.AreEqual((0, 25, 0), (s.Result.GoldBefore, s.Result.GoldAfter, s.Result.HpDelta));
                Assert.AreEqual("해골이 금화를 밀어 준다.", s.Result.Text);
                return;
            }
            Assert.Fail("이길 시드가 없다");
        }

        [Test]
        public void DarkWager_1_LossAtHp12_DefeatsTheRun_D51()
        {
            bool sawLoss = false;
            for (long seed = 1; seed <= 40 && !sawLoss; seed++)
            {
                var run = NewRun(2, seed);
                run.CurrentHp = 12;
                var rng = new BattleRandom(run.NodeSeed);
                rng.NextFloat();
                if (rng.Chance(0.5)) continue;
                EventScreenResult got = null;
                var s = Open(run, "EVT_DARK_WAGER", r => got = r);
                s.Choose(1);
                Assert.AreEqual((12, 0, RunOutcome.Defeat), (s.Result.HpBefore, s.Result.HpAfter, s.Result.RunOutcome));
                Assert.AreEqual(RunOutcome.Defeat, run.Outcome);
                s.Continue();
                Assert.AreEqual(RunOutcome.Defeat, got.RunOutcome);
                sawLoss = true;
            }
            Assert.IsTrue(sawLoss);
        }

        [Test]
        public void SoulPrison_1_ForcedBattleIsReportedNotRun_OtherwiseThreeCards()
        {
            int battles = 0, cards = 0;
            for (long seed = 1; seed <= 40; seed++)
            {
                var run = NewRun(1, seed);
                var rng = new BattleRandom(run.NodeSeed);
                rng.NextFloat();
                bool reward = rng.Chance(0.5);
                EventScreenResult got = null;
                var s = Open(run, "EVT_SOUL_PRISON", r => got = r);
                Assert.IsTrue(s.Choose(1));
                if (reward)
                {
                    cards++;
                    Assert.AreEqual(EventScreenPhase.Picking, s.Phase, "카드 3택1");
                    Assert.AreEqual(3, s.Capture().CardChoices.Length);
                    Assert.IsTrue(s.Confirm());
                    Assert.IsFalse(s.Result.ForcedBattle);
                }
                else
                {
                    battles++;
                    Assert.AreEqual(EventScreenPhase.Result, s.Phase);
                    Assert.IsTrue(s.Result.ForcedBattle, "맵이 RunBattle.StartForced로 전투를 연다");
                    Assert.AreEqual("우리가 부서지자 어둠 속의 간수가 몸을 일으킨다.", s.Result.Text);
                    Assert.AreEqual((80, 10), (run.CurrentHp, run.Deck.Count), "전투 전에는 아무것도 바뀌지 않는다");
                    s.Continue();
                    Assert.IsTrue(got.ForcedBattle);
                }
            }
            Assert.Greater(battles, 0);
            Assert.Greater(cards, 0);
        }

        [Test]
        public void SoulPrison_2_Heal12()
        {
            var run = NewRun(1);
            run.CurrentHp = 60;
            var s = Open(run, "EVT_SOUL_PRISON");
            s.Choose(2);
            Assert.AreEqual((60, 72), (s.Result.HpBefore, s.Result.HpAfter));
        }

        [Test]
        public void ConfirmAndChoose_AfterResult_AreIgnored()
        {
            var run = NewRun(1);
            run.CurrentHp = 50;
            var s = Open(run, "EVT_SOUL_PRISON");
            s.Choose(2);
            Assert.IsFalse(s.Choose(2));
            Assert.IsFalse(s.Confirm());
            Assert.AreEqual(62, run.CurrentHp, "한 번만 적용");
        }
    }
}
