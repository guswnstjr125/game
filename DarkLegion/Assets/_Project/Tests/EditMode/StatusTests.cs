using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>상태이상 8종 (`11`, `12` Status_Master). 기대값은 손으로 계산한다.</summary>
    [Category("Stage1")]
    public class StatusTests
    {
        private static BattleContext NewBattle(out RuntimeEnemy enemy, int enemyHp = 50, int enemyDef = 0)
        {
            var ctx = new BattleContext(1);
            ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], ctx.Random));
            enemy = new RuntimeEnemy("E1", "ENE_TEST", enemyHp, 5, enemyDef, false);
            ctx.Enemies.Add(enemy);
            ctx.BeginTurn();
            return ctx;
        }

        [Test]
        public void ST_01_BurnTicksAtTurnEnd_ThenDropsByOne_CapIs20()
        {
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 3);
            e.Statuses.Apply(StatusType.Burn, 3, ctx);
            ctx.EndTurn();
            Assert.AreEqual(47, e.CurrentHP, "틱 3, DEF 3 무시");
            Assert.AreEqual(2, e.Statuses.Stacks(StatusType.Burn));

            e.Statuses.Apply(StatusType.Burn, 30, ctx);
            Assert.AreEqual(20, e.Statuses.Stacks(StatusType.Burn), "20 초과 부여는 잘린다");
        }

        [Test]
        public void ST_04_PoisonKeepsStacks_CapIs20_ClearedAtBattleEnd()
        {
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 2);
            e.Statuses.Apply(StatusType.Poison, 4, ctx);
            ctx.EndTurn();
            ctx.BeginTurn();
            ctx.EndTurn();
            Assert.AreEqual(42, e.CurrentHP, "4 + 4, DEF 무시, 스택은 줄지 않는다");
            Assert.AreEqual(4, e.Statuses.Stacks(StatusType.Poison));
            e.Statuses.Apply(StatusType.Poison, 99, ctx);
            Assert.AreEqual(20, e.Statuses.Stacks(StatusType.Poison));
            ctx.EndBattleCleanup();
            Assert.AreEqual(0, e.Statuses.Stacks(StatusType.Poison));
        }

        [Test]
        public void ST_02_WeakAttacker_VulnerableTarget_DefBetween()
        {
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 2);
            var ally = new RuntimeMonster("A1", "NEC_MON_003", 10, 5, 0, null);
            ctx.Allies.Add(ally);
            ally.Statuses.Apply(StatusType.Weak, 2, ctx);
            e.Statuses.Apply(StatusType.Vulnerable, 2, ctx);
            ctx.Queue.EnqueueMain(new DamageAction { Source = ally, Target = e, BaseDamage = ally.Attack.FinalValue });
            ctx.Queue.Process();
            // floor((5 × 0.75 − 2) × 1.25) = floor(2.1875) = 2
            Assert.AreEqual(48, e.CurrentHP);
        }

        [Test]
        public void WeakDoesNotApplyToPlayerCardDamage()
        {
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 0);
            ctx.Player.Statuses.Apply(StatusType.Weak, 2, ctx);
            // 카드 피해: Source = 플레이어. D-01 "플레이어 카드 피해에는 적용 안 함"
            ctx.Queue.EnqueueMain(new DamageAction { Source = ctx.Player, Target = e, BaseDamage = 8 });
            ctx.Queue.Process();
            Assert.AreEqual(42, e.CurrentHP);
        }

        [Test]
        public void ST_03_ShieldAbsorbs_ThenClearedAtTurnEnd()
        {
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 0);
            e.Statuses.Apply(StatusType.Shield, 6, ctx);
            ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 4 });
            ctx.Queue.Process();
            Assert.AreEqual(50, e.CurrentHP);
            Assert.AreEqual(2, e.Shield);
            ctx.EndTurn();
            Assert.AreEqual(0, e.Shield);
        }

        [Test]
        public void DurationStatus_AppliedInMainPhase_LastsTwoTurnEnds()
        {
            var ctx = NewBattle(out var e);
            e.Statuses.Apply(StatusType.Weak, 2, ctx); // PlayerPhase에 부여
            ctx.EndTurn();
            Assert.AreEqual(1, e.Statuses.Stacks(StatusType.Weak));
            ctx.BeginTurn();
            ctx.EndTurn();
            Assert.IsFalse(e.Statuses.Has(StatusType.Weak));
        }

        [Test]
        public void StunAppliedInEnemyPhase_BlocksNextTurn_NotWasted()
        {
            var ctx = NewBattle(out _);
            var ally = new RuntimeMonster("A1", "NEC_MON_001", 10, 3, 1, null);
            ctx.Allies.Add(ally);
            ctx.State = BattleState.EnemyPhase;
            ally.Statuses.Apply(StatusType.Stun, 1, ctx);
            ctx.EndTurn();
            ctx.BeginTurn();
            Assert.IsFalse(ally.Statuses.CanAct, "적이 건 Stun은 다음 턴에 행동을 막는다");
            ctx.EndTurn();
            ctx.BeginTurn();
            Assert.IsTrue(ally.Statuses.CanAct);
        }

        [Test]
        public void DurationRefreshTakesLonger_BindBlocksAction()
        {
            var ctx = NewBattle(out var e);
            e.Statuses.Apply(StatusType.Bind, 3, ctx);
            e.Statuses.Apply(StatusType.Bind, 1, ctx);
            Assert.AreEqual(3, e.Statuses.Stacks(StatusType.Bind), "짧은 재부여는 남은 턴을 줄이지 않는다");
            Assert.IsFalse(e.Statuses.CanAct);
        }

        [Test]
        public void BurnTickCanKill_AndBattleIsWon()
        {
            var ctx = NewBattle(out var e, enemyHp: 3);
            e.Statuses.Apply(StatusType.Burn, 5, ctx);
            Assert.AreEqual(BattleOutcome.Victory, ctx.EndTurn());
            Assert.AreEqual(0, ctx.Enemies.Count);
        }
    }
}
