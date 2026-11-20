using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 한 턴 흐름: 커맨드 공격/방어(D-27), 적 의도(D-28), 플레이어 피격(D-29), 적 행동표(`10` Enemy_Actions, D-55).
    /// 기대값은 손으로 계산한다 (`32` §11.2).
    /// </summary>
    [Category("Stage1")]
    public class TurnFlowTests
    {
        private BattleContext _ctx;
        private int _id;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BattleContext(21);
            _ctx.Cards = StageOneCards.Create();
            _ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], _ctx.Random));
            _ctx.EnemyKits = StageOneEnemies.Create();
        }

        private RuntimeMonster Ally(SlotPosition pos, int hp = 10, int atk = 3, int def = 1)
        {
            var m = new RuntimeMonster("A" + (++_id), "NEC_MON_001", hp, atk, def, null);
            _ctx.State = BattleState.PlayerPhase;
            _ctx.Field.TrySummonAlly(m, pos, _ctx);
            _ctx.Queue.Process();
            return m;
        }

        private RuntimeEnemy Enemy(string id, SlotPosition pos, int hp = 30, int atk = 5, int def = 0)
        {
            var e = new RuntimeEnemy("E" + (++_id), id, hp, atk, def, false);
            _ctx.Field.PlaceEnemy(e, pos, _ctx);
            return e;
        }

        // ---------- D-27 커맨드 ----------

        [Test]
        public void CMD_01_DefaultAttack_FrontEnemy_D01Damage()
        {
            var a = Ally(SlotPosition.Front, atk: 3);
            var e = Enemy("ENE_TEST_IDLE", SlotPosition.Front, hp: 20, def: 0);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(UnitCommand.Attack, a.Command, "기본값 공격");
            _ctx.EndPlayerTurn();
            // ATK 3 − (DEF 0 + Front 1) = 2
            Assert.AreEqual(18, e.CurrentHP);
        }

        [Test]
        public void CMD_01_DefendShieldIs3PlusCurrentDef_BossHitExample()
        {
            // D-27 예: 뼈 병사(DEF 1) Front 방어 → Shield 3 + 2 = 5. ATK 8 단일 → 8 − 2 = 6, Shield 5 → HP 피해 1
            var a = Ally(SlotPosition.Front, hp: 10, atk: 3, def: 1);
            Enemy("ENE_TEST_HIT8", SlotPosition.Front, atk: 8);
            _ctx.StartPlayerTurn();
            Assert.IsTrue(_ctx.SetCommand(a, UnitCommand.Defend));
            _ctx.EndPlayerTurn();
            Assert.AreEqual(9, a.CurrentHP);
            Assert.AreEqual(0, a.Shield, "턴 종료에 소멸");
        }

        [Test]
        public void CMD_01_ChangeAttackTarget_AndBindCannotChoose()
        {
            var a = Ally(SlotPosition.Front, atk: 5);
            var front = Enemy("ENE_TEST_IDLE", SlotPosition.Front, hp: 20);
            var back = Enemy("ENE_TEST_IDLE", SlotPosition.Back, hp: 20);
            _ctx.StartPlayerTurn();
            Assert.IsTrue(_ctx.SetAttackTarget(a, back));
            _ctx.EndPlayerTurn();
            Assert.AreEqual(20, front.CurrentHP);
            Assert.AreEqual(15, back.CurrentHP, "Back 적: DEF 0, 보너스 없음 → 5");

            _ctx.StartPlayerTurn();
            a.Statuses.Apply(StatusType.Bind, 1, _ctx);
            Assert.IsFalse(_ctx.SetCommand(a, UnitCommand.Defend), "Bind면 선택 불가");
        }

        [Test]
        public void CMD_ResolveOrder_FrontMiddleBack()
        {
            var back = Ally(SlotPosition.Back, atk: 4);
            var front = Ally(SlotPosition.Front, atk: 4);
            var e = Enemy("ENE_TEST_IDLE", SlotPosition.Front, hp: 3, def: 0);
            var e2 = Enemy("ENE_TEST_IDLE", SlotPosition.Middle, hp: 20, def: 0);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            // Front가 먼저 3 처치 → Back은 앞줄(Middle)로: 4
            Assert.IsTrue(e.IsDead);
            Assert.AreEqual(16, e2.CurrentHP);
        }

        [Test]
        public void CMD_TauntEnemyIsDefaultTarget()
        {
            var a = Ally(SlotPosition.Front, atk: 5);
            var front = Enemy("ENE_TEST_IDLE", SlotPosition.Front, hp: 20);
            var taunt = Enemy("ENE_TEST_IDLE", SlotPosition.Back, hp: 20);
            taunt.Statuses.Apply(StatusType.Taunt, 1, _ctx);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(15, taunt.CurrentHP);
            Assert.AreEqual(20, front.CurrentHP);
        }

        [Test]
        public void SUM_01_SummonedThisTurnActsThisTurn()
        {
            var e = Enemy("ENE_TEST_IDLE", SlotPosition.Front, hp: 20);
            _ctx.StartPlayerTurn();
            var a = Ally(SlotPosition.Front, atk: 3);
            _ctx.EndPlayerTurn();
            Assert.AreEqual(18, e.CurrentHP, "D-02: 소환 턴에 행동");
        }

        [Test]
        public void ExtraAction_AttacksTwice()
        {
            var a = Ally(SlotPosition.Front, atk: 3);
            var e = Enemy("ENE_TEST_IDLE", SlotPosition.Front, hp: 20);
            _ctx.StartPlayerTurn();
            a.ExtraActionsThisTurn = 1; // 신속한 지휘 (D-63)
            _ctx.EndPlayerTurn();
            Assert.AreEqual(16, e.CurrentHP);
        }

        // ---------- D-28 의도 ----------

        [Test]
        public void INT_01_IntentFixedAtTurnStart_KindOnly_TargetPickedAtExecution()
        {
            var front = Ally(SlotPosition.Front, hp: 10, def: 0);
            var back = Ally(SlotPosition.Back, hp: 10, def: 0);
            var e = Enemy("ENE_TEST_HIT8", SlotPosition.Front, atk: 8);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(e));
            // 플레이어 턴 중에 앞줄 아군이 죽으면 실행 때 다음 앞줄을 고른다
            _ctx.Queue.EnqueueMain(new DamageAction { Target = front, BaseDamage = 99 });
            _ctx.Queue.Process();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(10 - 8, back.CurrentHP);
        }

        [Test]
        public void INT_01_EnemySummonedInEnemyPhase_ActsFromNextEnemyPhase()
        {
            var a = Ally(SlotPosition.Front, hp: 50, def: 0);
            var elite = Enemy("ENE_1F_ELT_001", SlotPosition.Front, hp: 30, atk: 7);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Summon, _ctx.IntentOf(elite));
            _ctx.SetCommand(a, UnitCommand.Defend);
            _ctx.EndPlayerTurn();
            var summoned = _ctx.Enemies.Single(x => x != elite);
            Assert.AreEqual("ENE_1F_GHO_001", summoned.DataId);
            Assert.AreEqual(50, a.CurrentHP, "소환된 적은 이번 턴에 행동하지 않는다 (D-02)");
            _ctx.StartPlayerTurn();
            Assert.AreNotEqual(IntentKind.None, _ctx.IntentOf(summoned));
        }

        // ---------- D-29 플레이어 피격 ----------

        [Test]
        public void HIT_01_SingleAttackHitsAllyNotPlayer()
        {
            var a = Ally(SlotPosition.Front, hp: 20, def: 0);
            Enemy("ENE_TEST_HIT8", SlotPosition.Front, atk: 8);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(80, _ctx.Player.CurrentHP);
            Assert.AreEqual(20 - 7, a.CurrentHP, "8 − Front 1");
        }

        [Test]
        public void HIT_01_NoAlly_SingleAttackHitsPlayer_NoDef()
        {
            Enemy("ENE_TEST_HIT8", SlotPosition.Front, atk: 8);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(72, _ctx.Player.CurrentHP, "플레이어 DEF 0");
        }

        [Test]
        public void HIT_01_AoEHitsAllAlliesAndPlayer()
        {
            var a = Ally(SlotPosition.Front, hp: 20, def: 0);
            var b = Ally(SlotPosition.Back, hp: 20, def: 0);
            Enemy("ENE_TEST_AOE4", SlotPosition.Front);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(20 - 3, a.CurrentHP, "광역 4 − Front 1");
            Assert.AreEqual(16, b.CurrentHP);
            Assert.AreEqual(76, _ctx.Player.CurrentHP);
        }

        // ---------- 1~2층 적 행동표 (D-55) ----------

        [Test]
        public void AI_GHO_002_VenomBiteWhenTargetHasNoPoison()
        {
            var a = Ally(SlotPosition.Front, hp: 20, def: 0);
            Enemy("ENE_1F_GHO_002", SlotPosition.Front, atk: 6);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            // 독물기: ATK×0.5 = 3 − Front 1 = 2, Poison 2 → 턴 종료 틱 2
            Assert.AreEqual(20 - 2 - 2, a.CurrentHP);
            Assert.AreEqual(2, a.Statuses.Stacks(StatusType.Poison));
        }

        [Test]
        public void AI_GHO_003_SelfDestructWhenHpAtMost3()
        {
            var a = Ally(SlotPosition.Front, hp: 20, def: 0);
            var e = Enemy("ENE_1F_GHO_003", SlotPosition.Front, hp: 3, atk: 5);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.AoE, _ctx.IntentOf(e));
            _ctx.EndPlayerTurn();
            Assert.IsTrue(e.IsDead);
            Assert.AreEqual(77, _ctx.Player.CurrentHP, "광역 3, 플레이어 포함");
        }

        [Test]
        public void AI_FIR_003_ShieldThenTaunt()
        {
            Ally(SlotPosition.Front, hp: 50, atk: 0);
            var e = Enemy("ENE_2F_FIR_003", SlotPosition.Front, hp: 30);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Buff, _ctx.IntentOf(e));
            _ctx.EndPlayerTurn();
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Buff, _ctx.IntentOf(e), "Shield는 턴 종료에 사라짐 → 연속 금지라 Taunt");
            _ctx.EndPlayerTurn();
            Assert.IsTrue(e.Statuses.Has(StatusType.Taunt));
        }

        [Test]
        public void FullRound_1F_Node1_ReproducibleWithSeed()
        {
            string Run(int seed)
            {
                var c = new BattleContext(seed) { Cards = StageOneCards.Create(), EnemyKits = StageOneEnemies.Create() };
                c.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], c.Random));
                c.State = BattleState.PlayerPhase;
                var m = new RuntimeMonster("A", "NEC_MON_001", 10, 3, 1, null);
                c.Field.TrySummonAlly(m, SlotPosition.Front, c);
                c.Queue.Process();
                c.Field.PlaceEnemy(new RuntimeEnemy("E1", "ENE_1F_GHO_001", 14, 5, 1, false), SlotPosition.Front, c);
                c.Field.PlaceEnemy(new RuntimeEnemy("E2", "ENE_1F_GHO_001", 14, 5, 1, false), SlotPosition.Middle, c);
                var log = new List<string>();
                for (int t = 0; t < 5 && c.Outcome == BattleOutcome.None; t++)
                {
                    c.StartPlayerTurn();
                    c.EndPlayerTurn();
                    log.Add($"{m.CurrentHP}/{c.Player.CurrentHP}/{string.Join(",", c.Enemies.Select(e => e.CurrentHP))}");
                }
                return string.Join("|", log);
            }
            Assert.AreEqual(Run(5), Run(5), "seed 5");
        }

        // ---------- 리뷰 반영 ----------

        [Test]
        public void ST_03_EnemyShieldFromEnemyPhase_LastsThroughNextPlayerTurn()
        {
            // D-64: Enemy Phase에 얻은 Shield는 그 턴 종료에 사라지지 않고 다음 턴 종료까지 남는다
            var a = Ally(SlotPosition.Front, hp: 50, atk: 3);
            var e = Enemy("ENE_2F_FIR_003", SlotPosition.Front, hp: 30, def: 0);
            _ctx.StartPlayerTurn();
            _ctx.SetCommand(a, UnitCommand.Defend);
            _ctx.EndPlayerTurn();          // 적: Shield +4
            Assert.AreEqual(4, e.Shield);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();          // 아군 공격 3 − (DEF 0 + Front 1) = 2 → Shield 4 → 2
            Assert.AreEqual(30, e.CurrentHP);
            Assert.AreEqual(0, e.Shield, "다음 턴 종료에 소멸");
        }

        [Test]
        public void INT_SummonIntent_FalseWhenEnemySlotsFull()
        {
            Ally(SlotPosition.Front, hp: 50);
            var elite = Enemy("ENE_1F_ELT_001", SlotPosition.Front, hp: 30, atk: 7);
            foreach (var pos in new[] { SlotPosition.Middle, SlotPosition.Back, SlotPosition.Extra1, SlotPosition.Extra2 })
                Enemy("ENE_TEST_IDLE", pos);
            _ctx.StartPlayerTurn();
            Assert.AreNotEqual(IntentKind.Summon, _ctx.IntentOf(elite), "칸이 없으면 소환 조건 거짓 (Enemy_Actions Note)");
        }

        [Test]
        public void INT_AttackPlusStatus_IsSpecialIcon()
        {
            Ally(SlotPosition.Front, hp: 20);
            var e = Enemy("ENE_1F_GHO_002", SlotPosition.Front);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Special, _ctx.IntentOf(e), "D-52: 피해 + 디버프처럼 두 칸에 걸치면 특수");
        }

        [Test]
        public void AI_StatusTargetPrefersUnitWithoutThatStatus()
        {
            var low = Ally(SlotPosition.Front, hp: 5, def: 0);
            var other = Ally(SlotPosition.Back, hp: 20, def: 0);
            low.Statuses.Apply(StatusType.Poison, 1, _ctx);
            Enemy("ENE_1F_GHO_002", SlotPosition.Front, atk: 6);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(2, other.Statuses.Stacks(StatusType.Poison), "독 없는 아군에게 독물기 (`12` §7 상태이상 대상 우선)");
        }

        [Test]
        public void AI_StunnedAfterIntent_SkipsAndRotationUnchanged()
        {
            var a = Ally(SlotPosition.Front, hp: 50, atk: 0);
            var e = Enemy("ENE_1F_ELT_002", SlotPosition.Front, hp: 50);
            _ctx.StartPlayerTurn();                         // 의도: POISON (1순위)
            e.Statuses.Apply(StatusType.Stun, 1, _ctx);    // 플레이어 턴 중 기절
            _ctx.EndPlayerTurn();
            Assert.AreEqual(0, a.Statuses.Stacks(StatusType.Poison));
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(2, a.Statuses.Stacks(StatusType.Poison), "D-55 #4: 순환 위치 그대로 → 다시 POISON");
        }

        [Test]
        public void CMD_BoundAllyCannotChangeTarget()
        {
            var a = Ally(SlotPosition.Front);
            var e = Enemy("ENE_TEST_IDLE", SlotPosition.Back);
            _ctx.StartPlayerTurn();
            a.Statuses.Apply(StatusType.Bind, 1, _ctx);
            Assert.IsFalse(_ctx.SetAttackTarget(a, e));
        }
    }
}
