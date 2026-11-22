using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// Wave 전환 (`32` CORE-03, `03` §2, `04` §11, `09` §6, `01` D-72). 기대값은 손으로 계산한다.
    /// </summary>
    [Category("Stage1")]
    public class WaveTests
    {
        private BattleContext _ctx;
        private int _id;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BattleContext(41) { Cards = StageOneCards.Create(), EnemyKits = StageOneEnemies.Create() };
            var deck = Enumerable.Range(0, 12).Select(i => _ctx.Cards.Get("NEC_SKL_003").CreateCard(100 + i, false));
            _ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(deck, _ctx.Random));
        }

        private RuntimeMonster Ally(SlotPosition pos, int hp = 10, int atk = 3, int def = 0, bool token = false)
        {
            var m = new RuntimeMonster("A" + (++_id), token ? "TOKEN_SKELETON" : "NEC_MON_001", hp, atk, def, null, isToken: token);
            _ctx.State = BattleState.PlayerPhase;
            _ctx.Field.TrySummonAlly(m, pos, _ctx);
            _ctx.Queue.Process();
            return m;
        }

        private RuntimeEnemy Enemy(int hp, int def = 0) => new RuntimeEnemy("E" + (++_id), "ENE_TEST_IDLE", hp, 5, def, false);

        [Test]
        public void CORE_03_MidWaveCleared_NoVictory_TurnEndsThenNextWaveSpawns()
        {
            var front = Ally(SlotPosition.Front, hp: 10, atk: 5);
            var token = Ally(SlotPosition.Back, hp: 4, atk: 2, token: true);
            var w1 = Enemy(hp: 4);
            _ctx.Field.PlaceEnemy(w1, null, _ctx);
            var w2a = Enemy(hp: 20);
            var w2b = Enemy(hp: 20);
            _ctx.QueueWave(w2a, w2b);
            Assert.AreEqual(1, _ctx.PendingWaveCount);

            _ctx.StartPlayerTurn();
            front.Statuses.Apply(StatusType.Poison, 2, _ctx);
            var outcome = _ctx.EndPlayerTurn();
            // 뼈 병사 ATK 5 − (DEF 0 + Front 1) = 4 → 적 HP 4 → 0. 토큰은 대상이 없어 공격하지 않는다
            Assert.IsTrue(w1.IsDead);
            Assert.AreEqual(BattleOutcome.None, outcome, "중간 Wave는 승리가 아니다");
            Assert.AreEqual(8, front.CurrentHP, "턴 종료는 평소대로: Poison 2 틱 → 10 − 2");
            Assert.AreEqual(2, front.Statuses.Stacks(StatusType.Poison), "상태이상 유지 (`04` §11)");
            Assert.AreEqual(0, _ctx.Player.Deck.Hand.Count, "손패는 턴 종료에 버림");

            // 턴 종료 뒤 다음 Wave (D-72)
            Assert.AreEqual(0, _ctx.PendingWaveCount);
            CollectionAssert.AreEqual(new[] { w2a, w2b }, _ctx.Enemies.ToArray());
            Assert.AreEqual(w2a, _ctx.Field.At(TeamType.Enemy, SlotPosition.Front), "빈 칸 앞쪽부터");
            Assert.AreEqual(w2b, _ctx.Field.At(TeamType.Enemy, SlotPosition.Middle));
            Assert.IsFalse(_ctx.Allies.Contains(token), "토큰 제거");
            Assert.AreEqual(front, _ctx.Field.At(TeamType.Player, SlotPosition.Front), "몬스터는 칸·HP 유지");
            Assert.AreEqual(20, w2a.CurrentHP, "이번 턴에는 맞지 않는다");

            _ctx.StartPlayerTurn();
            Assert.AreEqual(2, _ctx.Turn, "노드 턴 수 유지");
            Assert.AreEqual(3, _ctx.Player.CurrentCrystal, "Crystal 리셋");
            Assert.AreEqual(5, _ctx.Player.Deck.Hand.Count, "Draw 5");
        }

        [Test]
        public void CORE_03_LastWaveCleared_IsVictory()
        {
            Ally(SlotPosition.Front, hp: 10, atk: 5);
            _ctx.Field.PlaceEnemy(Enemy(hp: 4), null, _ctx);
            var w2 = Enemy(hp: 4);
            _ctx.QueueWave(w2);

            _ctx.StartPlayerTurn();
            Assert.AreEqual(BattleOutcome.None, _ctx.EndPlayerTurn());
            _ctx.StartPlayerTurn();
            Assert.AreEqual(BattleOutcome.Victory, _ctx.EndPlayerTurn(), "마지막 Wave 전멸");
            Assert.IsTrue(w2.IsDead);
        }

        [Test]
        public void CORE_03_MidWaveClearedOnTurn30_DefeatWithoutNextWave()
        {
            Ally(SlotPosition.Front, hp: 50, atk: 1);
            var w1 = Enemy(hp: 1000);
            _ctx.Field.PlaceEnemy(w1, null, _ctx);
            var w2 = Enemy(hp: 10);
            _ctx.QueueWave(w2);
            for (int t = 1; t < 30; t++)
            {
                _ctx.StartPlayerTurn();
                Assert.AreEqual(BattleOutcome.None, _ctx.EndPlayerTurn(), $"turn {t}");
            }
            _ctx.StartPlayerTurn();
            w1.CurrentHP = 1; // 30턴째에 중간 Wave를 끝낸다: ATK 1 − Front 1 → 최소 1
            Assert.AreEqual(BattleOutcome.Defeat, _ctx.EndPlayerTurn(), "31턴 진입 없이 패배");
            Assert.IsTrue(w1.IsDead);
            Assert.IsFalse(_ctx.Enemies.Contains(w2), "다음 Wave는 나오지 않는다");
        }

        [Test]
        public void MainPhaseCardClearsWave_NextWaveAfterTurnEnd()
        {
            _ctx.Field.PlaceEnemy(Enemy(hp: 3), null, _ctx);
            var w2 = Enemy(hp: 10);
            _ctx.QueueWave(w2);
            _ctx.StartPlayerTurn();
            // Main Phase에 카드 효과로 마지막 적을 죽인 경우 (DEF를 보지 않는 고정 피해 3)
            _ctx.Queue.EnqueueMain(new DamageAction { Source = _ctx.Player, Target = _ctx.Enemies[0], BaseDamage = 3, IsStatusTick = true });
            _ctx.Queue.Process();
            Assert.AreEqual(0, _ctx.LivingEnemyCount);
            Assert.AreEqual(BattleOutcome.None, _ctx.EvaluateOutcome(), "Main Phase에서도 중간 Wave는 승리가 아니다");
            Assert.IsFalse(_ctx.Enemies.Contains(w2), "턴 종료 전에는 나오지 않는다");
            Assert.AreEqual(BattleOutcome.None, _ctx.EndPlayerTurn());
            Assert.IsTrue(_ctx.Enemies.Contains(w2));
        }
    }
}
