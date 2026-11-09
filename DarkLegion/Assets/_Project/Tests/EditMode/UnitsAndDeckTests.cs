using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    [Category("Stage1")]
    public class UnitsAndDeckTests
    {
        private static List<RuntimeCard> Cards(int n, int cost = 1)
        {
            return Enumerable.Range(1, n).Select(i => new RuntimeCard(i, "CARD_" + i, cost)).ToList();
        }

        [Test]
        public void StatFinalValue_RoundsHalfUp_AndNeverNegative()
        {
            var atk = new RuntimeStat(10) { MultiplicativeModifier = 0.75f };
            Assert.AreEqual(8, atk.FinalValue); // 7.5 → 8 (0.5 올림)
            var low = new RuntimeStat(2) { AdditiveModifier = -5 };
            Assert.AreEqual(0, low.FinalValue);
        }

        [Test]
        public void FloorScaleTable_MatchesFormula()
        {
            // `04` Floor_Scale 표 (손으로 옮김): (층, 인덱스) → HP, ATK, DEF
            var table = new[]
            {
                (1, 0, 14, 5, 1), (1, 8, 17, 6, 1), (2, 0, 18, 6, 1), (2, 8, 21, 7, 1),
                (3, 0, 22, 7, 1), (3, 8, 26, 8, 2), (4, 0, 26, 8, 2), (4, 8, 31, 9, 2),
                (5, 0, 30, 9, 2), (5, 8, 36, 11, 2),
            };
            foreach (var (floor, idx, hp, atk, def) in table)
            {
                Assert.AreEqual((hp, atk, def), EnemyScaler.Reference(floor, idx), $"floor {floor} index {idx}");
            }
        }

        [Test]
        public void EnemyScaler_NodeFactorOnly_BossFixed()
        {
            // 굶주린 구울 14/5/1 을 1층 노드 5(인덱스 4)에: 14×1.092=15.288→15, 5×1.112=5.56→6, 1×1.108→1
            Assert.AreEqual((15, 6, 1), EnemyScaler.Scale(14, 5, 1, 4, isBoss: false));
            Assert.AreEqual((47, 8, 1), EnemyScaler.Scale(47, 8, 1, 8, isBoss: true));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => EnemyScaler.Scale(14, 5, 1, 9, false));
        }

        [Test]
        public void PlayerDefenseIsZero_FrontBonusApplies()
        {
            var player = new RuntimePlayer("P", 80, new RuntimeDeck(Cards(0), new BattleRandom(1)));
            Assert.AreEqual(0, player.EffectiveDefense);
            var enemy = new RuntimeEnemy("E1", "ENE_1F_GHO_001", 14, 5, 1, false) { PositionDefBonus = 1 };
            Assert.AreEqual(2, enemy.EffectiveDefense);
        }

        [Test]
        public void CrystalResetsEachTurn_NoCarryOver_NoCapDuringTurn()
        {
            var p = new RuntimePlayer("P", 80, new RuntimeDeck(Cards(0), new BattleRandom(1)));
            p.ResetCrystalForTurn();
            Assert.AreEqual(3, p.CurrentCrystal);
            p.CurrentCrystal += 4; // 희생 등으로 턴 중 3 초과 가능 (D-30)
            Assert.AreEqual(7, p.CurrentCrystal);
            Assert.IsTrue(p.TrySpendCrystal(2));
            Assert.IsFalse(p.TrySpendCrystal(6));
            p.ResetCrystalForTurn();
            Assert.AreEqual(3, p.CurrentCrystal);
        }

        [Test]
        public void DeathChain_UnitCreatedKilledRemoved_Repeat()
        {
            var ctx = new BattleContext(5);
            ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(Cards(0), ctx.Random));
            for (int round = 0; round < 20; round++)
            {
                var e = new RuntimeEnemy("E" + round, "ENE_1F_GHO_002", 3, 6, 0, false);
                ctx.Enemies.Add(e);
                ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 5 });
                // 같은 대상에게 한 번 더: 이미 IsDead라 무시되어야 한다
                ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 5 });
                ctx.Queue.Process();
                Assert.IsTrue(e.IsDead, "round " + round);
                Assert.AreEqual(0, ctx.Enemies.Count, "round " + round);
            }
            Assert.AreEqual(20, ctx.Graveyard.Count);
            Assert.AreEqual(BattleOutcome.Victory, ctx.EvaluateOutcome());
        }

        [Test]
        public void TeamTargetFilter()
        {
            var ctx = new BattleContext(1);
            var m = new RuntimeMonster("A1", "NEC_MON_001", 10, 3, 1, null);
            var e1 = new RuntimeEnemy("E1", "X", 10, 5, 1, false);
            var e2 = new RuntimeEnemy("E2", "X", 10, 5, 1, false);
            e2.Flags.Untargetable = true;
            ctx.Allies.Add(m);
            ctx.Enemies.Add(e1);
            ctx.Enemies.Add(e2);
            CollectionAssert.AreEqual(new[] { m }, ctx.Targetable(TeamType.Player));
            CollectionAssert.AreEqual(new[] { e1 }, ctx.Targetable(TeamType.Enemy));
        }

        [Test]
        public void DeckDrawShuffleWhenEmpty_AndStopsWhenAllEmpty()
        {
            var deck = new RuntimeDeck(Cards(7), new BattleRandom(11));
            Assert.AreEqual(5, deck.Draw(5).Count);
            deck.EndTurn();
            Assert.AreEqual(5, deck.DiscardPile.Count);
            var second = deck.Draw(5); // 남은 2장 + 버린 5장 섞어서 3장
            Assert.AreEqual(5, second.Count);
            Assert.AreEqual(1, deck.ShuffleCount);
            Assert.AreEqual(7, deck.TotalCount);

            var small = new RuntimeDeck(Cards(3), new BattleRandom(1));
            Assert.AreEqual(3, small.Draw(5).Count, "덱·버린 더미가 다 비면 더 뽑지 않는다");
        }

        [Test]
        public void HAND_01_OverflowGoesToDiscard()
        {
            var deck = new RuntimeDeck(Cards(15), new BattleRandom(2));
            var drawn = deck.Draw(12);
            Assert.AreEqual(10, drawn.Count);
            Assert.AreEqual(10, deck.Hand.Count);
            Assert.AreEqual(2, deck.DiscardPile.Count);
        }

        [Test]
        public void EtherealExhaustsAtTurnEnd_OthersDiscard()
        {
            var cards = Cards(2);
            cards[0].IsEthereal = true;
            var deck = new RuntimeDeck(cards, new BattleRandom(3), shuffle: false);
            deck.Draw(2);
            deck.EndTurn();
            Assert.AreEqual(1, deck.ExhaustPile.Count);
            Assert.IsTrue(deck.ExhaustPile[0].IsEthereal);
            Assert.AreEqual(1, deck.DiscardPile.Count);
        }

        [Test]
        public void MonsterCardStaysOnFieldUntilDeath()
        {
            var ctx = new BattleContext(4);
            var cards = Cards(1);
            ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(cards, ctx.Random));
            ctx.Player.Deck.Draw(1);
            ctx.Player.Deck.MoveAfterUse(cards[0], isMonster: true);
            Assert.AreEqual(CardZone.OnField, cards[0].Zone);
            var m = new RuntimeMonster("A1", cards[0].CardId, 2, 3, 0, cards[0]);
            ctx.Allies.Add(m);
            ctx.Queue.EnqueueMain(new DamageAction { Target = m, BaseDamage = 9 });
            ctx.Queue.Process();
            Assert.AreEqual(CardZone.DiscardPile, cards[0].Zone);
        }

        [Test]
        public void ShuffleIsReproducibleWithSameSeed()
        {
            var a = new RuntimeDeck(Cards(10), new BattleRandom(77)).Draw(5).Select(c => c.InstanceId).ToArray();
            var b = new RuntimeDeck(Cards(10), new BattleRandom(77)).Draw(5).Select(c => c.InstanceId).ToArray();
            CollectionAssert.AreEqual(a, b, "seed 77");
        }
    }
}
