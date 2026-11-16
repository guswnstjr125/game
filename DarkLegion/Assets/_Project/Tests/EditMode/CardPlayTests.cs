using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 카드 사용 (`10` §3·§4, `08` Cards_Effect·Cards_Upgrade, `09` Monster_Effects, D-20·D-24·D-42·D-61·D-63).
    /// 1단계 스킬·액션 12장과 1단계 몬스터의 소환·사망 효과. 기대값은 문서에서 손으로 계산한다.
    /// </summary>
    [Category("Stage1")]
    public class CardPlayTests
    {
        private BattleContext _ctx;
        private int _nextId;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BattleContext(11);
            _ctx.Cards = StageOneCards.Create();
            _ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], _ctx.Random));
            _ctx.BeginTurn();
            _ctx.Player.ResetCrystalForTurn();
        }

        private RuntimeCard InHand(string cardId, bool upgraded = false)
        {
            var def = _ctx.Cards.Get(cardId);
            var card = new RuntimeCard(++_nextId, cardId, def.Cost, def.Ethereal, def.Exhaust) { Upgraded = upgraded };
            _ctx.Player.Deck.AddToHand(card);
            return card;
        }

        private RuntimeMonster Ally(string cardId, SlotPosition pos, int hp = 10)
        {
            var card = InHand(cardId);
            _ctx.Player.CurrentCrystal += card.Cost;
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(card, null, pos), cardId);
            _ctx.Queue.Process();
            var m = (RuntimeMonster)_ctx.Field.At(TeamType.Player, pos);
            m.CurrentHP = hp;
            return m;
        }

        private RuntimeEnemy Enemy(SlotPosition pos, int hp = 30, int def = 0)
        {
            var e = new RuntimeEnemy("E" + (++_nextId), "ENE_TEST", hp, 5, def, false);
            _ctx.Field.PlaceEnemy(e, pos, _ctx);
            return e;
        }

        // ---------- 공통 사용 규칙 ----------

        [Test]
        public void CARD_PlayChecksPhaseCostAndHand()
        {
            var card = InHand("NEC_SKL_003");
            _ctx.Player.CurrentCrystal = 0;
            Assert.AreEqual(PlayResult.NotEnoughCrystal, _ctx.PlayCard(card, Enemy(SlotPosition.Front)));
            Assert.AreEqual(CardZone.Hand, card.Zone);
            _ctx.Player.CurrentCrystal = 3;
            _ctx.State = BattleState.EnemyPhase;
            Assert.AreEqual(PlayResult.InvalidPhase, _ctx.PlayCard(card, _ctx.Enemies[0]));
        }

        [Test]
        public void CARD_02_UnknownCardIdFailsSafely()
        {
            var card = new RuntimeCard(999, "NOPE_000", 0);
            _ctx.Player.Deck.AddToHand(card);
            Assert.AreEqual(PlayResult.UnknownCard, _ctx.PlayCard(card, null));
            Assert.AreEqual(CardZone.Hand, card.Zone);
        }

        [Test]
        public void CARD_WrongTargetRejected_NoCost()
        {
            var ally = Ally("NEC_MON_001", SlotPosition.Front);
            var card = InHand("NEC_SKL_003");
            Assert.AreEqual(PlayResult.InvalidTarget, _ctx.PlayCard(card, ally), "적 대상 카드에 아군");
            Assert.AreEqual(3, _ctx.Player.CurrentCrystal);
        }

        [Test]
        public void SUM_MonsterCardOnFullField_RejectedWithoutCost()
        {
            Ally("NEC_MON_001", SlotPosition.Front);
            Ally("NEC_MON_001", SlotPosition.Middle);
            Ally("NEC_MON_001", SlotPosition.Back);
            var card = InHand("NEC_MON_001");
            Assert.AreEqual(PlayResult.FieldFull, _ctx.PlayCard(card, null));
            Assert.AreEqual(3, _ctx.Player.CurrentCrystal, "`09` §3: Cost 미소모");
            Assert.AreEqual(CardZone.Hand, card.Zone, "패는 Hand 유지");
        }

        [Test]
        public void SUM_MonsterCardSummonsWithCardStats_CardStaysOnField()
        {
            var card = InHand("NEC_MON_001");
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(card, null, SlotPosition.Front));
            _ctx.Queue.Process();
            var m = (RuntimeMonster)_ctx.Field.At(TeamType.Player, SlotPosition.Front);
            Assert.AreEqual(10, m.MaxHP.FinalValue);  // 뼈 병사 10/3/1 (`08`)
            Assert.AreEqual(3, m.Attack.FinalValue);
            Assert.AreEqual(1, m.Defense.FinalValue);
            Assert.AreEqual(2, _ctx.Player.CurrentCrystal);
            Assert.AreEqual(CardZone.OnField, card.Zone);
        }

        // ---------- 스킬 6장 ----------

        [Test]
        public void NEC_SKL_001_SoulExtract_DrawsWhenTargetDiesThisTurn_IncludingSacrifice()
        {
            for (int i = 0; i < 5; i++) _ctx.Player.Deck.AddToHand(new RuntimeCard(500 + i, "FILLER", 0));
            _ctx.Player.Deck.EndTurn(); // 버린 더미 5장 → 드로우할 카드
            var ally = Ally("NEC_MON_001", SlotPosition.Front);
            var hand = _ctx.Player.Deck.Hand.Count;
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(InHand("NEC_SKL_001"), ally));
            _ctx.Queue.Process();
            Assert.AreEqual(hand, _ctx.Player.Deck.Hand.Count, "아직 안 죽음");
            _ctx.Sacrifice(ally);
            _ctx.Queue.Process();
            Assert.AreEqual(hand + 2, _ctx.Player.Deck.Hand.Count, "희생 사망도 발동 (D-42)");
        }

        [Test]
        public void NEC_SKL_001_SoulExtract_ExpiresAtTurnEnd()
        {
            var ally = Ally("NEC_MON_001", SlotPosition.Front);
            _ctx.PlayCard(InHand("NEC_SKL_001"), ally);
            _ctx.Queue.Process();
            _ctx.EndTurn();
            _ctx.BeginTurn();
            _ctx.Player.ResetCrystalForTurn();
            int discard = _ctx.Player.Deck.DiscardPile.Count;
            _ctx.Sacrifice(ally);
            _ctx.Queue.Process();
            Assert.AreEqual(0, _ctx.Player.Deck.Hand.Count, "지난 턴 지정은 효과 없음");
        }

        [Test]
        public void NEC_SKL_002_DarkBarrier_Shield3All_Front5()
        {
            var front = Ally("NEC_MON_001", SlotPosition.Front);
            var back = Ally("NEC_MON_001", SlotPosition.Back);
            _ctx.PlayCard(InHand("NEC_SKL_002"), null);
            _ctx.Queue.Process();
            Assert.AreEqual(5, front.Shield);
            Assert.AreEqual(3, back.Shield);
        }

        [Test]
        public void NEC_SKL_003_DecayTouch_Poison3Weak2()
        {
            var e = Enemy(SlotPosition.Front);
            _ctx.PlayCard(InHand("NEC_SKL_003"), e);
            _ctx.Queue.Process();
            Assert.AreEqual(3, e.Statuses.Stacks(StatusType.Poison));
            Assert.AreEqual(2, e.Statuses.Stacks(StatusType.Weak));
        }

        [Test]
        public void NEC_SKL_004_GraveRob_MonsterFromDiscardToHand()
        {
            var monster = new RuntimeCard(700, "NEC_MON_001", 1);
            _ctx.Player.Deck.AddToHand(monster);
            _ctx.Player.Deck.EndTurn();
            Assert.AreEqual(CardZone.DiscardPile, monster.Zone);
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(InHand("NEC_SKL_004"), null, chosenCard: monster));
            _ctx.Queue.Process();
            Assert.AreEqual(CardZone.Hand, monster.Zone);
        }

        [Test]
        public void NEU_SKL_001_QuickCommand_MovesToAdjacentEmpty_AndGrantsExtraAction()
        {
            var m = Ally("NEC_MON_001", SlotPosition.Front);
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(InHand("NEU_SKL_001"), m, SlotPosition.Middle));
            _ctx.Queue.Process();
            Assert.AreEqual(SlotPosition.Middle, _ctx.Field.PositionOf(m));
            Assert.AreEqual(0, m.PositionDefBonus, "Front를 떠나면 DEF 보너스 없음");
            Assert.AreEqual(1, m.ExtraActionsThisTurn);
        }

        [Test]
        public void NEU_SKL_002_Reorganize_DiscardHandDrawSame()
        {
            for (int i = 0; i < 6; i++) _ctx.Player.Deck.AddToHand(new RuntimeCard(600 + i, "FILLER", 0));
            _ctx.Player.Deck.EndTurn();
            _ctx.Player.Deck.AddToHand(new RuntimeCard(610, "FILLER", 0));
            _ctx.Player.Deck.AddToHand(new RuntimeCard(611, "FILLER", 0));
            _ctx.PlayCard(InHand("NEU_SKL_002"), null);
            _ctx.Queue.Process();
            Assert.AreEqual(2, _ctx.Player.Deck.Hand.Count, "재정비 자신을 뺀 손패 2장 → 2장 드로우");
            Assert.IsFalse(_ctx.Player.Deck.Hand.Any(c => c.InstanceId == 610 || c.InstanceId == 611) && _ctx.Player.Deck.ShuffleCount == 0);
        }

        // ---------- 액션 6장 ----------

        [Test]
        public void NEC_ACT_001_BloodRite_SacrificePlusBonus1_Exhaust()
        {
            var m = Ally("NEC_MON_001", SlotPosition.Front);
            var card = InHand("NEC_ACT_001");
            _ctx.PlayCard(card, m);
            _ctx.Queue.Process();
            // Ally()가 소환 비용을 채워 주므로 시작 3 그대로. 3 − 카드 1 + 희생 Lv1 1 + 보너스 1 = 4 (D-20)
            Assert.AreEqual(4, _ctx.Player.CurrentCrystal);
            Assert.IsTrue(m.IsDead);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone, "D-24");
        }

        [Test]
        public void NEC_ACT_001_Upgraded_Bonus2()
        {
            var m = Ally("NEC_MON_001", SlotPosition.Front);
            _ctx.PlayCard(InHand("NEC_ACT_001", upgraded: true), m);
            _ctx.Queue.Process();
            Assert.AreEqual(5, _ctx.Player.CurrentCrystal, "3 − 1 + 1 + 보너스 2");
        }

        [Test]
        public void NEC_ACT_002_UndeadArmy_ThreeSkeletons_FitWhatFits()
        {
            Ally("NEC_MON_001", SlotPosition.Front);
            _ctx.Player.CurrentCrystal = 3;
            var card = InHand("NEC_ACT_002");
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(card, null));
            _ctx.Queue.Process();
            var tokens = _ctx.Allies.Where(a => a.IsToken).ToList();
            Assert.AreEqual(2, tokens.Count, "빈 칸 2개만큼만 (D-63)");
            Assert.AreEqual(4, tokens[0].MaxHP.FinalValue);
            Assert.AreEqual(2, tokens[0].Attack.FinalValue);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone);
        }

        [Test]
        public void NEC_ACT_003_DeathWhirl_EachAllyHitsOppositeEnemyForItsHp()
        {
            var a1 = Ally("NEC_MON_001", SlotPosition.Front, hp: 7);
            var a2 = Ally("NEC_MON_001", SlotPosition.Back, hp: 4);
            _ctx.Player.CurrentCrystal = 3;
            var front = Enemy(SlotPosition.Front, hp: 30, def: 1);
            var back = Enemy(SlotPosition.Back, hp: 30, def: 0);
            _ctx.PlayCard(InHand("NEC_ACT_003"), null);
            _ctx.Queue.Process();
            // 카드 피해 D-01: Front 적 DEF 1 + Front 보너스 1 = 2 → 7 − 2 = 5. Back 적 4 − 0 = 4
            Assert.AreEqual(25, front.CurrentHP);
            Assert.AreEqual(26, back.CurrentHP);
        }

        [Test]
        public void NEC_ACT_004_LifeDrain_15Damage_HealLowestHalf()
        {
            var hurt = Ally("NEC_MON_001", SlotPosition.Front, hp: 2);
            Ally("NEC_MON_001", SlotPosition.Back, hp: 9);
            _ctx.Player.CurrentCrystal = 3;
            var e = Enemy(SlotPosition.Back, hp: 30, def: 0);
            _ctx.PlayCard(InHand("NEC_ACT_004"), e);
            _ctx.Queue.Process();
            Assert.AreEqual(15, e.CurrentHP);
            Assert.AreEqual(9, hurt.CurrentHP, "2 + floor(15 × 0.5)=7, 최대 HP 10 이하");
        }

        [Test]
        public void NEU_ACT_001_Opportunity_Draw2ThenDiscardChosen()
        {
            for (int i = 0; i < 4; i++) _ctx.Player.Deck.AddToHand(new RuntimeCard(800 + i, "FILLER", 0));
            _ctx.Player.Deck.EndTurn();
            var keep = new RuntimeCard(900, "FILLER", 0);
            _ctx.Player.Deck.AddToHand(keep);
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(InHand("NEU_ACT_001"), null, chosenCard: keep));
            _ctx.Queue.Process();
            Assert.AreEqual(2, _ctx.Player.Deck.Hand.Count, "1 + 2 드로우 − 1 버림");
            Assert.AreEqual(CardZone.DiscardPile, keep.Zone);
        }

        [Test]
        public void NEU_ACT_002_Alchemy_BanishMonsterGainCostTimesTwo()
        {
            var golem = new RuntimeCard(950, "NEC_MON_001", 1);
            _ctx.Player.Deck.AddToHand(golem);
            var card = InHand("NEU_ACT_002");
            _ctx.PlayCard(card, null, chosenCard: golem);
            _ctx.Queue.Process();
            // 3 − 2 + 1×2 = 3
            Assert.AreEqual(3, _ctx.Player.CurrentCrystal);
            Assert.AreEqual(CardZone.ExhaustPile, golem.Zone);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone);
        }

        // ---------- 1단계 몬스터 효과 ----------

        [Test]
        public void NEC_MON_003_CursedSkeleton_OnSummonPoison1RandomEnemy()
        {
            var e = Enemy(SlotPosition.Front);
            Ally("NEC_MON_003", SlotPosition.Back);
            Assert.AreEqual(1, e.Statuses.Stacks(StatusType.Poison));
        }

        [Test]
        public void NEC_MON_002_HungryGhoul_OnDeathHealsRandomAlly3()
        {
            var ghoul = Ally("NEC_MON_002", SlotPosition.Front, hp: 5);
            var other = Ally("NEC_MON_001", SlotPosition.Back, hp: 4);
            _ctx.Sacrifice(ghoul);
            _ctx.Queue.Process();
            Assert.AreEqual(7, other.CurrentHP);
        }

        [Test]
        public void NEC_MON_005_Wraith_AtkPlus1PerAllyDeath_Max4()
        {
            var wraith = Ally("NEC_MON_005", SlotPosition.Back);
            int baseAtk = wraith.Attack.FinalValue;
            for (int i = 0; i < 6; i++)
            {
                _ctx.Player.CurrentCrystal = 3;
                var m = Ally("NEU_MON_001", SlotPosition.Front);
                _ctx.Sacrifice(m);
                _ctx.Queue.Process();
            }
            Assert.AreEqual(baseAtk + 4, wraith.Attack.FinalValue);
        }

        [Test]
        public void NEU_MON_004_SupplyWagon_OnSummonDraw1_AndSoulbound()
        {
            _ctx.Player.Deck.AddToHand(new RuntimeCard(990, "FILLER", 0));
            _ctx.Player.Deck.EndTurn();
            var wagon = Ally("NEU_MON_004", SlotPosition.Back);
            Assert.AreEqual(1, _ctx.Player.Deck.Hand.Count);
            Assert.AreEqual(SacrificeResult.Soulbound, _ctx.Sacrifice(wagon));
        }

        [Test]
        public void NEU_MON_001_Shard_SacrificeGives1()
        {
            var shard = Ally("NEU_MON_001", SlotPosition.Front);
            int before = _ctx.Player.CurrentCrystal;
            _ctx.Sacrifice(shard);
            _ctx.Queue.Process();
            Assert.AreEqual(before + 1, _ctx.Player.CurrentCrystal);
        }

        [Test]
        public void NEC_MON_007_BloodPriest_OptionalSacrificeDraws2()
        {
            for (int i = 0; i < 3; i++) _ctx.Player.Deck.AddToHand(new RuntimeCard(1000 + i, "FILLER", 0));
            _ctx.Player.Deck.EndTurn();
            var fodder = Ally("NEU_MON_001", SlotPosition.Front);
            _ctx.Player.CurrentCrystal = 3;
            var priest = InHand("NEC_MON_007");
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(priest, fodder, SlotPosition.Back));
            _ctx.Queue.Process();
            Assert.IsTrue(fodder.IsDead);
            Assert.AreEqual(2, _ctx.Player.Deck.Hand.Count);
            Assert.AreEqual(3 - 2 + 1, _ctx.Player.CurrentCrystal, "비용 2, 파편 희생 +1");
        }
    }
}
