using System.Linq;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 1단계 몬스터 카드 (#39, D-44 32장 중 몬스터 20장). 효과는 `09` Monster_Effects (D-61), 해석은 D-70.
    /// 스탯은 `08` Cards_Master·Cards_Upgrade. 기대값은 손으로 계산한다 (`32` §11.2).
    /// </summary>
    [Category("Stage1")]
    public class MonsterEffectTests
    {
        private BattleContext _ctx;
        private int _id;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BattleContext(31) { Cards = StageOneCards.Create(), Fusion = StageOneFusion.Create() };
            _ctx.Player = new RuntimePlayer("P", 80, new RuntimeDeck(new RuntimeCard[0], _ctx.Random));
            _ctx.StartPlayerTurn(); // Crystal 3, 빈 덱이라 드로우 없음
        }

        private RuntimeCard InHand(string cardId, bool upgraded = false)
        {
            var card = _ctx.Cards.Get(cardId).CreateCard(++_id, upgraded);
            _ctx.Player.Deck.AddToHand(card);
            return card;
        }

        /// <summary>카드를 내서 소환한다. 비용만큼 Crystal을 먼저 채워 Crystal 계산에 영향을 주지 않는다.</summary>
        private RuntimeMonster Summon(string cardId, SlotPosition pos, bool upgraded = false, RuntimeUnit target = null)
        {
            var card = InHand(cardId, upgraded);
            _ctx.Player.CurrentCrystal += card.Cost;
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(card, target, pos), cardId);
            _ctx.Queue.Process();
            return (RuntimeMonster)_ctx.Field.At(TeamType.Player, pos);
        }

        /// <summary>행동표가 없는 적 (의도 없음 → 공격하지 않는다).</summary>
        private RuntimeEnemy Enemy(SlotPosition pos, int hp = 30, int def = 0)
        {
            var e = new RuntimeEnemy("E" + (++_id), "ENE_TEST", hp, 5, def, false);
            _ctx.Field.PlaceEnemy(e, pos, _ctx);
            return e;
        }

        // ---------- 1단계 32장 픽스처 (D-44) ----------

        [Test]
        public void Fixture_HasAllStageOne32Cards()
        {
            string[] ids =
            {
                "NEC_ACT_001", "NEC_ACT_002", "NEC_ACT_003", "NEC_ACT_004", "NEC_MON_001", "NEC_MON_002", "NEC_MON_003", "NEC_MON_004",
                "NEC_MON_005", "NEC_MON_006", "NEC_MON_007", "NEC_MON_008", "NEC_MON_009", "NEC_MON_010", "NEC_MON_013", "NEC_MON_014",
                "NEC_SKL_001", "NEC_SKL_002", "NEC_SKL_003", "NEC_SKL_004", "NEU_ACT_001", "NEU_ACT_002", "NEU_MON_001", "NEU_MON_002",
                "NEU_MON_003", "NEU_MON_004", "NEU_MON_005", "NEU_MON_006", "NEU_MON_007", "NEU_MON_008", "NEU_SKL_001", "NEU_SKL_002"
            };
            Assert.AreEqual(32, ids.Distinct().Count());
            foreach (var id in ids) Assert.IsNotNull(_ctx.Cards.Get(id), id);
            Assert.AreEqual(20, ids.Count(id => _ctx.Cards.Get(id).Type == CardType.Monster), "몬스터 20장");
        }

        // ---------- 효과 없는 기본 몬스터: 스탯만 (Cards_Master, Cards_Upgrade Up_Params) ----------

        [TestCase("NEC_MON_001", 1, 1, 10, 3, 1, 12, 3, 1)]
        [TestCase("NEC_MON_006", 1, 1, 5, 3, 3, 7, 3, 3)]
        [TestCase("NEC_MON_008", 2, 2, 20, 6, 2, 22, 6, 2)]
        [TestCase("NEU_MON_003", 1, 1, 11, 4, 1, 13, 4, 1)]
        [TestCase("NEU_MON_005", 1, 1, 1, 8, 0, 3, 8, 0)]
        [TestCase("NEU_MON_006", 1, 1, 9, 3, 2, 11, 3, 2)]
        public void BasicMonster_StatsOnly(string id, int cost, int level, int hp, int atk, int def, int upHp, int upAtk, int upDef)
        {
            var def0 = _ctx.Cards.Get(id);
            Assert.AreEqual(cost, def0.Cost);
            Assert.AreEqual(level, def0.Level);
            Assert.IsNull(CardEffects.For(id), "효과 없음");

            var m = Summon(id, SlotPosition.Back);
            Assert.AreEqual(hp, m.MaxHP.FinalValue);
            Assert.AreEqual(hp, m.CurrentHP);
            Assert.AreEqual(atk, m.Attack.FinalValue);
            Assert.AreEqual(def, m.Defense.FinalValue);
            Assert.AreEqual(level, SacrificeAction.CrystalFor(m), "희생 Crystal = 레벨");
            Assert.AreEqual(0, _ctx.Player.Deck.Hand.Count, "소환 효과 없음");

            var up = Summon(id, SlotPosition.Middle, upgraded: true);
            Assert.AreEqual(upHp, up.MaxHP.FinalValue);
            Assert.AreEqual(upAtk, up.Attack.FinalValue);
            Assert.AreEqual(upDef, up.Defense.FinalValue);
        }

        // ---------- OnAttack (D-70: 커맨드 공격 1회마다, 피해 뒤) ----------

        [Test]
        public void NEC_MON_004_PlagueRat_AttackAppliesPoison1()
        {
            Summon("NEC_MON_004", SlotPosition.Front);
            var e = Enemy(SlotPosition.Front, hp: 20);
            _ctx.EndPlayerTurn();
            // ATK 2 − (DEF 0 + Front 1) = 1 → 19, Poison 1 틱 → 18
            Assert.AreEqual(18, e.CurrentHP);
            Assert.AreEqual(1, e.Statuses.Stacks(StatusType.Poison));
        }

        [Test]
        public void NEC_MON_004_PlagueRat_PoisonEvenIfShieldAbsorbsAll()
        {
            Summon("NEC_MON_004", SlotPosition.Front);
            var e = Enemy(SlotPosition.Front, hp: 20);
            e.Statuses.Apply(StatusType.Shield, 5, _ctx);
            _ctx.EndPlayerTurn();
            // 피해 1 → Shield 5 → 4, 틱 1 → Shield 3 (틱에도 Shield 적용, D-01). HP 그대로
            Assert.AreEqual(20, e.CurrentHP);
            Assert.AreEqual(1, e.Statuses.Stacks(StatusType.Poison));
        }

        [Test]
        public void NEC_MON_014_CursedKnight_AttackAppliesPoison1_Upgraded()
        {
            Summon("NEC_MON_014", SlotPosition.Front, upgraded: true);
            var e = Enemy(SlotPosition.Front, hp: 30);
            _ctx.EndPlayerTurn();
            // 강화 ATK 7 − 1 = 6 → 24, Poison 1 틱 → 23
            Assert.AreEqual(23, e.CurrentHP);
            Assert.AreEqual(1, e.Statuses.Stacks(StatusType.Poison));
        }

        [Test]
        public void OnAttack_TargetKilledByThatAttack_GetsNoPoison()
        {
            Summon("NEC_MON_014", SlotPosition.Front);
            var e = Enemy(SlotPosition.Front, hp: 5);
            Enemy(SlotPosition.Middle, hp: 30);
            _ctx.EndPlayerTurn();
            // ATK 6 − 1 = 5 → 0
            Assert.IsTrue(e.IsDead);
            Assert.AreEqual(0, e.Statuses.Stacks(StatusType.Poison));
        }

        [Test]
        public void NEC_MON_013_CannibalGhoul_AttackHealsSelf2_CappedAtMax()
        {
            var g = Summon("NEC_MON_013", SlotPosition.Front);
            Enemy(SlotPosition.Front, hp: 50);
            g.CurrentHP = 10;
            _ctx.EndPlayerTurn();
            Assert.AreEqual(12, g.CurrentHP, "10 + 2");

            _ctx.StartPlayerTurn();
            g.CurrentHP = 17;
            _ctx.EndPlayerTurn();
            Assert.AreEqual(18, g.CurrentHP, "최대 HP 18을 넘지 않는다");
        }

        [Test]
        public void OnAttack_ExtraActionIsAnotherAttack()
        {
            var g = Summon("NEC_MON_013", SlotPosition.Front);
            var e = Enemy(SlotPosition.Front, hp: 50);
            g.CurrentHP = 10;
            g.ExtraActionsThisTurn = 1;
            _ctx.EndPlayerTurn();
            Assert.AreEqual(14, g.CurrentHP, "공격 2회 × 회복 2");
            Assert.AreEqual(50 - 2 * (5 - 1), e.CurrentHP, "ATK 5 − Front DEF 1 = 4, 두 번");
        }

        [Test]
        public void OnAttack_CardDamageIsNotAnAttack()
        {
            var g = Summon("NEC_MON_013", SlotPosition.Front);
            Enemy(SlotPosition.Front, hp: 50);
            g.CurrentHP = 10;
            _ctx.Player.CurrentCrystal = 2;
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(InHand("NEC_ACT_003"), null)); // 죽음의 소용돌이: 카드 피해
            _ctx.Queue.Process();
            Assert.AreEqual(10, g.CurrentHP);
        }

        // ---------- 부패한 기사 (D-70: 처치 Crystal은 다음 턴 시작에) ----------

        [Test]
        public void NEC_MON_010_RottenKnight_KillGivesCrystalNextTurn()
        {
            Summon("NEC_MON_010", SlotPosition.Front);
            var front = Enemy(SlotPosition.Front, hp: 6);
            var middle = Enemy(SlotPosition.Middle, hp: 30);
            _ctx.EndPlayerTurn();
            // ATK 7 − (0 + 1) = 6 → 처치
            Assert.IsTrue(front.IsDead);

            _ctx.StartPlayerTurn();
            Assert.AreEqual(3 + 1, _ctx.Player.CurrentCrystal, "MaxCrystal 3 리셋 뒤 +1");

            _ctx.EndPlayerTurn();
            // Middle은 Front 보너스 없음: 7 → 23, 처치 아님
            Assert.AreEqual(23, middle.CurrentHP);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(3, _ctx.Player.CurrentCrystal, "한 번만 받는다");
        }

        [Test]
        public void NEC_MON_010_RottenKnight_PoisonTickKillDoesNotCount()
        {
            Summon("NEC_MON_010", SlotPosition.Front);
            var front = Enemy(SlotPosition.Front, hp: 7);
            Enemy(SlotPosition.Middle, hp: 30);
            front.Statuses.Apply(StatusType.Poison, 1, _ctx);
            _ctx.EndPlayerTurn();
            // 공격 6 → 1, 틱 1 → 0: 틱으로 죽음
            Assert.IsTrue(front.IsDead);
            _ctx.StartPlayerTurn();
            Assert.AreEqual(3, _ctx.Player.CurrentCrystal);
        }

        [Test]
        public void NextTurnCrystal_ClearedAtBattleEnd()
        {
            _ctx.Player.NextTurnCrystal = 1;
            _ctx.EndBattleCleanup();
            Assert.AreEqual(0, _ctx.Player.NextTurnCrystal);
        }

        // ---------- 시체 포식자 (소멸 더미의 Monster 카드 1장 → 손패) ----------

        private RuntimeCard Exhausted(string cardId)
        {
            var card = InHand(cardId);
            _ctx.Player.Deck.ExhaustFromHand(card);
            return card;
        }

        [Test]
        public void NEC_MON_009_CorpseEater_ReturnsMonsterFromExhaustPile()
        {
            var skill = Exhausted("NEC_SKL_003");
            var mon = Exhausted("NEC_MON_001");
            Summon("NEC_MON_009", SlotPosition.Back);
            Assert.AreEqual(CardZone.Hand, mon.Zone);
            Assert.AreEqual(CardZone.ExhaustPile, skill.Zone, "Monster 카드만");
        }

        [Test]
        public void NEC_MON_009_CorpseEater_OnlyOneOfSeveral()
        {
            var a = Exhausted("NEC_MON_001");
            var b = Exhausted("NEC_MON_006");
            Summon("NEC_MON_009", SlotPosition.Back);
            Assert.AreEqual(1, new[] { a, b }.Count(c => c.Zone == CardZone.Hand));
            Assert.AreEqual(1, _ctx.Player.Deck.ExhaustPile.Count);
        }

        [Test]
        public void NEC_MON_009_CorpseEater_NoMonsterInExhaust_NoEffect()
        {
            Exhausted("NEC_SKL_003");
            Summon("NEC_MON_009", SlotPosition.Back);
            Assert.AreEqual(0, _ctx.Player.Deck.Hand.Count);
            Assert.AreEqual(1, _ctx.Player.Deck.ExhaustPile.Count);
        }

        [Test]
        public void NEC_MON_009_CorpseEater_FullHand_GoesToDiscard()
        {
            var mon = Exhausted("NEC_MON_001");
            for (int i = 0; i < 10; i++) InHand("NEC_SKL_003");
            // 손패가 10장인 채로 소환 효과가 실행되게, 카드를 내지 않고 몬스터만 소환한다
            var m = MonsterFactory.FromCard(_ctx, _ctx.Cards.Get("NEC_MON_009"), null);
            Assert.AreEqual(SummonResult.Placed, _ctx.Field.TrySummonAlly(m, SlotPosition.Back, _ctx));
            _ctx.Queue.Process();
            Assert.AreEqual(CardZone.DiscardPile, mon.Zone, "손패 10장이면 버린 더미로 (D-30)");
        }

        // ---------- 모방 슬라임 (D-70) ----------

        [Test]
        public void NEU_MON_002_Mimic_CopiesOnlyNeighbourBaseStats()
        {
            Summon("NEC_MON_008", SlotPosition.Front);
            var s = Summon("NEU_MON_002", SlotPosition.Middle);
            Assert.AreEqual(20, s.MaxHP.FinalValue);
            Assert.AreEqual(20, s.CurrentHP, "현재 HP = 새 최대 HP");
            Assert.AreEqual(6, s.Attack.FinalValue);
            Assert.AreEqual(2, s.Defense.FinalValue);
            Assert.AreEqual(1, SacrificeAction.CrystalFor(s), "레벨은 그대로 Lv1");
        }

        [Test]
        public void NEU_MON_002_Mimic_CopiesBaseNotModifiers_UpgradedNeighbour()
        {
            var n = Summon("NEC_MON_008", SlotPosition.Back, upgraded: true);
            n.Attack.AdditiveModifier += 5;
            var s = Summon("NEU_MON_002", SlotPosition.Middle);
            Assert.AreEqual(22, s.MaxHP.FinalValue, "강화 HP 22");
            Assert.AreEqual(6, s.Attack.FinalValue, "보정 +5는 복사하지 않는다");
        }

        [Test]
        public void NEU_MON_002_Mimic_TwoNeighbours_MustChoose()
        {
            Summon("NEC_MON_001", SlotPosition.Front);
            var back = Summon("NEC_MON_008", SlotPosition.Back);
            var card = InHand("NEU_MON_002");
            _ctx.Player.CurrentCrystal = 1;
            Assert.AreEqual(PlayResult.InvalidTarget, _ctx.PlayCard(card, null, SlotPosition.Middle));
            Assert.AreEqual(1, _ctx.Player.CurrentCrystal, "비용 미소모");
            Assert.AreEqual(CardZone.Hand, card.Zone);
            Assert.IsNull(_ctx.Field.At(TeamType.Player, SlotPosition.Middle));

            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(card, back, SlotPosition.Middle));
            _ctx.Queue.Process();
            var s = (RuntimeMonster)_ctx.Field.At(TeamType.Player, SlotPosition.Middle);
            Assert.AreEqual(20, s.MaxHP.FinalValue);
            Assert.AreEqual(6, s.Attack.FinalValue);
        }

        [Test]
        public void NEU_MON_002_Mimic_TargetNotAdjacent_Rejected()
        {
            var front = Summon("NEC_MON_008", SlotPosition.Front);
            Summon("NEC_MON_001", SlotPosition.Middle);
            var card = InHand("NEU_MON_002");
            _ctx.Player.CurrentCrystal = 1;
            Assert.AreEqual(PlayResult.InvalidTarget, _ctx.PlayCard(card, front, SlotPosition.Back), "Back의 옆 칸은 Middle뿐");
        }

        [Test]
        public void NEU_MON_002_Mimic_AutoSlotNeighbour()
        {
            // 칸을 지정하지 않으면 Back → Middle → Front 첫 빈 칸 (`09` §2.1): Back이 비어 Back에 놓이고 옆 칸은 Middle
            Summon("NEC_MON_008", SlotPosition.Middle);
            var card = InHand("NEU_MON_002");
            _ctx.Player.CurrentCrystal = 1;
            Assert.AreEqual(PlayResult.Ok, _ctx.PlayCard(card, null));
            _ctx.Queue.Process();
            Assert.AreEqual(20, ((RuntimeMonster)_ctx.Field.At(TeamType.Player, SlotPosition.Back)).MaxHP.FinalValue);
        }

        [Test]
        public void NEU_MON_002_Mimic_NoNeighbour_KeepsOwnStats()
        {
            Summon("NEC_MON_008", SlotPosition.Back);
            var s = Summon("NEU_MON_002", SlotPosition.Front);
            Assert.AreEqual(10, s.MaxHP.FinalValue);
            Assert.AreEqual(2, s.Attack.FinalValue);
            Assert.AreEqual(1, s.Defense.FinalValue);
        }

        [Test]
        public void NEU_MON_002_Mimic_FusionUsesOtherMaterialClass()
        {
            // NEC 풀과 NEC+NEU 풀에 결과를 하나씩만 두면 어느 풀을 썼는지 바로 보인다
            var f = new FusionCatalog();
            f.AddPool("NEC", 1, "NEC_POOL_RESULT", 1);
            f.AddPool("NEC+NEU", 1, "MIXED_POOL_RESULT", 1);
            f.AddPool("NEU", 1, "NEU_POOL_RESULT", 1);
            f.AddClassMimic("NEU_MON_002");
            var r = new BattleRandom(1);
            Assert.AreEqual("NEC_POOL_RESULT", FusionSystem.Resolve(f, "NEU_MON_002", "NEC_MON_003", 1, r, out _), "슬라임 = NEC로 취급 → NEC + NEC");
            Assert.AreEqual("NEC_POOL_RESULT", FusionSystem.Resolve(f, "NEC_MON_003", "NEU_MON_002", 1, r, out _), "순서 무관");
            Assert.AreEqual("MIXED_POOL_RESULT", FusionSystem.Resolve(f, "NEU_MON_003", "NEC_MON_003", 1, r, out _), "다른 NEU 카드는 그대로");
            Assert.AreEqual("NEU_POOL_RESULT", FusionSystem.Resolve(f, "NEU_MON_002", "NEU_MON_002", 1, r, out _), "둘 다 슬라임이면 NEU");
            Assert.IsTrue(StageOneFusion.Create().IsClassMimic("NEU_MON_002"), "픽스처도 Monster_Effects FusionAsClass를 옮긴다");
        }

        // ---------- 수정 골렘 (Passive 피해 감소 1, OnSacrificed Crystal 3) ----------

        [Test]
        public void NEU_MON_007_CrystalGolem_SacrificeGives3()
        {
            var g = Summon("NEU_MON_007", SlotPosition.Back);
            _ctx.Player.CurrentCrystal = 0;
            Assert.AreEqual(SacrificeResult.Ok, _ctx.Sacrifice(g));
            _ctx.Queue.Process();
            Assert.AreEqual(3, _ctx.Player.CurrentCrystal, "Lv2 기본 +2 대신 +3");
        }

        private int Hit(RuntimeUnit target, int atk, bool tick = false)
        {
            int before = target.CurrentHP;
            var src = new RuntimeEnemy("EX" + (++_id), "ENE_TEST", 30, atk, 0, false);
            _ctx.Queue.EnqueueMain(new DamageAction { Source = src, Target = target, BaseDamage = atk, IsStatusTick = tick });
            _ctx.Queue.Process();
            return before - target.CurrentHP;
        }

        [Test]
        public void NEU_MON_007_CrystalGolem_FrontReducesAllyDamageBy1()
        {
            var g = Summon("NEU_MON_007", SlotPosition.Front);
            var s = Summon("NEC_MON_001", SlotPosition.Middle);
            // 뼈 병사 Middle DEF 1: 8 − 1 = 7, 감소 1 → 6 (D-70 예)
            Assert.AreEqual(6, Hit(s, 8));
            // 골렘 자신 Front DEF 4 + 1 = 5: 8 − 5 = 3, 감소 1 → 2
            Assert.AreEqual(2, Hit(g, 8));
            // 최소 1은 감소 뒤에: 2 − 5 → 1
            Assert.AreEqual(1, Hit(g, 2));
            // Vulnerable: floor(7 × 1.25) = 8, 감소 1 → 7 (D-70 예)
            s.CurrentHP = 20;
            s.Statuses.Apply(StatusType.Vulnerable, 1, _ctx);
            Assert.AreEqual(7, Hit(s, 8));
        }

        [Test]
        public void NEU_MON_007_CrystalGolem_NotFront_NoReduction()
        {
            Summon("NEU_MON_007", SlotPosition.Middle);
            var s = Summon("NEC_MON_001", SlotPosition.Front);
            // 골렘이 Front가 아니면 감소 없음: 뼈 병사 Front DEF 1 + 1 = 2 → 8 − 2 = 6
            Assert.AreEqual(6, Hit(s, 8));
        }

        [Test]
        public void NEU_MON_007_CrystalGolem_NoTick_NoPlayer()
        {
            Summon("NEU_MON_007", SlotPosition.Front);
            var s = Summon("NEC_MON_001", SlotPosition.Middle);
            Assert.AreEqual(3, Hit(s, 3, tick: true), "틱에는 적용하지 않는다");
            Assert.AreEqual(8, Hit(_ctx.Player, 8), "플레이어에는 적용하지 않는다 (DEF 0)");
        }

        [Test]
        public void DamageCalculator_FlatReductionAfterVulnerableBeforeMinimum()
        {
            Assert.AreEqual(7, DamageCalculator.AttackDamage(new DamageRequest { BaseDamage = 8, TargetDef = 1, TargetVulnerable = true, FlatReduction = 1 }));
            Assert.AreEqual(1, DamageCalculator.AttackDamage(new DamageRequest { BaseDamage = 2, TargetDef = 5, FlatReduction = 1 }));
            Assert.AreEqual(6, DamageCalculator.AttackDamage(new DamageRequest { BaseDamage = 8, TargetDef = 1, FlatReduction = 1 }));
        }
    }
}
