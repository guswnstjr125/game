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

        /// <summary>소환 요청 후 큐를 처리해 칸에 놓는다.</summary>
        private static void Summon(BattleContext ctx, RuntimeMonster m, SlotPosition? pos)
        {
            ctx.Field.TrySummonAlly(m, pos, ctx);
            ctx.Queue.Process();
        }

        private static RuntimeMonster Mon(string id, int level = 1, bool token = false, bool soulbound = false)
        {
            var m = new RuntimeMonster(id, "NEC_MON_001", 10, 3, 1, null, token) { Level = level };
            if (soulbound) m.Tags.Add("Soulbound");
            return m;
        }

        [Test]
        public void FIELD_AutoPlacement_AllyBackFirst_EnemyFrontFirst()
        {
            var ctx = NewBattle();
            Assert.AreEqual(SummonResult.Placed, ctx.Field.TrySummonAlly(Mon("A1"), null, ctx));
            ctx.Queue.Process();
            Assert.AreEqual(SlotPosition.Back, ctx.Field.PositionOf(ctx.Allies[0]));
            Summon(ctx, Mon("A2"), null);
            ctx.Queue.Process();
            Assert.AreEqual(SlotPosition.Middle, ctx.Field.PositionOf(ctx.Allies[1]));

            var e = new RuntimeEnemy("E1", "X", 10, 5, 1, false);
            ctx.Field.PlaceEnemy(e, null, ctx);
            Assert.AreEqual(SlotPosition.Front, ctx.Field.PositionOf(e));
        }

        [Test]
        public void DMG_01_FrontGetsDefBonus_OthersDoNot()
        {
            var ctx = NewBattle();
            var front = Mon("A1");
            var back = Mon("A2");
            Summon(ctx, front, SlotPosition.Front);
            Summon(ctx, back, SlotPosition.Back);
            Assert.AreEqual(2, front.EffectiveDefense, "DEF 1 + Front 1");
            Assert.AreEqual(1, back.EffectiveDefense);
        }

        [Test]
        public void SUM_FullField_MonsterCardRejected()
        {
            var ctx = NewBattle();
            for (int i = 0; i < 3; i++) Summon(ctx, Mon("A" + i), null);
            Assert.AreEqual(SummonResult.RejectedFullField, ctx.Field.TrySummonAlly(Mon("A4"), null, ctx));
            Assert.AreEqual(3, ctx.Allies.Count);
        }

        [Test]
        public void SUM_FullField_TokenWaitsForReplace_ReplacedUnitDiesWithoutCrystal()
        {
            var ctx = NewBattle();
            for (int i = 0; i < 3; i++) Summon(ctx, Mon("A" + i), null);
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
        public void CORE_02_DeathFreesSlot_NoAutoPull()
        {
            var ctx = NewBattle();
            var front = Mon("A1");
            var middle = Mon("A2");
            Summon(ctx, front, SlotPosition.Front);
            Summon(ctx, middle, SlotPosition.Middle);
            ctx.Queue.EnqueueMain(new DamageAction { Target = front, BaseDamage = 99 });
            ctx.Queue.Process();
            Assert.IsNull(ctx.Field.At(TeamType.Player, SlotPosition.Front));
            Assert.AreEqual(SlotPosition.Middle, ctx.Field.PositionOf(middle), "자동 당김 없음");
            Assert.AreSame(middle, ctx.Field.FrontLine(TeamType.Player), "빈 Front면 Middle이 앞줄");
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public void ECO_01_SacrificeCrystalByLevel(int level, int gain)
        {
            var ctx = NewBattle();
            var m = Mon("A1", level);
            Summon(ctx, m, null);
            Assert.AreEqual(SacrificeResult.Ok, ctx.Sacrifice(m));
            ctx.Queue.Process();
            Assert.AreEqual(3 + gain, ctx.Player.CurrentCrystal);
            Assert.IsTrue(m.IsDead);
            Assert.IsNull(ctx.Field.PositionOf(m));
        }

        [Test]
        public void ECO_01_SacrificeOverride_ShardAndGolem()
        {
            // 파편 +1 (D-04), 수정 골렘 Lv2 +3 (`13` §2.3)
            var ctx = NewBattle();
            var shard = Mon("S1");
            shard.SacrificeCrystalOverride = 1;
            var golem = Mon("G1", level: 2);
            golem.SacrificeCrystalOverride = 3;
            Summon(ctx, shard, null);
            Summon(ctx, golem, null);
            ctx.Sacrifice(shard);
            ctx.Sacrifice(golem);
            ctx.Queue.Process();
            Assert.AreEqual(3 + 1 + 3, ctx.Player.CurrentCrystal);
        }

        [Test]
        public void ECO_SoulboundAndReservedCannotBeSacrificed()
        {
            var ctx = NewBattle();
            var sb = Mon("A1", soulbound: true);
            Summon(ctx, sb, null);
            Assert.AreEqual(SacrificeResult.Soulbound, ctx.Sacrifice(sb));

            var m = Mon("A2");
            Summon(ctx, m, null);
            ctx.Sacrifice(m);
            Assert.AreEqual(SacrificeResult.AlreadyReserved, ctx.Sacrifice(m), "같은 유닛을 두 번 희생할 수 없다");
        }

        [Test]
        public void ECO_SacrificeOnlyInPlayerPhase_BindStillAllowed()
        {
            var ctx = NewBattle();
            var m = Mon("A1");
            Summon(ctx, m, null);
            m.Statuses.Apply(StatusType.Bind, 2, ctx);
            Assert.AreEqual(SacrificeResult.Ok, ctx.Sacrifice(m), "Bind여도 희생 가능 (`11` §2)");

            var m2 = Mon("A2");
            Summon(ctx, m2, null);
            ctx.State = BattleState.EnemyPhase;
            Assert.AreEqual(SacrificeResult.InvalidPhase, ctx.Sacrifice(m2));
        }

        [Test]
        public void ECO_01_SacrificeCountResetsEachTurn()
        {
            // 피의 잔·REL_BOSS_1F는 턴당 첫 희생에만 +1 (D-04·D-15). 유물 효과는 #44 이후, 여기서는 판정만
            var ctx = NewBattle();
            var a = Mon("A1");
            var b = Mon("A2");
            Summon(ctx, a, null);
            Summon(ctx, b, null);
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
        public void CORE_03_WaveStart_RemovesTokensOnly_KeepsMonsters_ClearsEnemies()
        {
            var ctx = NewBattle();
            var m = Mon("A1");
            var t = Mon("T1", token: true);
            Summon(ctx, m, null);
            Summon(ctx, t, null);
            ctx.Field.PlaceEnemy(new RuntimeEnemy("E1", "X", 10, 5, 1, false), null, ctx);
            m.Statuses.Apply(StatusType.Poison, 3, ctx);
            ctx.StartNextWave();
            CollectionAssert.AreEqual(new[] { m }, ctx.Allies);
            Assert.IsNull(ctx.Field.PositionOf(t));
            Assert.AreEqual(0, ctx.Enemies.Count);
            Assert.AreEqual(3, m.Statuses.Stacks(StatusType.Poison), "상태이상은 Wave 사이 유지 (D-08)");
            Assert.AreEqual(0, ctx.Graveyard.Count, "토큰 제거는 사망이 아니다 (트리거·보상 없음)");
        }

        [Test]
        public void ECO_01_BloodChalice_OnlyFirstSacrificeEachTurnGetsBonus()
        {
            // `32` ECO-01: 같은 턴 희생 3회 (피의 잔 보유) → 첫 희생만 +1 추가. Lv1 ×3 = 3, 보너스 1 → 시작 3 + 4 = 7
            var ctx = NewBattle();
            ctx.FirstSacrificeBonus = 1; // 피의 잔 (D-04). REL_BOSS_1F도 있으면 2 (D-15)
            var ms = new[] { Mon("A1"), Mon("A2"), Mon("A3") };
            foreach (var m in ms) ctx.Field.TrySummonAlly(m, null, ctx);
            ctx.Queue.Process();
            foreach (var m in ms) Assert.AreEqual(SacrificeResult.Ok, ctx.Sacrifice(m));
            ctx.Queue.Process();
            Assert.AreEqual(7, ctx.Player.CurrentCrystal);

            ctx.EndTurn();
            ctx.BeginTurn();
            ctx.Player.ResetCrystalForTurn();
            var next = Mon("A4");
            Summon(ctx, next, null);
            ctx.Queue.Process();
            ctx.Sacrifice(next);
            ctx.Queue.Process();
            Assert.AreEqual(3 + 1 + 1, ctx.Player.CurrentCrystal, "다음 턴 첫 희생은 다시 보너스");
        }

        [Test]
        public void ECO_ReservedTargetStaysOnField_UntilSacrificeResolves()
        {
            // `13` §2.2: 희생 예약 → 즉시 슬롯 제거 금지. IsDead는 SacrificeAction이 Crystal을 준 뒤에 된다
            var ctx = NewBattle();
            var m = Mon("A1");
            Summon(ctx, m, SlotPosition.Front);
            ctx.Queue.Process();
            ctx.Sacrifice(m);
            Assert.IsFalse(m.IsDead);
            Assert.AreEqual(SlotPosition.Front, ctx.Field.PositionOf(m));
            ctx.Queue.Process();
            Assert.IsTrue(m.IsDead);
        }

        [Test]
        public void ECO_ReservedTargetKilledFirst_SacrificeSkipped_NoCrystal()
        {
            // `13` §2.5 TARGET_DEAD: Resolve 시 Skip
            var ctx = NewBattle();
            var m = Mon("A1");
            Summon(ctx, m, null);
            ctx.Queue.Process();
            ctx.Queue.EnqueueMain(new DamageAction { Target = m, BaseDamage = 99 });
            ctx.Sacrifice(m);
            ctx.Queue.Process();
            Assert.IsTrue(m.IsDead);
            Assert.AreEqual(3, ctx.Player.CurrentCrystal);
            Assert.AreEqual(0, ctx.SacrificesThisTurn);
        }

        [Test]
        public void SUM_Replace_OnlyFrontAllowed_InvalidPickKeepsWaiting()
        {
            var ctx = NewBattle();
            for (int i = 0; i < 3; i++) ctx.Field.TrySummonAlly(Mon("A" + i), null, ctx);
            ctx.Queue.Process();
            var token = Mon("T1", token: true);
            Summon(ctx, token, null);
            var back = ctx.Field.At(TeamType.Player, SlotPosition.Back);
            Assert.IsFalse(ctx.Field.ResolveReplace(back, ctx), "`09` §3: Front 슬롯 유닛만 고를 수 있다");
            Assert.AreEqual(BattleState.WaitingReplace, ctx.State);
            Assert.IsFalse(back.IsDead);
            Assert.IsTrue(ctx.Field.ResolveReplace(ctx.Field.At(TeamType.Player, SlotPosition.Front), ctx));
            ctx.Queue.Process();
            Assert.AreEqual(SlotPosition.Front, ctx.Field.PositionOf(token));
        }

        [Test]
        public void SUM_WaitingReplace_OnlyFromPlayerPhase()
        {
            var ctx = NewBattle();
            for (int i = 0; i < 3; i++) ctx.Field.TrySummonAlly(Mon("A" + i), null, ctx);
            ctx.Queue.Process();
            ctx.State = BattleState.EnemyPhase;
            Assert.AreEqual(SummonResult.RejectedFullField, ctx.Field.TrySummonAlly(Mon("T1", token: true), null, ctx));
            Assert.AreEqual(BattleState.EnemyPhase, ctx.State);
        }

        [Test]
        public void SUM_TwoSummonsBeforeProcess_GetDifferentSlots()
        {
            var ctx = NewBattle();
            var a = Mon("A1");
            var b = Mon("A2");
            ctx.Field.TrySummonAlly(a, null, ctx);
            ctx.Field.TrySummonAlly(b, null, ctx);
            ctx.Queue.Process();
            Assert.AreEqual(SlotPosition.Back, ctx.Field.PositionOf(a));
            Assert.AreEqual(SlotPosition.Middle, ctx.Field.PositionOf(b));
        }
    }
}
