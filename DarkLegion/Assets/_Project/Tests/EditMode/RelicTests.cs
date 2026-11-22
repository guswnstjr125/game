using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 1단계 유물 8종 (#44, #76 2-4): `24` §1, `06` Relics_MVP, D-04·D-15·D-69·D-76. 기대값은 손으로 적는다.
    /// </summary>
    [Category("Stage1")]
    public class RelicTests
    {
        private static GameDataLoadResult Data => StageOneBattleSim.Loaded();

        /// <summary>F1-N1 전투 (구울 ×2). 덱은 손패로 다 들어오도록 5장 이하.</summary>
        private static BattleContext Battle(string[] relics, params string[] deck)
        {
            var run = RunFlow.NewRun("RUN-L", 1, "NEC", 80, deck.Length > 0 ? deck : new[] { "NEC_SKL_003" }, relics);
            var ctx = RunBattle.Start(run, Data);
            ctx.StartPlayerTurn();
            return ctx;
        }

        private static RuntimeMonster Ally(BattleContext ctx, SlotPosition pos, int hp = 30, int atk = 3)
        {
            var m = new RuntimeMonster("A" + pos, "NEC_MON_001", hp, atk, 0, null);
            ctx.Field.TrySummonAlly(m, pos, ctx);
            ctx.Queue.Process();
            return m;
        }

        private static void Hit(BattleContext ctx, RuntimeUnit source, RuntimeUnit target, int dmg, bool tick = false, bool attack = false)
        {
            ctx.Queue.EnqueueMain(new DamageAction { Source = source, Target = target, BaseDamage = dmg, IsStatusTick = tick, IsAttack = attack });
            ctx.Queue.Process();
        }

        // ---------- 피의 잔·굶주린 자의 뼈 (D-04·D-15) ----------

        [Test]
        public void Chalice_And_Boss1F_FirstSacrificeOnly_Stack()
        {
            var ctx = Battle(new[] { "REL_BLOOD_CHALICE", "REL_BOSS_1F" });
            var a = Ally(ctx, SlotPosition.Front);
            var b = Ally(ctx, SlotPosition.Middle);
            ctx.Player.CurrentCrystal = 0;
            ctx.Sacrifice(a);
            ctx.Queue.Process();
            Assert.AreEqual(3, ctx.Player.CurrentCrystal, "Lv1 +1, 잔 +1, 뼈 +1 (D-15 예)");
            ctx.Sacrifice(b);
            ctx.Queue.Process();
            Assert.AreEqual(4, ctx.Player.CurrentCrystal, "같은 턴 두 번째는 본체 +1만");
        }

        // ---------- REL_COM_001 낡은 뼈조각 (D-76) ----------

        [Test]
        public void Com001_SpendToZero_Plus1_OncePerTurn()
        {
            var ctx = Battle(new[] { "REL_COM_001" });
            Assert.AreEqual(3, ctx.Player.CurrentCrystal);
            ctx.Player.TrySpendCrystal(1);
            Assert.AreEqual(2, ctx.Player.CurrentCrystal);
            ctx.Player.TrySpendCrystal(2);
            Assert.AreEqual(1, ctx.Player.CurrentCrystal, "2 → 0이 된 순간 +1");
            ctx.Player.TrySpendCrystal(1);
            Assert.AreEqual(0, ctx.Player.CurrentCrystal, "턴당 1회");
            ctx.Player.TrySpendCrystal(0);
            Assert.AreEqual(0, ctx.Player.CurrentCrystal, "0 비용은 쓰는 것이 아니다");
            ctx.EndPlayerTurn();
            ctx.StartPlayerTurn();
            ctx.Player.TrySpendCrystal(3);
            Assert.AreEqual(1, ctx.Player.CurrentCrystal, "다음 턴 다시");
        }

        [Test]
        public void Com001_CardCostCounts()
        {
            var ctx = Battle(new[] { "REL_COM_001" }, "NEC_SKL_003", "NEC_SKL_003", "NEC_SKL_003");
            var e = ctx.Enemies[0];
            foreach (var card in ctx.Player.Deck.Hand.ToList())
            {
                Assert.AreEqual(PlayResult.Ok, ctx.PlayCard(card, e));
                ctx.Queue.Process();
            }
            Assert.AreEqual(1, ctx.Player.CurrentCrystal, "부패의 손길 ×3 (비용 1): 3 → 0 → +1");
        }

        // ---------- REL_COM_002 독이 든 가시 (D-76) ----------

        [Test]
        public void Com002_EnemyAttackOnAllyOrPlayer_AttackerPoison1_EachHit()
        {
            var ctx = Battle(new[] { "REL_COM_002" });
            var ally = Ally(ctx, SlotPosition.Front);
            var enemy = ctx.Enemies[0];
            Hit(ctx, enemy, ally, 5);
            Assert.AreEqual(1, enemy.Statuses.Stacks(StatusType.Poison));
            Hit(ctx, enemy, ctx.Player, 5);
            Assert.AreEqual(2, enemy.Statuses.Stacks(StatusType.Poison), "플레이어 피격도");
            ally.Statuses.Apply(StatusType.Shield, 50, ctx);
            Hit(ctx, enemy, ally, 5);
            Assert.AreEqual(3, enemy.Statuses.Stacks(StatusType.Poison), "Shield가 다 막아도");
            Hit(ctx, enemy, ally, 5, tick: true);
            Assert.AreEqual(3, enemy.Statuses.Stacks(StatusType.Poison), "틱은 아니다");
        }

        [Test]
        public void Com002_AoeOnFour_Poison4_DeadAttackerNone()
        {
            var ctx = Battle(new[] { "REL_COM_002" });
            var allies = new[] { Ally(ctx, SlotPosition.Front), Ally(ctx, SlotPosition.Middle), Ally(ctx, SlotPosition.Back) };
            var enemy = ctx.Enemies[0];
            foreach (var t in allies.Cast<RuntimeUnit>().Concat(new[] { ctx.Player })) ctx.Queue.EnqueueMain(new DamageAction { Source = enemy, Target = t, BaseDamage = 4 });
            ctx.Queue.Process();
            Assert.AreEqual(4, enemy.Statuses.Stacks(StatusType.Poison), "광역 4명 → 4");

            var other = ctx.Enemies[1];
            Hit(ctx, null, other, 999, tick: true); // 공격자가 먼저 죽었다
            Hit(ctx, other, allies[0], 3);
            Assert.AreEqual(0, other.Statuses.Stacks(StatusType.Poison));
        }

        // ---------- REL_COM_003 피 묻은 장갑 (D-76) ----------

        [Test]
        public void Com003_TenPercentVulnerablePerAllyAttack()
        {
            int vul = 0, n = 2000;
            for (int seed = 1; seed <= n; seed++)
            {
                var run = RunFlow.NewRun("RUN-G", seed, "NEC", 80, new[] { "NEC_SKL_003" }, new[] { "REL_COM_003" });
                var ctx = RunBattle.Start(run, Data);
                ctx.StartPlayerTurn();
                Ally(ctx, SlotPosition.Front, atk: 2);
                var target = ctx.Enemies[0];
                Hit(ctx, ctx.Allies[0], target, 2, attack: true);
                if (target.Statuses.Has(StatusType.Vulnerable)) vul++;
            }
            Assert.AreEqual(0.10, vul / (double)n, 0.02);
        }

        [Test]
        public void Com003_KilledTargetGetsNothing()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var run = RunFlow.NewRun("RUN-G", seed, "NEC", 80, new[] { "NEC_SKL_003" }, new[] { "REL_COM_003" });
                var ctx = RunBattle.Start(run, Data);
                ctx.StartPlayerTurn();
                var a = Ally(ctx, SlotPosition.Front);
                var t = ctx.Enemies[0];
                Hit(ctx, a, t, 999, attack: true);
                Assert.IsFalse(t.Statuses.Has(StatusType.Vulnerable));
            }
        }

        // ---------- REL_UNC_001 부서진 크리스탈 (D-76) ----------

        [Test]
        public void Unc001_SacrificeDraw1_FirstTwoPerTurn()
        {
            var deck = Enumerable.Repeat("NEC_SKL_003", 12).ToArray();
            var ctx = Battle(new[] { "REL_UNC_001" }, deck);
            int hand = ctx.Player.Deck.Hand.Count;
            Assert.AreEqual(5, hand);
            for (int i = 0; i < 3; i++)
            {
                ctx.Sacrifice(Ally(ctx, SlotPosition.Back));
                ctx.Queue.Process();
            }
            Assert.AreEqual(5 + 2, ctx.Player.Deck.Hand.Count, "희생 3번 → Draw 2 (턴당 2회)");
        }

        // ---------- REL_UNC_002 재앙의 인장 (D-76) ----------

        [Test]
        public void Unc002_AtkPlusBurningEnemies_Max3_ReadEachTime()
        {
            var ctx = Battle(new[] { "REL_UNC_002" });
            var a = Ally(ctx, SlotPosition.Front, atk: 3);
            Assert.AreEqual(3, ctx.EffectiveAttack(a));
            ctx.Enemies[0].Statuses.Apply(StatusType.Burn, 1, ctx);
            Assert.AreEqual(4, ctx.EffectiveAttack(a), "Burn 적 1");
            ctx.Enemies[1].Statuses.Apply(StatusType.Burn, 2, ctx);
            Assert.AreEqual(5, ctx.EffectiveAttack(a), "Burn 적 2");
            for (int i = 0; i < 3; i++)
            {
                var extra = new RuntimeEnemy("EX" + i, "ENE_1F_GHO_001", 10, 1, 0, false);
                ctx.Field.PlaceEnemy(extra, null, ctx);
                extra.Statuses.Apply(StatusType.Burn, 1, ctx);
            }
            Assert.AreEqual(6, ctx.EffectiveAttack(a), "5명이어도 최대 +3");

            var plain = Battle(new string[0]);
            var b = Ally(plain, SlotPosition.Front, atk: 3);
            plain.Enemies[0].Statuses.Apply(StatusType.Burn, 1, plain);
            Assert.AreEqual(3, plain.EffectiveAttack(b), "유물이 없으면 그대로");
        }

        [Test]
        public void Unc002_CommandAttackUsesEffectiveAttack()
        {
            var ctx = Battle(new[] { "REL_UNC_002" });
            var a = Ally(ctx, SlotPosition.Front, atk: 3);
            var front = ctx.Enemies[0];
            front.Statuses.Apply(StatusType.Burn, 1, ctx);
            int hp = front.CurrentHP;
            ctx.EndPlayerTurn();
            // ATK 3 + 1 = 4 − (DEF 1 + Front 1) = 2, 턴 종료 Burn 1 틱 → 3
            Assert.AreEqual(hp - 2 - 1, front.CurrentHP);
        }

        // ---------- REL_UNC_003 맹수의 발톱 (D-76) ----------

        [Test]
        public void Unc003_AttackKillAtkPlus1_Max5_CardKillNot()
        {
            var ctx = Battle(new[] { "REL_UNC_003" });
            var a = Ally(ctx, SlotPosition.Front, atk: 3);
            for (int i = 0; i < 7; i++)
            {
                var e = new RuntimeEnemy("K" + i, "ENE_1F_GHO_001", 1, 1, 0, false);
                ctx.Field.PlaceEnemy(e, SlotPosition.Extra2, ctx);
                Hit(ctx, a, e, 5, attack: true);
                Assert.IsTrue(e.IsDead);
            }
            Assert.AreEqual(3 + 5, a.Attack.FinalValue, "처치 7번 → 최대 +5");

            var b = Ally(ctx, SlotPosition.Middle, atk: 3);
            var c = new RuntimeEnemy("C", "ENE_1F_GHO_001", 1, 1, 0, false);
            ctx.Field.PlaceEnemy(c, SlotPosition.Extra2, ctx);
            Hit(ctx, ctx.Player, c, 5); // 카드 피해
            Assert.AreEqual(3, b.Attack.FinalValue, "카드 피해 처치는 아니다");
        }

        // ---------- 등록 ----------

        [Test]
        public void Registry_AllStage1HandlersPresent()
        {
            var raw = StageOneBattleSim.Data();
            CollectionAssert.IsEmpty(HandlerRegistry.Missing(HandlerKind.Relic, Stage1Scope.Relics, raw));
            var check = HandlerRegistry.CheckStage1(raw);
            CollectionAssert.IsEmpty(check.MissingCards);
            CollectionAssert.IsEmpty(check.MissingPending, "1단계 필수 핸들러 전부 (`38` §5)");
        }
    }
}
