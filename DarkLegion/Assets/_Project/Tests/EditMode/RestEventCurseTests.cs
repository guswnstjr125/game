using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 휴식·이벤트·저주 (#44, #76 2-3): `03` §2, `24` §2, `06` RestNode_Rules·Events_MVP·Event_FloorPool, `08` Curse_Cards,
    /// D-45·D-51·D-68·D-69·D-75·D-77. PROG-06·EVT-01. 기대값은 손으로 적는다.
    /// </summary>
    [Category("Stage1")]
    public class RestEventCurseTests
    {
        private static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        private static RunState Run(long seed = 1)
        {
            return RunFlow.NewRun("RUN-E", seed, "NEC", 80, StageOneBattleSim.NecStartDeck, new[] { "REL_BLOOD_CHALICE" });
        }

        private static void GoTo(RunState run, int floor, int node)
        {
            run.Floor = floor;
            run.StageIndex = node - 1;
        }

        // ---------- 휴식 (D-69·D-75) ----------

        [Test]
        public void Rest_HealFloor30Percent_CappedAtMax()
        {
            var run = Run();
            run.CurrentHp = 40;
            var o = RestFlow.Options(run, Data);
            Assert.AreEqual((true, 24, true), (o.CanHeal, o.HealAmount, o.CanUpgrade), "floor(80 × 0.3) = 24");
            Assert.IsTrue(RestFlow.Heal(run, Data));
            Assert.AreEqual(64, run.CurrentHp);
            run.CurrentHp = 70;
            RestFlow.Heal(run, Data);
            Assert.AreEqual(80, run.CurrentHp, "MaxHP까지");
            run.MaxHp = 85;
            run.CurrentHp = 10;
            Assert.AreEqual(25, RestFlow.Options(run, Data).HealAmount, "HOL 85 → floor(25.5) = 25 (D-69)");
        }

        [Test]
        public void Rest_UpgradeChosenCard_OnlyUpgradable()
        {
            var run = Run();
            var target = run.Deck.First(c => c.CardId == "NEC_MON_001");
            Assert.IsTrue(RestFlow.Upgrade(run, Data, target.InstanceId));
            Assert.IsTrue(target.Upgraded);
            Assert.IsFalse(RestFlow.Upgrade(run, Data, target.InstanceId), "이미 강화한 카드");
            Assert.IsFalse(RestFlow.Upgrade(run, Data, 999), "없는 개체");
        }

        [Test]
        public void Rest_NeitherAvailable_ContinueOnly()
        {
            var run = Run();
            foreach (var c in run.Deck) c.Upgraded = true;
            var o = RestFlow.Options(run, Data);
            Assert.AreEqual((false, false, true), (o.CanHeal, o.CanUpgrade, o.ContinueOnly), "HP 가득 + 강화할 카드 없음 → [계속]만 (D-69)");
            Assert.IsFalse(RestFlow.Heal(run, Data));
        }

        [Test]
        public void Upgradable_CursesNever_D75()
        {
            var run = Run();
            var curse = run.AddCard("CUR_002");
            Assert.IsFalse(RunDeckRules.IsUpgradable(curse, Data.Cards));
            Assert.IsTrue(RunDeckRules.IsUpgradable(run.Deck[0], Data.Cards));
        }

        // ---------- 이벤트 추첨 (D-69, PROG-06) ----------

        [Test]
        public void PROG_06_FloorPoolsAndUnion7()
        {
            var f1 = Data.Events.Floor(1).Select(e => e.id).ToList();
            var f2 = Data.Events.Floor(2).Select(e => e.id).ToList();
            Assert.AreEqual(6, f1.Count, "층마다 활성 6종");
            Assert.AreEqual(6, f2.Count);
            Assert.AreEqual(1.0, Data.Events.Floor(1).Sum(e => e.weight), 1e-9, "공통 14% × 4 + 신규 22% × 2");
            CollectionAssert.AreEquivalent(Stage1Scope.Events.ToArray(), f1.Union(f2).ToArray(), "1~2층 합집합 7종");
            CollectionAssert.DoesNotContain(f1, "EVT_DARK_WAGER");
            CollectionAssert.DoesNotContain(f2, "EVT_WANDER_MERCHANT");
        }

        [Test]
        public void Enter_FirstValuePicks_Saved_NoRepeatInRun_D69()
        {
            var run = Run(seed: 4);
            GoTo(run, 1, 3);
            string first = EventFlow.Enter(run, Data);
            Assert.AreEqual(first, run.NodeEventIds["F1-N3"]);
            Assert.AreEqual(first, EventFlow.Enter(run, Data), "저장된 이벤트를 다시 연다");

            // 첫 값으로 가중치 재정규화 추첨 (손으로 같은 계산)
            var pool = Data.Events.Floor(1);
            double x = new BattleRandom(Seeds.NodeSeed(4, 1, 2)).NextFloat() * pool.Sum(e => e.weight);
            string expected = pool.Last().id;
            foreach (var (id, w) in pool)
            {
                if (x < w) { expected = id; break; }
                x -= w;
            }
            Assert.AreEqual(expected, first);

            GoTo(run, 1, 6);
            Assert.AreNotEqual(first, EventFlow.Enter(run, Data), "이번 런에서 나온 이벤트는 뺀다");
        }

        [Test]
        public void Enter_ManySeeds_NeverRepeatsWithinRun()
        {
            for (long seed = 1; seed <= 200; seed++)
            {
                var run = Run(seed);
                var seen = new List<string>();
                foreach (var (f, n) in new[] { (1, 3), (1, 6), (2, 3), (2, 6) })
                {
                    GoTo(run, f, n);
                    seen.Add(EventFlow.Enter(run, Data));
                }
                Assert.AreEqual(4, seen.Distinct().Count(), $"seed {seed}");
            }
        }

        // ---------- 이벤트 선택지 (`24` §2, D-68·D-75·D-77) ----------

        private static EventOutcome Choose(RunState run, string eventId, int option)
        {
            run.NodeEventIds[run.CurrentNodeId] = eventId;
            return EventFlow.Choose(run, Data, eventId, option);
        }

        private static RunState AtEvent(long seed = 1, int floor = 1, int node = 3)
        {
            var run = Run(seed);
            GoTo(run, floor, node);
            return run;
        }

        [Test]
        public void DevilPact_1_MaxHpMinus10_CurrentClamped_ChooseCardOrSkip()
        {
            var run = AtEvent();
            Assert.IsTrue(EventFlow.IsEnabled(run, Data, "EVT_DEVIL_PACT", 1), "MaxHP 80 > 20");
            var o = Choose(run, "EVT_DEVIL_PACT", 1);
            Assert.AreEqual(3, o.CardChoices.Count);
            Assert.IsTrue(o.CardChooseOne);
            Assert.IsTrue(EventFlow.Confirm(run, Data, o, new EventChoice { CardIndex = null }), "건너뛰기 (D-75)");
            Assert.AreEqual((70, 70), (run.MaxHp, run.CurrentHp), "80 → 70, 현재 HP도 70");
            Assert.AreEqual(10, run.Deck.Count);

            var low = AtEvent();
            low.MaxHp = 20;
            low.CurrentHp = 20;
            Assert.IsFalse(EventFlow.IsEnabled(low, Data, "EVT_DEVIL_PACT", 1), "MaxHP > 20일 때만 (D-68)");
        }

        [Test]
        public void EVT_01_DevilPact_2_CurseFrom002Or004_ThenRelicMustPick()
        {
            var seenCurses = new HashSet<string>();
            for (long seed = 1; seed <= 60; seed++)
            {
                var run = AtEvent(seed);
                var o = Choose(run, "EVT_DEVIL_PACT", 2);
                Assert.AreEqual(1, o.CursesToAdd.Count);
                seenCurses.Add(o.CursesToAdd[0]);
                // D-77: 이벤트 결정 다음 첫 값으로 저주
                var rng = new BattleRandom(run.NodeSeed);
                rng.NextFloat();
                Assert.AreEqual(rng.Next(0, 2) == 0 ? "CUR_002" : "CUR_004", o.CursesToAdd[0]);
                CollectionAssert.AreEqual(new[] { "REL_COM_001", "REL_COM_002", "REL_COM_003" }, o.RelicChoices);
                Assert.IsFalse(EventFlow.Confirm(run, Data, o, new EventChoice()), "유물은 하나 골라야 한다");
                Assert.IsTrue(EventFlow.Confirm(run, Data, o, new EventChoice { RelicIndex = 2 }));
                CollectionAssert.Contains(run.Relics, "REL_COM_003");
                Assert.AreEqual(o.CursesToAdd[0], run.Deck.Last().CardId, "저주는 새 개체로 런 덱에");
            }
            CollectionAssert.AreEquivalent(new[] { "CUR_002", "CUR_004" }, seenCurses.ToArray());
        }

        [Test]
        public void DevilPact_2_DisabledWhenAllOwned()
        {
            var run = AtEvent();
            run.Relics.AddRange(new[] { "REL_COM_001", "REL_COM_002", "REL_COM_003" });
            Assert.IsFalse(EventFlow.IsEnabled(run, Data, "EVT_DEVIL_PACT", 2));
        }

        [Test]
        public void BloodAltar_1_Lose20_UpgradeChosen_NeedsHpOver20AndUpgradable()
        {
            var run = AtEvent();
            var o = Choose(run, "EVT_BLOOD_ALTAR", 1);
            var target = run.Deck.First(c => c.CardId == "NEC_SKL_003");
            Assert.IsFalse(EventFlow.Confirm(run, Data, o, new EventChoice()), "강화할 카드를 골라야 한다");
            Assert.IsTrue(EventFlow.Confirm(run, Data, o, new EventChoice { UpgradeInstanceId = target.InstanceId }));
            Assert.AreEqual(60, run.CurrentHp);
            Assert.IsTrue(target.Upgraded);

            var low = AtEvent();
            low.CurrentHp = 20;
            Assert.IsFalse(EventFlow.IsEnabled(low, Data, "EVT_BLOOD_ALTAR", 1), "HP > 20 (D-51)");
        }

        [Test]
        public void BloodAltar_2_MapSacrifice_RemovesChosenMonster_RandomUpgrade()
        {
            var run = AtEvent(seed: 6);
            var o = Choose(run, "EVT_BLOOD_ALTAR", 2);
            Assert.IsTrue(o.NeedsSacrificeChoice);
            var victim = run.Deck.First(c => c.CardId == "NEU_MON_001");
            Assert.IsTrue(EventFlow.Confirm(run, Data, o, new EventChoice { SacrificeInstanceId = victim.InstanceId }));
            Assert.IsFalse(run.Deck.Contains(victim));
            Assert.AreEqual(9, run.Deck.Count);
            Assert.AreEqual(1, run.Deck.Count(c => c.Upgraded), "남은 강화 가능 카드 중 무작위 1장");
            // 시작 덱 몬스터 6장(뼈 병사·구울·해골·역병 쥐·파편·용병): 5장 남아 3장 이상 → 가능
        }

        [Test]
        public void MapSacrifice_NotSoulbound_KeepsThreeMonsters_D68()
        {
            var run = RunFlow.NewRun("RUN-S", 1, "NEC", 80, new[] { "NEC_MON_001", "NEC_MON_002", "NEC_MON_003", "NEU_MON_004", "NEC_SKL_003" }, new string[0]);
            // 몬스터 4장(보급 마차는 Soulbound) → 제거 후 3장 남음: 마차 제외 3장이 후보
            var cands = RunDeckRules.MapSacrificeCandidates(run, Data.Cards).Select(c => c.CardId).ToList();
            CollectionAssert.AreEquivalent(new[] { "NEC_MON_001", "NEC_MON_002", "NEC_MON_003" }, cands);
            run.Deck.RemoveAll(c => c.CardId == "NEC_MON_003");
            Assert.IsEmpty(RunDeckRules.MapSacrificeCandidates(run, Data.Cards), "제거하면 2장만 남는다");
        }

        [Test]
        public void EVT_01_CursedChest_1_RandomUnownedRelic_Then40PercentCur003()
        {
            int curses = 0, n = 400;
            for (long seed = 1; seed <= n; seed++)
            {
                var run = AtEvent(seed);
                run.Relics.Add("REL_COM_002");
                var o = Choose(run, "EVT_CURSED_CHEST", 1);
                var rng = new BattleRandom(run.NodeSeed);
                rng.NextFloat();
                string expected = new[] { "REL_COM_001", "REL_COM_003" }[rng.Next(0, 2)];
                bool curse = rng.Chance(0.4);
                Assert.AreEqual(expected, o.AutoRelic, "D-77: 유물 먼저");
                CollectionAssert.AreEqual(curse ? new[] { "CUR_003" } : new string[0], o.CursesToAdd, "그다음 40%");
                if (curse) curses++;
            }
            Assert.AreEqual(0.4, curses / (double)n, 0.07);
        }

        [Test]
        public void CursedChest_2_Stage1Heal6()
        {
            var run = AtEvent();
            run.CurrentHp = 50;
            var o = Choose(run, "EVT_CURSED_CHEST", 2);
            EventFlow.Confirm(run, Data, o, new EventChoice());
            Assert.AreEqual(56, run.CurrentHp);
        }

        [Test]
        public void SoulPrison_1_HalfCardsHalfForcedBattle_ForcedBattleIsNode1AWave1AtEventScale()
        {
            int battles = 0;
            for (long seed = 1; seed <= 100; seed++)
            {
                var run = AtEvent(seed, floor: 1, node: 6);
                var o = Choose(run, "EVT_SOUL_PRISON", 1);
                var rng = new BattleRandom(run.NodeSeed);
                rng.NextFloat();
                bool cards = rng.Chance(0.5);
                Assert.AreEqual(!cards, o.ForcedBattle);
                Assert.AreEqual(cards ? 3 : 0, o.CardChoices.Count);
                if (!o.ForcedBattle) continue;
                battles++;
                Assert.IsTrue(EventFlow.Confirm(run, Data, o, new EventChoice()));
                var ctx = RunBattle.StartForced(run, Data);
                // 1층 노드 1 A/Wave 1 = 구울 ×2, 이벤트 위치(노드 6) 스테이지 인덱스 5: HP 14 × 1.115 = 15.61 → 16
                Assert.AreEqual(2, ctx.Enemies.Count);
                Assert.AreEqual(16, ctx.Enemies[0].MaxHP.FinalValue);
                Assert.AreEqual(0, ctx.PendingWaveCount);
            }
            Assert.That(battles, Is.InRange(30, 70));
        }

        [Test]
        public void SoulPrison_2_Heal12_Shrine_1_Heal15_Capped()
        {
            var run = AtEvent();
            run.CurrentHp = 60;
            EventFlow.Confirm(run, Data, Choose(run, "EVT_SOUL_PRISON", 2), new EventChoice());
            Assert.AreEqual(72, run.CurrentHp);
            EventFlow.Confirm(run, Data, Choose(run, "EVT_FORGOTTEN_SHRINE", 1), new EventChoice());
            Assert.AreEqual(80, run.CurrentHp, "72 + 15 → 최대 80");
        }

        [Test]
        public void Merchant_1_Gold8Choose_2_Hp4RandomOne()
        {
            var run = AtEvent();
            Assert.IsFalse(EventFlow.IsEnabled(run, Data, "EVT_WANDER_MERCHANT", 1), "골드 0 < 8");
            run.Gold = 9;
            var o1 = Choose(run, "EVT_WANDER_MERCHANT", 1);
            Assert.IsTrue(EventFlow.Confirm(run, Data, o1, new EventChoice { CardIndex = 1 }));
            Assert.AreEqual((1, 11), (run.Gold, run.Deck.Count));

            var run2 = AtEvent();
            var o2 = Choose(run2, "EVT_WANDER_MERCHANT", 2);
            Assert.AreEqual(1, o2.CardChoices.Count);
            Assert.IsFalse(o2.CardChooseOne, "count=1은 고르지 않고 받는다");
            Assert.IsTrue(EventFlow.Confirm(run2, Data, o2, new EventChoice()));
            Assert.AreEqual((76, 11, o2.CardChoices[0]), (run2.CurrentHp, run2.Deck.Count, run2.Deck.Last().CardId));
            run2.CurrentHp = 4;
            Assert.IsFalse(EventFlow.IsEnabled(run2, Data, "EVT_WANDER_MERCHANT", 2), "HP 5 이상 (D-68)");
        }

        [Test]
        public void Shrine_2_SacrificeThenChosenUpgrade_NotTheRemovedCard()
        {
            var run = AtEvent();
            var o = Choose(run, "EVT_FORGOTTEN_SHRINE", 2);
            var victim = run.Deck.First(c => c.CardId == "NEC_MON_003");
            Assert.IsFalse(EventFlow.Confirm(run, Data, o, new EventChoice { SacrificeInstanceId = victim.InstanceId, UpgradeInstanceId = victim.InstanceId }), "제거한 카드는 강화할 수 없다");
            var up = run.Deck.First(c => c.CardId == "NEC_ACT_001");
            Assert.IsTrue(EventFlow.Confirm(run, Data, o, new EventChoice { SacrificeInstanceId = victim.InstanceId, UpgradeInstanceId = up.InstanceId }));
            Assert.IsTrue(up.Upgraded);
            Assert.AreEqual(9, run.Deck.Count);
        }

        [Test]
        public void EVT_01_DarkWager_1_Win18OrLose12_DeathEndsRun_2_CardOrCur002()
        {
            bool sawWin = false, sawLose = false, sawCard = false, sawCurse = false;
            for (long seed = 1; seed <= 60; seed++)
            {
                var run = AtEvent(seed, floor: 2, node: 3);
                run.CurrentHp = 50;
                var o = Choose(run, "EVT_DARK_WAGER", 1);
                EventFlow.Confirm(run, Data, o, new EventChoice());
                Assert.That(run.CurrentHp, Is.EqualTo(68).Or.EqualTo(38), "1단계 승리 HP +18 / 패배 −12");
                sawWin |= run.CurrentHp == 68;
                sawLose |= run.CurrentHp == 38;

                var run2 = AtEvent(seed, floor: 2, node: 3);
                var o2 = Choose(run2, "EVT_DARK_WAGER", 2);
                if (o2.CursesToAdd.Count > 0)
                {
                    CollectionAssert.AreEqual(new[] { "CUR_002" }, o2.CursesToAdd);
                    Assert.IsEmpty(o2.CardChoices);
                    sawCurse = true;
                }
                else
                {
                    Assert.AreEqual(1, o2.CardChoices.Count);
                    sawCard = true;
                }
            }
            Assert.IsTrue(sawWin && sawLose && sawCard && sawCurse);

            for (long seed = 1; ; seed++)
            {
                var run = AtEvent(seed, floor: 2, node: 3);
                run.CurrentHp = 12;
                var o = Choose(run, "EVT_DARK_WAGER", 1);
                if (o.HpDelta > 0) continue;
                EventFlow.Confirm(run, Data, o, new EventChoice());
                Assert.AreEqual((0, RunOutcome.Defeat), (run.CurrentHp, run.Outcome), "HP 0 이하 → 패배 (D-51)");
                break;
            }
        }

        [Test]
        public void Option3_Nothing_AndConfirmOnlyOnce()
        {
            var run = AtEvent();
            var o = Choose(run, "EVT_DEVIL_PACT", 3);
            Assert.IsTrue(EventFlow.Confirm(run, Data, o, new EventChoice()));
            Assert.IsFalse(EventFlow.Confirm(run, Data, o, new EventChoice()), "D-67");
            Assert.AreEqual((80, 80, 0, 10), (run.CurrentHp, run.MaxHp, run.Gold, run.Deck.Count));
        }

        // ---------- 저주 카드 (D-45·D-75) ----------

        private static BattleContext BattleWith(params string[] deck)
        {
            var run = RunFlow.NewRun("RUN-C", 1, "NEC", 80, deck, new string[0]);
            return RunBattle.Start(run, Data);
        }

        [Test]
        public void Curse_DataLoaded()
        {
            var c2 = Data.Cards.Get("CUR_002");
            Assert.AreEqual((CardType.Curse, 0, true, false), (c2.Type, c2.Cost, c2.Ethereal, c2.Exhaust));
            var c3 = Data.Cards.Get("CUR_003");
            Assert.AreEqual((CardType.Curse, 2, false, true), (c3.Type, c3.Cost, c3.Ethereal, c3.Exhaust));
        }

        [Test]
        public void EVT_01_Cur002_OnDrawBurn2ToPlayer_Unplayable_EtherealExhausts()
        {
            var ctx = BattleWith("CUR_002", "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003");
            ctx.StartPlayerTurn();
            Assert.AreEqual(2, ctx.Player.Statuses.Stacks(StatusType.Burn), "드로우 시 Burn 2");
            var curse = ctx.Player.Deck.Hand.First(c => c.CardId == "CUR_002");
            Assert.AreEqual(PlayResult.Unplayable, ctx.PlayCard(curse, null), "쓸 수 없다 (D-75)");
            ctx.EndPlayerTurn();
            Assert.AreEqual(CardZone.ExhaustPile, curse.Zone, "Ethereal");
            Assert.AreEqual(1, ctx.Player.Statuses.Stacks(StatusType.Burn), "턴 종료 틱 뒤 Burn −1 (2 → 1)");
        }

        [Test]
        public void EVT_01_Cur004_OnDrawLoseHp3_IgnoresShield()
        {
            var ctx = BattleWith("CUR_004", "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003");
            ctx.Player.Shield = 10;
            ctx.StartPlayerTurn();
            Assert.AreEqual((77, 10), (ctx.Player.CurrentHP, ctx.Player.Shield), "HP 손실 3, Shield 그대로");
        }

        [Test]
        public void Cur004_CanKill()
        {
            var ctx = BattleWith("CUR_004", "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003");
            ctx.Player.CurrentHP = 3;
            ctx.StartPlayerTurn();
            Assert.AreEqual(BattleOutcome.Defeat, ctx.Outcome);
        }

        [Test]
        public void Cur002_OverflowToDiscard_DoesNotTrigger()
        {
            var ctx = BattleWith("CUR_002");
            for (int i = 0; i < 10; i++) ctx.Player.Deck.AddToHand(Data.Cards.Get("NEC_SKL_003").CreateCard(100 + i, false));
            ctx.StartPlayerTurn();
            Assert.AreEqual(0, ctx.Player.Statuses.Stacks(StatusType.Burn), "손패 10장 → 버린 더미, 발동 없음 (D-75)");
        }

        [Test]
        public void EVT_01_Cur003_Cost2_WeakSelectedAlly_Exhaust_NeedsAlly()
        {
            var ctx = BattleWith("CUR_003", "NEC_MON_001", "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003");
            ctx.StartPlayerTurn();
            var curse = ctx.Player.Deck.Hand.First(c => c.CardId == "CUR_003");
            Assert.AreEqual(PlayResult.InvalidTarget, ctx.PlayCard(curse, null), "아군이 없으면 쓸 수 없다");
            var mon = ctx.Player.Deck.Hand.First(c => c.CardId == "NEC_MON_001");
            ctx.PlayCard(mon, null, SlotPosition.Front);
            ctx.Queue.Process();
            ctx.Player.CurrentCrystal = 2;
            var ally = ctx.Allies[0];
            Assert.AreEqual(PlayResult.Ok, ctx.PlayCard(curse, ally));
            ctx.Queue.Process();
            Assert.AreEqual(2, ally.Statuses.Stacks(StatusType.Weak), "Weak 2턴");
            Assert.AreEqual(0, ctx.Player.CurrentCrystal);
            Assert.AreEqual(CardZone.ExhaustPile, curse.Zone);
        }

        [Test]
        public void Curses_NotRewardsNotFusionNotUpgrade()
        {
            Assert.IsFalse(CardPool.Candidates(Data.Cards, 2, "NEC", NodeKind.Battle, stage1: false).Any(c => c.Type == CardType.Curse));
        }

        // ---------- 핸들러 등록 ----------

        [Test]
        public void Registry_CursesAndEventsRegistered()
        {
            var raw = StageOneBattleSim.Data();
            CollectionAssert.IsEmpty(HandlerRegistry.Missing(HandlerKind.Curse, Stage1Scope.Curses, raw));
            CollectionAssert.IsEmpty(HandlerRegistry.Missing(HandlerKind.Event, Stage1Scope.Events, raw));
        }
    }
}
