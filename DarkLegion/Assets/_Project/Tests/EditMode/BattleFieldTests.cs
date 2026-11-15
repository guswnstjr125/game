using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>슬롯·소환·희생 (`09`, `13` §2, `01` D-02·D-04·D-15). 기대값은 문서에서 손으로 옮긴다.</summary>
    [Category("Stage1")]
    public class BattleFieldTests
    {
        private static BattleContext NewBattle()
        {
            var ctx = new BattleContext(7);
            ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], ctx.Random));
            ctx.BeginTurn();
            ctx.Player.ResetCrystalForTurn();
            return ctx;
        }

        private static RuntimeMonster Mon(string id, int level = 1, bool token = false, bool soulbound = false)
        {
            var m = new RuntimeMonster(id, "NEC_MON_001", 10, 3, 1, null, token) { Level = level };
            if (soulbound) m.Tags.Add("Soulbound");
            return m;
        }

        [Test]
        public void AutoPlacement_AllyBackFirst_EnemyFrontFirst()
        {
            var ctx = NewBattle();
            Assert.AreEqual(SummonResult.Placed, ctx.Field.TrySummonAlly(Mon("A1"), null, ctx));
            Assert.AreEqual(SlotPosition.Back, ctx.Field.PositionOf(ctx.Allies[0]));
            ctx.Field.TrySummonAlly(Mon("A2"), null, ctx);
            Assert.AreEqual(SlotPosition.Middle, ctx.Field.PositionOf(ctx.Allies[1]));

            var e = new RuntimeEnemy("E1", "X", 10, 5, 1, false);
            ctx.Field.PlaceEnemy(e, null, ctx);
            Assert.AreEqual(SlotPosition.Front, ctx.Field.PositionOf(e));
        }

        [Test]
        public void FrontGetsDefBonus_OthersDoNot()
        {
            var ctx = NewBattle();
            var front = Mon("A1");
            var back = Mon("A2");
            ctx.Field.TrySummonAlly(front, SlotPosition.Front, ctx);
            ctx.Field.TrySummonAlly(back, SlotPosition.Back, ctx);
            Assert.AreEqual(2, front.EffectiveDefense, "DEF 1 + Front 1");
            Assert.AreEqual(1, back.EffectiveDefense);
        }

        [Test]
        public void MonsterCard_FullField_Rejected()
        {
            var ctx = NewBattle();
            for (int i = 0; i < 3; i++) ctx.Field.TrySummonAlly(Mon("A" + i), null, ctx);
            Assert.AreEqual(SummonResult.RejectedFullField, ctx.Field.TrySummonAlly(Mon("A4"), null, ctx));
            Assert.AreEqual(3, ctx.Allies.Count);
        }

        [Test]
        public void Token_FullField_WaitsForReplace_ThenReplacedUnitDiesWithoutCrystal()
        {
            var ctx = NewBattle();
            for (int i = 0; i < 3; i++) ctx.Field.TrySummonAlly(Mon("A" + i), null, ctx);
            var token = Mon("T1", token: true);
            Assert.AreEqual(SummonResult.WaitingReplace, ctx.Field.TrySummonAlly(token, null, ctx));
            Assert.AreEqual(BattleState.WaitingReplace, ctx.State);

            var victim = ctx.Field.At(TeamType.Player, SlotPosition.Front);
            ctx.Field.ResolveReplace(victim, ctx);
            ctx.Queue.Process();
            Assert.IsTrue(victim.IsDead);
            Assert.AreEqual(SlotPosition.Front, ctx.Field.PositionOf(token));
            Assert.AreEqual(3, ctx.Player.CurrentCrystal, "교체는 희생이 아니다: Crystal 없음");
            Assert.AreEqual(BattleState.PlayerPhase, ctx.State);
        }

        [Test]
        public void DeathFreesSlot_NoAutoPull()
        {
            var ctx = NewBattle();
            var front = Mon("A1");
            var middle = Mon("A2");
            ctx.Field.TrySummonAlly(front, SlotPosition.Front, ctx);
            ctx.Field.TrySummonAlly(middle, SlotPosition.Middle, ctx);
            ctx.Queue.EnqueueMain(new DamageAction { Target = front, BaseDamage = 99 });
            ctx.Queue.Process();
            Assert.IsNull(ctx.Field.At(TeamType.Player, SlotPosition.Front));
            Assert.AreEqual(SlotPosition.Middle, ctx.Field.PositionOf(middle), "자동 당김 없음");
            Assert.AreSame(middle, ctx.Field.FrontLine(TeamType.Player), "빈 Front면 Middle이 앞줄");
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public void ECO_SacrificeCrystalByLevel(int level, int gain)
        {
            var ctx = NewBattle();
            var m = Mon("A1", level);
            ctx.Field.TrySummonAlly(m, null, ctx);
            Assert.AreEqual(SacrificeResult.Ok, ctx.Sacrifice(m));
            ctx.Queue.Process();
            Assert.AreEqual(3 + gain, ctx.Player.CurrentCrystal);
            Assert.IsTrue(m.IsDead);
            Assert.IsNull(ctx.Field.PositionOf(m));
        }

        [Test]
        public void SacrificeOverride_ShardAndGolem()
        {
            // 파편 +1 (D-04), 수정 골렘 Lv2 +3 (`13` §2.3)
            var ctx = NewBattle();
            var shard = Mon("S1");
            shard.SacrificeCrystalOverride = 1;
            var golem = Mon("G1", level: 2);
            golem.SacrificeCrystalOverride = 3;
            ctx.Field.TrySummonAlly(shard, null, ctx);
            ctx.Field.TrySummonAlly(golem, null, ctx);
            ctx.Sacrifice(shard);
            ctx.Sacrifice(golem);
            ctx.Queue.Process();
            Assert.AreEqual(3 + 1 + 3, ctx.Player.CurrentCrystal);
        }

        [Test]
        public void SoulboundCannotBeSacrificed_DeadCannotBeSacrificed()
        {
            var ctx = NewBattle();
            var sb = Mon("A1", soulbound: true);
            ctx.Field.TrySummonAlly(sb, null, ctx);
            Assert.AreEqual(SacrificeResult.Soulbound, ctx.Sacrifice(sb));

            var m = Mon("A2");
            ctx.Field.TrySummonAlly(m, null, ctx);
            ctx.Sacrifice(m);
            Assert.AreEqual(SacrificeResult.TargetDead, ctx.Sacrifice(m), "같은 유닛을 두 번 희생할 수 없다");
        }

        [Test]
        public void SacrificeOnlyInPlayerPhase_BindStillAllowed()
        {
            var ctx = NewBattle();
            var m = Mon("A1");
            ctx.Field.TrySummonAlly(m, null, ctx);
            m.Statuses.Apply(StatusType.Bind, 2, ctx);
            Assert.AreEqual(SacrificeResult.Ok, ctx.Sacrifice(m), "Bind여도 희생 가능 (`11` §2)");

            var m2 = Mon("A2");
            ctx.Field.TrySummonAlly(m2, null, ctx);
            ctx.State = BattleState.EnemyPhase;
            Assert.AreEqual(SacrificeResult.InvalidPhase, ctx.Sacrifice(m2));
        }

        [Test]
        public void FirstSacrificeThisTurn_TrackedForRelics()
        {
            // 피의 잔·REL_BOSS_1F는 턴당 첫 희생에만 +1 (D-04·D-15). 유물 효과는 #44 이후, 여기서는 판정만
            var ctx = NewBattle();
            var a = Mon("A1");
            var b = Mon("A2");
            ctx.Field.TrySummonAlly(a, null, ctx);
            ctx.Field.TrySummonAlly(b, null, ctx);
            Assert.AreEqual(0, ctx.SacrificesThisTurn);
            ctx.Sacrifice(a);
            ctx.Sacrifice(b);
            ctx.Queue.Process();
            Assert.AreEqual(2, ctx.SacrificesThisTurn);
            ctx.EndTurn();
            ctx.BeginTurn();
            Assert.AreEqual(0, ctx.SacrificesThisTurn);
        }

        [Test]
        public void WaveStart_RemovesTokensOnly_KeepsMonsters_ClearsEnemies()
        {
            var ctx = NewBattle();
            var m = Mon("A1");
            var t = Mon("T1", token: true);
            ctx.Field.TrySummonAlly(m, null, ctx);
            ctx.Field.TrySummonAlly(t, null, ctx);
            ctx.Field.PlaceEnemy(new RuntimeEnemy("E1", "X", 10, 5, 1, false), null, ctx);
            m.Statuses.Apply(StatusType.Poison, 3, ctx);
            ctx.StartNextWave();
            CollectionAssert.AreEqual(new[] { m }, ctx.Allies);
            Assert.IsNull(ctx.Field.PositionOf(t));
            Assert.AreEqual(0, ctx.Enemies.Count);
            Assert.AreEqual(3, m.Statuses.Stacks(StatusType.Poison), "상태이상은 Wave 사이 유지 (D-08)");
            Assert.AreEqual(0, ctx.Graveyard.Count, "토큰 제거는 사망이 아니다 (트리거·보상 없음)");
        }
    }
}
