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
        public void ST_03_Normal5PlusEnemyPhase4_NoDamage_EndsAt4()
        {
            // D-78: 일반 5는 그 턴 종료에 빠지고, Enemy Phase 4만 남는다. 지금 구현은 합 9를 남긴다.
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 0);
            e.Statuses.Apply(StatusType.Shield, 5, ctx);
            ctx.State = BattleState.EnemyPhase;
            e.Statuses.Apply(StatusType.Shield, 4, ctx);
            Assert.AreEqual(9, e.Shield, "보이는 양은 합");
            ctx.EndTurn();
            Assert.AreEqual(4, e.Shield);
        }

        [Test]
        public void ST_03_Normal5PlusEnemyPhase4_Damage7_EndsAt2()
        {
            // 피해 7은 일반 5를 모두 쓰고 Enemy Phase 4를 2로 남긴다. 종료 후 2.
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 0);
            e.Statuses.Apply(StatusType.Shield, 5, ctx);
            ctx.State = BattleState.EnemyPhase;
            e.Statuses.Apply(StatusType.Shield, 4, ctx);
            ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 7 });
            ctx.Queue.Process();
            Assert.AreEqual(50, e.CurrentHP, "7은 합 9 안이라 HP는 그대로");
            ctx.EndTurn();
            Assert.AreEqual(2, e.Shield);
        }

        [Test]
        public void ST_03_Normal5PlusEnemyPhase4_Damage3_EndsAt4()
        {
            // 피해 3은 일반 5만 2로 줄인다. Enemy Phase 4는 그대로라 종료 후 4.
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 0);
            e.Statuses.Apply(StatusType.Shield, 5, ctx);
            ctx.State = BattleState.EnemyPhase;
            e.Statuses.Apply(StatusType.Shield, 4, ctx);
            ctx.Queue.EnqueueMain(new DamageAction { Target = e, BaseDamage = 3 });
            ctx.Queue.Process();
            Assert.AreEqual(50, e.CurrentHP);
            ctx.EndTurn();
            Assert.AreEqual(4, e.Shield);
        }

        [Test]
        public void ST_03_BurnAndPoisonTicks_ConsumeNormalShieldFirst()
        {
            // 틱 2는 DEF를 무시하고 일반 3부터 깎는다. 종료 후 Enemy Phase 4가 남는다.
            var ctx = NewBattle(out var burned, enemyHp: 50, enemyDef: 3);
            var poisoned = new RuntimeEnemy("E2", "ENE_TEST", 50, 5, 3, false);
            ctx.Enemies.Add(poisoned);
            burned.Statuses.Apply(StatusType.Shield, 3, ctx);
            poisoned.Statuses.Apply(StatusType.Shield, 3, ctx);
            ctx.State = BattleState.EnemyPhase;
            burned.Statuses.Apply(StatusType.Shield, 4, ctx);
            poisoned.Statuses.Apply(StatusType.Shield, 4, ctx);
            ctx.State = BattleState.PlayerPhase;
            burned.Statuses.Apply(StatusType.Burn, 2, ctx);
            poisoned.Statuses.Apply(StatusType.Poison, 2, ctx);
            ctx.EndTurn();
            Assert.AreEqual(50, burned.CurrentHP, "틱 2는 합 7 안");
            Assert.AreEqual(50, poisoned.CurrentHP);
            Assert.AreEqual(4, burned.Shield);
            Assert.AreEqual(4, poisoned.Shield);
            Assert.AreEqual(1, burned.Statuses.Stacks(StatusType.Burn), "Burn은 틱 뒤 1 감소");
            Assert.AreEqual(2, poisoned.Statuses.Stacks(StatusType.Poison), "Poison 스택은 유지");
        }

        [Test]
        public void ST_03_ShieldCap99_DropsExcessEnemyPhase_EndLeaves9()
        {
            // 일반 90 + Enemy Phase 20은 합 상한 99라 Enemy Phase는 9만 들어간다. 종료 후 9.
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 0);
            e.Statuses.Apply(StatusType.Shield, 90, ctx);
            ctx.State = BattleState.EnemyPhase;
            e.Statuses.Apply(StatusType.Shield, 20, ctx);
            Assert.AreEqual(99, e.Shield);
            ctx.EndTurn();
            Assert.AreEqual(9, e.Shield);
        }

        [Test]
        public void ST_03_EnemyPhase4_ThenNextTurnPlus3_SecondEndIs3()
        {
            // N턴 Enemy Phase +4, 피해 0 → 종료 후 4. N+1에서 그 4는 일반분, +3만 남는다.
            var ctx = NewBattle(out var e, enemyHp: 50, enemyDef: 0);
            ctx.State = BattleState.EnemyPhase;
            e.Statuses.Apply(StatusType.Shield, 4, ctx);
            ctx.EndTurn();
            Assert.AreEqual(4, e.Shield);
            ctx.BeginTurn();
            ctx.State = BattleState.EnemyPhase;
            e.Statuses.Apply(StatusType.Shield, 3, ctx);
            ctx.EndTurn();
            Assert.AreEqual(3, e.Shield);
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
        public void ShorterReapplyInEnemyPhase_DoesNotExtendDuration()
        {
            var ctx = NewBattle(out _);
            var ally = new RuntimeMonster("A1", "NEC_MON_001", 10, 3, 1, null);
            ctx.Allies.Add(ally);
            ally.Statuses.Apply(StatusType.Bind, 2, ctx);   // Main Phase에 2턴
            ctx.State = BattleState.EnemyPhase;
            ally.Statuses.Apply(StatusType.Bind, 1, ctx);   // 더 짧은 재부여: 바뀌는 것이 없어야 한다
            ctx.EndTurn();
            Assert.AreEqual(1, ally.Statuses.Stacks(StatusType.Bind), "갱신되지 않은 상태는 그 턴 종료에 정상적으로 줄어든다");
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
