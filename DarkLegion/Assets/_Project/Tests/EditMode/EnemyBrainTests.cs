using System.Collections.Generic;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>적 행동 선택 (`01` D-55, `21` §1.1).</summary>
    [Category("Stage1")]
    public class EnemyBrainTests
    {
        private static BattleContext Battle(out RuntimeEnemy e, int seed = 1)
        {
            var ctx = new BattleContext(seed);
            e = new RuntimeEnemy("E1", "ENE_TEST", 20, 5, 1, false);
            ctx.Enemies.Add(e);
            return ctx;
        }

        private static List<string> Run(EnemyBrain brain, RuntimeEnemy e, BattleContext ctx, int turns)
        {
            var ids = new List<string>();
            for (int i = 0; i < turns; i++)
            {
                var a = brain.Decide(e, ctx);
                ids.Add(a == null ? "-" : a.Id);
            }
            return ids;
        }

        [Test]
        public void AI_UnconditionalActionsRotateInTableOrder()
        {
            // 역병 수도사 ENE_1F_ELT_002: 1) Poison 2) Weak2 3) 기본공격 (조건 없음)
            var ctx = Battle(out var e);
            var brain = new EnemyBrain(new[]
            {
                new EnemyActionOption("Poison"), new EnemyActionOption("Weak2"), new EnemyActionOption("Basic"),
            });
            CollectionAssert.AreEqual(new[] { "Poison", "Weak2", "Basic", "Poison", "Weak2" }, Run(brain, e, ctx, 5));
        }

        [Test]
        public void AI_02_ConditionalFirst_ButNeverTwiceInARow()
        {
            // 용암 수호자 ENE_2F_FIR_003: 1) Shield 없음 → Shield+4  2) Taunt 없음 → Taunt  3) 기본공격
            var ctx = Battle(out var e);
            var brain = new EnemyBrain(new[]
            {
                new EnemyActionOption("Shield", (self, c) => self.Shield == 0),
                new EnemyActionOption("Taunt", (self, c) => !self.Statuses.Has(StatusType.Taunt)),
                new EnemyActionOption("Basic"),
            });
            // Shield는 매 턴 사라진다고 보고(테스트에서 Shield를 주지 않음) Taunt는 2턴째에 건다
            var ids = new List<string>();
            for (int turn = 1; turn <= 4; turn++)
            {
                var a = brain.Decide(e, ctx);
                ids.Add(a.Id);
                if (a.Id == "Taunt") e.Statuses.Apply(StatusType.Taunt, 1, ctx);
            }
            CollectionAssert.AreEqual(new[] { "Shield", "Taunt", "Shield", "Basic" }, ids);
        }

        [Test]
        public void AI_ChanceConditionUsesBattleRandom_NoBackToBack()
        {
            // 굶주린 구울 ENE_1F_GHO_001: 1) HP>50% 30% 강타  2) 기본공격
            // 연속 금지가 있으므로 장기 비율 p = 0.3 × (1 − p) → p = 0.3 / 1.3 = 23.08%
            var ctx = Battle(out var e, seed: 2024);
            var brain = new EnemyBrain(new[]
            {
                new EnemyActionOption("Heavy", (self, c) => self.CurrentHP * 2 > self.MaxHP.FinalValue, chance: 0.3),
                new EnemyActionOption("Basic"),
            });
            var ids = Run(brain, e, ctx, 20000);
            int heavy = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == "Heavy")
                {
                    heavy++;
                    if (i > 0) Assert.AreNotEqual("Heavy", ids[i - 1], "seed 2024, turn " + i);
                }
            }
            Assert.That(heavy / 20000.0, Is.EqualTo(0.2308).Within(0.015), "seed 2024");

            e.CurrentHP = 10; // 50% 이하: 조건 거짓
            CollectionAssert.AreEqual(new[] { "Basic", "Basic", "Basic" }, Run(brain, e, ctx, 3));
        }

        [Test]
        public void AI_StunOrBind_NoAction_AndRotationUnchanged()
        {
            var ctx = Battle(out var e);
            var brain = new EnemyBrain(new[] { new EnemyActionOption("A"), new EnemyActionOption("B") });
            Assert.AreEqual("A", brain.Decide(e, ctx).Id);
            e.Statuses.Apply(StatusType.Stun, 1, ctx);
            Assert.IsNull(brain.Decide(e, ctx));
            e.Statuses.Remove(StatusType.Stun);
            Assert.AreEqual("B", brain.Decide(e, ctx).Id);
        }

        [Test]
        public void AI_SameSeedSameDecisions()
        {
            EnemyActionOption[] Options() => new[]
            {
                new EnemyActionOption("Heavy", null, chance: 0.5), new EnemyActionOption("Basic"),
            };
            var c1 = Battle(out var e1, seed: 9);
            var c2 = Battle(out var e2, seed: 9);
            CollectionAssert.AreEqual(Run(new EnemyBrain(Options()), e1, c1, 50), Run(new EnemyBrain(Options()), e2, c2, 50), "seed 9");
        }
    }
}
