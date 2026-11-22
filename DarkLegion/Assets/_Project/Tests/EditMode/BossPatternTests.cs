using System.IO;
using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 1~2층 보스 패턴 (#45, `22` §2~§3, `14_보스_패턴.xlsx` Boss_TurnCycle, D-18·D-28·D-29·D-65).
    /// 기대값은 손으로 계산한다 (`32` §11.2). 피해 = max(1, ATK − (DEF + Front 1)) (D-01).
    /// </summary>
    [Category("Stage1")]
    public class BossPatternTests
    {
        private BattleContext _ctx;
        private int _id;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BattleContext(7);
            _ctx.Cards = StageOneCards.Create();
            _ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], _ctx.Random));
            _ctx.EnemyKits = StageOneEnemies.Create();
            _ctx.BossPatterns = StageOneBosses.Create();
        }

        private RuntimeMonster Ally(SlotPosition pos, int hp = 30, int atk = 0, int def = 0)
        {
            var m = new RuntimeMonster("A" + (++_id), "NEC_MON_001", hp, atk, def, null);
            _ctx.State = BattleState.PlayerPhase;
            _ctx.Field.TrySummonAlly(m, pos, _ctx);
            _ctx.Queue.Process();
            return m;
        }

        private RuntimeEnemy Boss(string id = "ENE_1F_BOS_001", int hp = 47, int atk = 8, int def = 1)
        {
            var e = new RuntimeEnemy("B" + (++_id), id, hp, atk, def, true);
            _ctx.Field.PlaceEnemy(e, SlotPosition.Front, _ctx);
            return e;
        }

        private void Turn()
        {
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
        }

        [Test]
        public void BOSS_1F_P1_T0_SingleAttackOnFront()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 1);
            var boss = Boss();
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss));
            Assert.AreEqual(1, _ctx.BossPhaseOf(boss));
            _ctx.EndPlayerTurn();
            // ATK 8 − (DEF 1 + Front 1) = 6
            Assert.AreEqual(24, front.CurrentHP);
        }

        [Test]
        public void BOSS_1F_P1_CycleIsSingle_WeakOnBack_Single_ThenRepeats()
        {
            Ally(SlotPosition.Front);
            var back = Ally(SlotPosition.Back);
            var boss = Boss();
            var kinds = new System.Collections.Generic.List<IntentKind>();
            for (int i = 0; i < 4; i++)
            {
                _ctx.StartPlayerTurn();
                kinds.Add(_ctx.IntentOf(boss));
                _ctx.EndPlayerTurn();
                if (i == 1) Assert.AreEqual(2, back.Statuses.Stacks(StatusType.Weak), "Weak 2턴: 적이 건 지속형은 그 턴 종료에 줄지 않는다 → 다음 플레이어 턴 2번 적용");
            }
            CollectionAssert.AreEqual(new[] { IntentKind.SingleAttack, IntentKind.Debuff, IntentKind.SingleAttack, IntentKind.SingleAttack }, kinds);
        }

        [TestCase(24, 1)]
        [TestCase(23, 2)]
        [TestCase(10, 2)]
        [TestCase(9, 3)]
        public void BOSS_PhaseBoundary_HalfAndFifthOfMaxHp(int hp, int expectedPhase)
        {
            // 47의 50% = 23.5 → 23 이하 Phase 2, 20% = 9.4 → 9 이하 Phase 3 (`12` §6.2 "이하")
            Ally(SlotPosition.Front);
            var boss = Boss();
            boss.CurrentHP = hp;
            _ctx.StartPlayerTurn();
            Assert.AreEqual(expectedPhase, _ctx.BossPhaseOf(boss));
        }

        [Test]
        public void BOSS_D28_PhaseChangeDuringPlayerTurn_AppliesNextTurn_FromT0()
        {
            Ally(SlotPosition.Front, hp: 30, atk: 5);       // 보스 공격: 5 − (1 + 1) = 3
            var boss = Boss();
            boss.CurrentHP = 26;
            int changes = 0;
            _ctx.BossPhaseChanged += (e, p) => changes++;
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss), "Phase 1 T0");
            _ctx.EndPlayerTurn();
            Assert.AreEqual(23, boss.CurrentHP, "플레이어 턴 중 50% 아래로");
            Assert.AreEqual(1, _ctx.BossPhaseOf(boss), "이번 턴은 이미 확정한 Phase 1 행동");
            _ctx.StartPlayerTurn();
            Assert.AreEqual(2, _ctx.BossPhaseOf(boss));
            Assert.AreEqual(1, changes);
            Assert.AreEqual(IntentKind.Summon, _ctx.IntentOf(boss), "새 페이즈는 T0(소환)부터 (D-65)");
        }

        private RuntimeEnemy Idle(SlotPosition pos)
        {
            var e = new RuntimeEnemy("I" + (++_id), "ENE_TEST_IDLE", 30, 0, 0, false);
            _ctx.Field.PlaceEnemy(e, pos, _ctx);
            return e;
        }

        [Test]
        public void BOSS_1F_P2_T0_SummonsHungryGhoulIntoFreeSlot()
        {
            Ally(SlotPosition.Front);
            var boss = Boss();
            boss.CurrentHP = 20;
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Summon, _ctx.IntentOf(boss));
            _ctx.EndPlayerTurn();
            var ghoul = _ctx.Enemies.Single(e => e != boss);
            Assert.AreEqual("ENE_1F_GHO_001", ghoul.DataId, "Note \"GHO_001 ×1\" = 같은 층 굶주린 구울");
            Assert.AreEqual(14, ghoul.MaxHP.FinalValue);
            Assert.AreEqual(5, ghoul.Attack.FinalValue);
            Assert.AreSame(boss, ghoul.SummonedBy);
            Assert.IsFalse(ghoul.IsBoss);
        }

        [Test]
        public void BOSS_1F_P2_NoFreeSlot_SummonDoesNothing_ThenDrain_ThenAoE6HitsAlliesAndPlayer()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var back = Ally(SlotPosition.Back, hp: 12, def: 0);
            var boss = Boss();
            var dummy = Idle(SlotPosition.Middle);
            Idle(SlotPosition.Back);
            Idle(SlotPosition.Extra1);
            Idle(SlotPosition.Extra2);
            boss.CurrentHP = 23;                                        // 47의 50% 이하 → Phase 2

            // 아군은 ATK 0이라도 최소 1 피해(D-01)라 보스 HP가 흔들리지 않게 허수아비를 친다
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Summon, _ctx.IntentOf(boss));
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();                                       // T0 소환: 빈 칸 없음 → 아무 일 없음
            Assert.AreEqual(5, _ctx.Enemies.Count(e => !e.IsDead), "적 칸 5개가 다 차 있음");
            Assert.AreEqual(23, boss.CurrentHP);

            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Special, _ctx.IntentOf(boss), "흡혈 = 특수 (D-48)");
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            // T1 흡혈: HP가 가장 낮은 Back(12)에 8 − 0 = 8 → 4. 회복 floor(8 × 50%) = 4 → 27 (50% 초과)
            Assert.AreEqual(4, back.CurrentHP);
            Assert.AreEqual(30, front.CurrentHP);
            Assert.AreEqual(27, boss.CurrentHP);

            _ctx.StartPlayerTurn();
            Assert.AreEqual(2, _ctx.BossPhaseOf(boss), "회복으로 50%를 넘어도 페이즈는 내려가지 않는다 (D-65)");
            Assert.AreEqual(IntentKind.AoE, _ctx.IntentOf(boss));
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            // T2 광역 6: Front 6 − (0 + 1) = 5, Back 6 ≥ 4 → 사망, 플레이어 6 (DEF 0, D-29)
            Assert.AreEqual(25, front.CurrentHP);
            Assert.IsTrue(back.IsDead);
            Assert.AreEqual(74, _ctx.Player.CurrentHP);
        }

        [Test]
        public void BOSS_01_1F_P3_TwoHitsPerTurn_KillsFullHpLv1FrontEachTurn()
        {
            // D-18: 뼈 병사 Lv1 (HP 10, DEF 1) Front: 8 − (1 + 1) = 6 × 2 = 12 ≥ 10 → 사망. 초과 피해는 넘어가지 않고
            // 두 번째 공격은 새 Front(Middle 칸의 아군)를 다시 고른다
            var first = Ally(SlotPosition.Front, hp: 10, def: 1);
            var second = Ally(SlotPosition.Middle, hp: 10, def: 1);
            var boss = Boss();
            boss.CurrentHP = 9;
            _ctx.StartPlayerTurn();
            Assert.AreEqual(3, _ctx.BossPhaseOf(boss));
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss));
            _ctx.EndPlayerTurn();
            Assert.IsTrue(first.IsDead || second.IsDead, "턴당 1체 사망");
            Assert.AreEqual(80, _ctx.Player.CurrentHP, "아군이 남아 있으면 단일 공격은 플레이어를 맞히지 않는다 (D-29)");
        }

        [Test]
        public void BOSS_BindOnBoss_NoAction_CycleDoesNotAdvance()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var boss = Boss();
            _ctx.StartPlayerTurn();                                      // T0 단일 공격 확정
            boss.Statuses.Apply(StatusType.Bind, 1, _ctx);               // 플레이어 턴 중 Bind
            _ctx.EndPlayerTurn();
            Assert.AreEqual(30, front.CurrentHP, "행동 없음");
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss), "사이클은 T0 그대로");
        }

        [Test]
        public void BOSS_2F_P1_T0_BurnPlus2OnAllAlliesAndPlayer()
        {
            var front = Ally(SlotPosition.Front, hp: 30);
            var back = Ally(SlotPosition.Back, hp: 30);
            var boss = Boss("ENE_2F_BOS_001", 59, 9, 2);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.Debuff, _ctx.IntentOf(boss), "전장 Burn = 디버프 (D-48)");
            _ctx.EndPlayerTurn();
            // 턴 종료 틱: Burn 2 피해 후 1 남음 (`11`)
            Assert.AreEqual(28, front.CurrentHP);
            Assert.AreEqual(28, back.CurrentHP);
            Assert.AreEqual(78, _ctx.Player.CurrentHP);
            Assert.AreEqual(1, _ctx.Player.Statuses.Stacks(StatusType.Burn));
        }

        [Test]
        public void BOSS_2F_P3_BurnAndSingleAttackSameTurn_IconIsSingleAttack()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var boss = Boss("ENE_2F_BOS_001", 59, 9, 2);
            boss.CurrentHP = 11;                                         // 59 × 20% = 11.8 → 11은 Phase 3
            _ctx.StartPlayerTurn();
            Assert.AreEqual(3, _ctx.BossPhaseOf(boss));
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss), "D-52: 피해가 있는 행동의 아이콘");
            _ctx.EndPlayerTurn();
            // Burn+1 → 단일 9 − (0 + 1) = 8 → 턴 종료 Burn 1 틱
            Assert.AreEqual(30 - 8 - 1, front.CurrentHP);
            Assert.AreEqual(79, _ctx.Player.CurrentHP, "전장 Burn은 플레이어에게도 (D-29)");
        }

        [Test]
        public void BOSS_2F_P2_T1_SingleAttackTwice()
        {
            var front = Ally(SlotPosition.Front, hp: 40, def: 0);
            var boss = Boss("ENE_2F_BOS_001", 59, 9, 2);
            boss.CurrentHP = 29;
            Turn();                                                      // T0 Burn+3 → 틱 3, 남은 2
            Assert.AreEqual(37, front.CurrentHP);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss));
            _ctx.EndPlayerTurn();
            // 8 × 2 = 16, 턴 종료 Burn 2 틱 → 37 − 16 − 2 = 19
            Assert.AreEqual(19, front.CurrentHP);
        }

        // ---------- BOSS-03 (32 1단계 Must Pass, D-65) ----------

        [Test]
        public void BOSS_03_1F_P2_SummonsOneGhoul_14_5_1_NoNodeScale()
        {
            // 노드 9(인덱스 8)로 스케일하면 HP는 HalfUp(14×1.184)=17이다. 소환은 키트 원값이다.
            Ally(SlotPosition.Front);
            var boss = Boss();
            boss.CurrentHP = 20;
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            var ghoul = _ctx.Enemies.Single(e => e != boss);
            Assert.AreEqual("ENE_1F_GHO_001", ghoul.DataId);
            Assert.AreEqual(14, ghoul.MaxHP.FinalValue);
            Assert.AreEqual(5, ghoul.Attack.FinalValue);
            Assert.AreEqual(1, ghoul.Defense.FinalValue);
        }

        [Test]
        public void BOSS_03_1F_P2_FullSlots_NoSummon()
        {
            Ally(SlotPosition.Front, hp: 40, atk: 0);
            var boss = Boss();
            boss.CurrentHP = 20;
            Idle(SlotPosition.Middle);
            Idle(SlotPosition.Back);
            Idle(SlotPosition.Extra1);
            Idle(SlotPosition.Extra2);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(5, _ctx.Enemies.Count(e => !e.IsDead));
        }

        [Test]
        public void BOSS_03_1F_LifeSteal_Hp12_Heals4_AndStaysPhase2AboveHalf()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var back = Ally(SlotPosition.Back, hp: 12, def: 0);
            var boss = Boss();
            var dummy = Idle(SlotPosition.Middle);
            Idle(SlotPosition.Back);
            Idle(SlotPosition.Extra1);
            Idle(SlotPosition.Extra2);
            boss.CurrentHP = 23;
            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            // Back DEF 0, 위치 보너스 없음. ATK 8 − 0 = 8. floor(8 × 50%) = 4. 23 + 4 = 27
            Assert.AreEqual(4, back.CurrentHP);
            Assert.AreEqual(27, boss.CurrentHP);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(2, _ctx.BossPhaseOf(boss), "27은 47의 50%를 넘어도 P2");
        }

        [Test]
        public void BOSS_03_1F_LifeSteal_Hp4_Heals2()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var back = Ally(SlotPosition.Back, hp: 4, def: 0);
            var boss = Boss();
            var dummy = Idle(SlotPosition.Middle);
            Idle(SlotPosition.Back);
            Idle(SlotPosition.Extra1);
            Idle(SlotPosition.Extra2);
            boss.CurrentHP = 23;
            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            // 피해 8 중 실제로 깎인 HP는 4. floor(4 × 50%) = 2. 23 + 2 = 25
            Assert.IsTrue(back.IsDead);
            Assert.AreEqual(25, boss.CurrentHP);
        }

        [Test]
        public void BOSS_03_1F_LifeSteal_ShieldBlocksHeal()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var back = Ally(SlotPosition.Back, hp: 12, def: 0);
            var boss = Boss();
            var dummy = Idle(SlotPosition.Middle);
            Idle(SlotPosition.Back);
            Idle(SlotPosition.Extra1);
            Idle(SlotPosition.Extra2);
            boss.CurrentHP = 23;
            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            _ctx.StartPlayerTurn();
            back.Statuses.Apply(StatusType.Shield, 6, _ctx);
            _ctx.SetAttackTarget(front, dummy);
            _ctx.SetAttackTarget(back, dummy);
            _ctx.EndPlayerTurn();
            // ATK 8, Shield 6, HP로 들어간 2. floor(2 × 50%) = 1. 23 + 1 = 24. Back 12 − 2 = 10
            Assert.AreEqual(10, back.CurrentHP);
            Assert.AreEqual(24, boss.CurrentHP);
        }

        [Test]
        public void BOSS_03_2F_P1_Burn2ThenSingle7ThenAoE4OnPlayer()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 1);
            var boss = Boss("ENE_2F_BOS_001", 59, 9, 2);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            // 전장 Burn+2, 틱 2. 플레이어 80 − 2 = 78, Front 30 − 2 = 28, 스택 1
            Assert.AreEqual(78, _ctx.Player.CurrentHP);
            Assert.AreEqual(28, front.CurrentHP);
            Assert.AreEqual(1, _ctx.Player.Statuses.Stacks(StatusType.Burn));
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            // 단일 max(1, floor(9 − 2)) = 7, 남은 Burn 틱 1. 28 − 7 − 1 = 20. 플레이어 78 − 1 = 77
            Assert.AreEqual(20, front.CurrentHP);
            Assert.AreEqual(77, _ctx.Player.CurrentHP);
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            // 광역 4, 플레이어 DEF 0. 77 − 4 = 73
            Assert.AreEqual(73, _ctx.Player.CurrentHP);
        }

        [Test]
        public void BOSS_03_2F_P3_Burn1AndSingleSameTurn()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var boss = Boss("ENE_2F_BOS_001", 59, 9, 2);
            boss.CurrentHP = 11;
            _ctx.StartPlayerTurn();
            Assert.AreEqual(3, _ctx.BossPhaseOf(boss));
            _ctx.EndPlayerTurn();
            // 같은 턴 Burn+1과 단일. 단일 max(1, 9 − 1) = 8, 틱 1. 30 − 8 − 1 = 21. 플레이어 80 − 1 = 79
            Assert.AreEqual(21, front.CurrentHP);
            Assert.AreEqual(79, _ctx.Player.CurrentHP);
        }

        [Test]
        public void BOSS_03_BindOrStun_StopsActionAndCycle()
        {
            var front = Ally(SlotPosition.Front, hp: 30, def: 0);
            var boss = Boss();
            _ctx.StartPlayerTurn();
            boss.Statuses.Apply(StatusType.Bind, 1, _ctx);
            _ctx.EndPlayerTurn();
            Assert.AreEqual(30, front.CurrentHP);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss));
            boss.Statuses.Apply(StatusType.Stun, 1, _ctx);
            _ctx.EndPlayerTurn();
            Assert.AreEqual(30, front.CurrentHP, "Stun도 확정된 단일 공격을 하지 않는다");
            _ctx.StartPlayerTurn();
            Assert.AreEqual(IntentKind.SingleAttack, _ctx.IntentOf(boss), "사이클도 T0 그대로");
        }

        // ---------- 시뮬 (#45 "각 보스 3회 이상", #28 측정 항목: 플레이어 HP 손실, D-29) ----------

        [TestCase("ENE_1F_BOS_001", 47, 8, 1, 1)]
        [TestCase("ENE_1F_BOS_001", 47, 8, 1, 2)]
        [TestCase("ENE_1F_BOS_001", 47, 8, 1, 3)]
        [TestCase("ENE_2F_BOS_001", 59, 9, 2, 1)]
        [TestCase("ENE_2F_BOS_001", 59, 9, 2, 2)]
        [TestCase("ENE_2F_BOS_001", 59, 9, 2, 3)]
        public void BOSS_Sim_FixedTeam_FightEnds_PassesPhase2_RecordsPlayerHpLoss(string bossId, int hp, int atk, int def, int seed)
        {
            // 고정 팀: 아군 3체 HP 20 · ATK 6 · DEF 1, 카드 없이 커맨드 공격만. 수치 판단용이 아니라 패턴이 끝까지 도는지 보는 시뮬
            _ctx = new BattleContext(seed)
            {
                Cards = StageOneCards.Create(),
                EnemyKits = StageOneEnemies.Create(),
                BossPatterns = StageOneBosses.Create()
            };
            _ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], _ctx.Random));
            foreach (var pos in new[] { SlotPosition.Front, SlotPosition.Middle, SlotPosition.Back }) Ally(pos, hp: 20, atk: 6, def: 1);
            var boss = Boss(bossId, hp, atk, def);
            var phases = new System.Collections.Generic.List<int>();
            _ctx.BossPhaseChanged += (e, p) => phases.Add(p);

            int turns = 0;
            while (_ctx.Outcome == BattleOutcome.None && turns < 30)
            {
                turns++;
                Turn();
            }
            int loss = 80 - System.Math.Max(0, _ctx.Player.CurrentHP);
            TestContext.Progress.WriteLine($"[SIM] {bossId} seed {seed}: {_ctx.Outcome} in {turns} turns, phases {string.Join(">", phases)}, player HP loss {loss}, allies alive {_ctx.Allies.Count(a => !a.IsDead)}");
            Assert.AreNotEqual(BattleOutcome.None, _ctx.Outcome, "30턴 안에 끝난다");
            CollectionAssert.Contains(phases, 2);
            CollectionAssert.AreEqual(phases.OrderBy(x => x).ToList(), phases, "페이즈는 올라가기만 한다");
        }

        // ---------- D-79. 3층 P2 소환은 진입 첫 T0만 (`32` BOSS-04의 소환 절) ----------

        private static GameDataLoadResult LoadDesign()
        {
            string dir = DesignDataParityTests.FindDesignData();
            Assert.IsNotNull(dir, "DarkLegion/DesignData/*.json 없음");
            var texts = Directory.GetFiles(dir, "*.json").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => File.ReadAllText(f));
            return GameDataLoader.Load(GameData.FromJson(texts));
        }

        private int CountLiving(string dataId) => _ctx.Enemies.Count(e => !e.IsDead && e.DataId == dataId);
        [Test]
        public void BOSS_3F_P2_SummonsFls003Once_OnFirstT0Only()
        {
            // 73의 50% = 36.5 → HP 36은 P2, 20% = 14.6 → 36은 P3가 아니다 (D-65)
            var loaded = LoadDesign();
            Assert.IsEmpty(loaded.Errors, string.Join("\n", loaded.Errors));
            var steps = loaded.BossPatterns.Get("ENE_3F_BOS_001");
            var summon = steps.Single(s => s.Phase == 2 && s.Action == "Summon");
            Assert.AreEqual("FLS_003 ×1", summon.Note);
            Assert.IsTrue(summon.OnceOnPhaseEnter, "3층 P2 소환 행은 OnceOnPhaseEnter");
            var heal = steps.Single(s => s.Action == "SelfHeal");
            Assert.AreEqual((3, 6, "Self"), (heal.Phase, heal.Damage, heal.TargetRule));
            Assert.AreEqual(30, loaded.BossPatterns.Get("ENE_4F_BOS_001").Single(s => s.Action == "Execute").ThresholdPct);
            Assert.AreEqual("CUR_001", loaded.BossPatterns.Get("ENE_5F_BOS_001").Single(s => s.Action == "AddCurseCard").CurseId);

            _ctx.BossPatterns = loaded.BossPatterns;
            _ctx.EnemyKits = loaded.EnemyKits;
            var front = Ally(SlotPosition.Front, hp: 40, atk: 0, def: 1);
            var boss = Boss("ENE_3F_BOS_001", hp: 73, atk: 11, def: 2);
            boss.CurrentHP = 36;
            var dummy = new RuntimeEnemy("D" + (++_id), "ENE_TEST_IDLE", 40, 0, 0, false);
            _ctx.Field.PlaceEnemy(dummy, SlotPosition.Middle, _ctx);

            _ctx.StartPlayerTurn();
            Assert.AreEqual(2, _ctx.BossPhaseOf(boss));
            _ctx.SetAttackTarget(front, dummy);
            _ctx.EndPlayerTurn();
            Assert.AreEqual(1, CountLiving("ENE_3F_FLS_003"), "진입 첫 T0에 FLS_003 ×1");
            var flesh = _ctx.Enemies.Single(e => e.DataId == "ENE_3F_FLS_003");
            Assert.AreEqual((18, 7, 1), (flesh.MaxHP.FinalValue, flesh.Attack.FinalValue, flesh.Defense.FinalValue));
            // 광역 6: Front DEF 1+1 → max(1, floor(6−2))=4, 플레이어 DEF 0 → 6
            Assert.AreEqual(36, front.CurrentHP);
            Assert.AreEqual(74, _ctx.Player.CurrentHP);

            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, dummy);
            _ctx.EndPlayerTurn();
            Assert.AreEqual(1, CountLiving("ENE_3F_FLS_003"), "T1 흡혈 턴에는 소환이 없다");

            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, dummy);
            _ctx.EndPlayerTurn();
            Assert.AreEqual(1, CountLiving("ENE_3F_FLS_003"), "다음 T0는 광역만, 소환은 다시 하지 않는다");
            Assert.AreEqual(68, _ctx.Player.CurrentHP, "광역 6이 한 번 더");
            Assert.AreEqual(2, _ctx.BossPhaseOf(boss), "흡혈로 50%를 넘어도 P2");
        }

        [Test]
        public void BOSS_3F_P2_FullSlots_StillDoesNotSummonOnLaterT0()
        {
            var loaded = LoadDesign();
            _ctx.BossPatterns = loaded.BossPatterns;
            _ctx.EnemyKits = loaded.EnemyKits;
            var front = Ally(SlotPosition.Front, hp: 40, atk: 5, def: 1);
            var boss = Boss("ENE_3F_BOS_001", hp: 73, atk: 11, def: 2);
            boss.CurrentHP = 36;
            var victim = new RuntimeEnemy("V" + (++_id), "ENE_TEST_IDLE", 1, 0, 0, false);
            _ctx.Field.PlaceEnemy(victim, SlotPosition.Middle, _ctx);
            RuntimeEnemy tank = null;
            foreach (var pos in new[] { SlotPosition.Back, SlotPosition.Extra1, SlotPosition.Extra2 })
            {
                var idle = new RuntimeEnemy("I" + (++_id), "ENE_TEST_IDLE", 30, 0, 0, false);
                if (tank == null) tank = idle;
                _ctx.Field.PlaceEnemy(idle, pos, _ctx);
            }
            Assert.AreEqual(5, _ctx.Enemies.Count(e => !e.IsDead));

            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, tank);
            _ctx.EndPlayerTurn();
            Assert.AreEqual(0, CountLiving("ENE_3F_FLS_003"), "칸이 없으면 소환은 무효");
            Assert.AreEqual(5, _ctx.Enemies.Count(e => !e.IsDead));

            _ctx.StartPlayerTurn();
            _ctx.SetAttackTarget(front, victim);
            _ctx.EndPlayerTurn();
            Assert.IsTrue(victim.IsDead);
            Assert.AreEqual(0, CountLiving("ENE_3F_FLS_003"));

            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(0, CountLiving("ENE_3F_FLS_003"), "실패한 첫 T0로 기회를 썼으므로 빈 칸이 생겨도 다시 소환하지 않는다");
        }

        [Test]
        public void BOSS_SummonNoteWithoutOnceFlag_RepeatsOnLaterT0()
        {
            var catalog = new BossPatternCatalog();
            catalog.Add("ENE_1F_BOS_001", new BossStep(2, 0, "Summon", 0, null, null, false, "GHO_001 ×1", onceOnPhaseEnter: false));
            _ctx.BossPatterns = catalog;
            Ally(SlotPosition.Front, hp: 40, atk: 0);
            var boss = Boss();
            boss.CurrentHP = 20;
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            _ctx.StartPlayerTurn();
            _ctx.EndPlayerTurn();
            Assert.AreEqual(2, CountLiving("ENE_1F_GHO_001"), "OnceOnPhaseEnter가 아니면 T0마다 소환 (1층 P2·5층 P2)");
        }

        [Test]
        public void BOSS_3F_P3_SelfHealRow_DoesNotThrow()
        {
            // SelfHeal·Execute 같은 3~5층 전용 행동은 아직 효과 없음. 행을 만나도 턴이 깨지지 않아야 한다.
            var loaded = LoadDesign();
            _ctx.BossPatterns = loaded.BossPatterns;
            _ctx.EnemyKits = loaded.EnemyKits;
            Ally(SlotPosition.Front, hp: 40, def: 1);
            var boss = Boss("ENE_3F_BOS_001", hp: 73, atk: 11, def: 2);
            boss.CurrentHP = 14; // 73×20 = 1460, 14×100 = 1400 → P3
            Assert.DoesNotThrow(() =>
            {
                _ctx.StartPlayerTurn();
                Assert.AreEqual(3, _ctx.BossPhaseOf(boss));
                _ctx.EndPlayerTurn();
            });
        }

    }
}
