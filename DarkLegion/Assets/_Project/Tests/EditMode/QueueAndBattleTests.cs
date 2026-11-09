using System.Collections.Generic;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    [Category("Stage1")]
    public class QueueAndBattleTests
    {
        /// <summary>실행 순서를 기록하고, 필요하면 실행 중에 다른 큐에 액션을 넣는 테스트용 액션.</summary>
        private sealed class Probe : BattleAction
        {
            private readonly string _name;
            private readonly List<string> _log;
            public System.Action<BattleContext> OnExecute;

            public Probe(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public override BattleActionType Type => BattleActionType.Trigger;

            public override void Execute(BattleContext context)
            {
                _log.Add(_name);
                OnExecute?.Invoke(context);
            }

            public override string ToString() => _name;
        }

        [Test]
        public void FlushOrder_DeathBeforeTrigger_AndDeathAfterEachTrigger()
        {
            var ctx = new BattleContext(1);
            var log = new List<string>();
            var m1 = new Probe("M1", log);
            var m2 = new Probe("M2", log);
            var t1 = new Probe("T1", log);
            var t2 = new Probe("T2", log);
            var d1 = new Probe("D1", log);
            var d2 = new Probe("D2", log);
            m1.OnExecute = c =>
            {
                c.Queue.EnqueueTrigger(t1);
                c.Queue.EnqueueTrigger(t2);
                c.Queue.EnqueueDeath(d1);
            };
            t1.OnExecute = c => c.Queue.EnqueueDeath(d2);
            ctx.Queue.EnqueueMain(m1);
            ctx.Queue.EnqueueMain(m2);

            ctx.Queue.Process();

            // `06` §9.2: Main 1회 → Death → Trigger(1건마다 Death 재-Flush) → 다음 Main
            CollectionAssert.AreEqual(new[] { "M1", "D1", "T1", "D2", "T2", "M2" }, log);
        }

        [Test]
        public void CancelledActionDoesNotExecute()
        {
            var ctx = new BattleContext(1);
            var log = new List<string>();
            var a = new Probe("A", log);
            a.Cancel();
            ctx.Queue.EnqueueMain(a);
            ctx.Queue.Process();
            CollectionAssert.IsEmpty(log);
        }

        [Test]
        public void InfiniteTriggerLoopStopsWithEmergency()
        {
            var ctx = new BattleContext(1);
            var log = new List<string>();
            Probe loop = null;
            loop = new Probe("L", log) { OnExecute = c => c.Queue.EnqueueTrigger(new Probe("L", log) { OnExecute = loop.OnExecute }) };
            loop.OnExecute = c => c.Queue.EnqueueTrigger(new Probe("L", log) { OnExecute = loop.OnExecute });
            var start = new Probe("M", log) { OnExecute = c => c.Queue.EnqueueTrigger(loop) };
            ctx.Queue.EnqueueMain(start);

            ctx.Queue.Process();

            Assert.IsTrue(ctx.Queue.EmergencyStopped);
            Assert.LessOrEqual(log.Count, 1 + BattleLimits.MaxTriggerIterationsPerFlush);
            Assert.AreEqual(BattleOutcome.Defeat, ctx.EvaluateOutcome(playerHp: 50, livingEnemies: 1));
        }

        [Test]
        public void TriggerDepthOverLimitIsDropped()
        {
            var ctx = new BattleContext(1);
            var deep = new Probe("deep", new List<string>()) { TriggerDepth = BattleLimits.MaxTriggerDepth + 1 };
            Assert.IsFalse(ctx.Queue.EnqueueTrigger(deep));
            Assert.AreEqual(0, ctx.Queue.TriggerCount);
        }

        [Test]
        public void CORE_01_TenTurnsBasicAttackKeepsStateConsistent()
        {
            var ctx = new BattleContext(1234);
            int enemyHp = 200;
            int executed = 0;
            for (int turn = 1; turn <= 10; turn++)
            {
                ctx.BeginTurn();
                Assert.AreEqual(turn, ctx.Turn);
                ctx.State = BattleState.Resolving;
                var attack = new Probe("attack", new List<string>())
                {
                    OnExecute = c =>
                    {
                        enemyHp = DamageCalculator.ApplyToShieldAndHp(
                            DamageCalculator.AttackDamage(new DamageRequest { BaseDamage = 8, TargetDef = 2 }), 0, enemyHp).hp;
                        executed++;
                    }
                };
                ctx.Queue.EnqueueMain(attack);
                ctx.Queue.Process();
                Assert.AreEqual(0, ctx.Queue.MainCount + ctx.Queue.TriggerCount + ctx.Queue.DeathCount, "turn " + turn);
                ctx.State = BattleState.TurnEnd;
                Assert.AreEqual(BattleOutcome.None, ctx.EvaluateOutcome(playerHp: 50, livingEnemies: 1), "turn " + turn);
            }
            Assert.AreEqual(10, executed);
            Assert.AreEqual(200 - 10 * 6, enemyHp);
        }

        [Test]
        public void CORE_04_Turn30EndWithoutWinIsDefeat()
        {
            var ctx = new BattleContext(1);
            for (int i = 0; i < 29; i++)
            {
                ctx.BeginTurn();
                ctx.State = BattleState.TurnEnd;
                Assert.AreEqual(BattleOutcome.None, ctx.EvaluateOutcome(50, 1), "turn " + ctx.Turn);
            }
            ctx.BeginTurn();
            Assert.AreEqual(30, ctx.Turn);
            ctx.State = BattleState.EnemyPhase;
            Assert.AreEqual(BattleOutcome.None, ctx.EvaluateOutcome(50, 1), "30턴 도중에는 아직 진행");
            ctx.State = BattleState.TurnEnd;
            Assert.AreEqual(BattleOutcome.Defeat, ctx.EvaluateOutcome(50, 1));
            Assert.AreEqual(BattleState.End, ctx.State);
        }

        [Test]
        public void VictoryAndDefeat()
        {
            var win = new BattleContext(1);
            win.BeginTurn();
            Assert.AreEqual(BattleOutcome.Victory, win.EvaluateOutcome(10, 0));

            var lose = new BattleContext(1);
            lose.BeginTurn();
            Assert.AreEqual(BattleOutcome.Defeat, lose.EvaluateOutcome(0, 2));

            var both = new BattleContext(1);
            both.BeginTurn();
            Assert.AreEqual(BattleOutcome.Defeat, both.EvaluateOutcome(0, 0), "동시에 0이면 패배");
        }
    }
}
