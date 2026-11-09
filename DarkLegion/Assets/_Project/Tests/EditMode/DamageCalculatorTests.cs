using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>D-01 피해 공식. 기대값은 손으로 계산해 적는다 (`32` §11.2).</summary>
    [Category("Stage1")]
    public class DamageCalculatorTests
    {
        private static DamageRequest Hit(int atk, int def, bool weak = false, bool vuln = false, bool crit = false)
        {
            return new DamageRequest { BaseDamage = atk, TargetDef = def, AttackerWeak = weak, TargetVulnerable = vuln, IsCritical = crit };
        }

        [Test]
        public void DMG_01_Atk8_To_FrontBoneSoldier_Is6()
        {
            // 뼈 병사 DEF1 + Front 1 = 2 → 8 − 2 = 6
            Assert.AreEqual(6, DamageCalculator.AttackDamage(Hit(8, 1 + DamageCalculator.FrontDefBonus)));
        }

        [Test]
        public void DMG_01_Atk8_To_FrontPlagueRat_Is3()
        {
            // 역병 쥐 DEF4 + Front 1 = 5 → 8 − 5 = 3
            Assert.AreEqual(3, DamageCalculator.AttackDamage(Hit(8, 4 + DamageCalculator.FrontDefBonus)));
        }

        [Test]
        public void DMG_01_WeakSkeleton_To_FrontVulnerableGhoul_Is2()
        {
            // floor((5 × 0.75 − 2) × 1.25) = floor(2.1875) = 2  (D-01 검증 예시 3)
            Assert.AreEqual(2, DamageCalculator.AttackDamage(Hit(5, 2, weak: true, vuln: true)));
        }

        [Test]
        public void DMG_01_Ghoul_To_FrontBoneSoldier_Is3()
        {
            // D-01 검증 예시 1: 5 − 2 = 3
            Assert.AreEqual(3, DamageCalculator.AttackDamage(Hit(5, 2)));
        }

        [Test]
        public void DMG_01_MinimumDamageIs1()
        {
            // D-01 검증 예시 2: max(1, 3 − 2) = 1, DEF가 더 커도 1
            Assert.AreEqual(1, DamageCalculator.AttackDamage(Hit(3, 2)));
            Assert.AreEqual(1, DamageCalculator.AttackDamage(Hit(3, 10)));
        }

        [Test]
        public void DMG_01_CriticalBeforeDef()
        {
            // floor(6 × 1.5 − 2) = 7
            Assert.AreEqual(7, DamageCalculator.AttackDamage(Hit(6, 2, crit: true)));
        }

        [Test]
        public void DMG_01_IgnoreDefenseAndPenetration()
        {
            var r = Hit(6, 4);
            r.IgnoreDefense = true;
            Assert.AreEqual(6, DamageCalculator.AttackDamage(r));

            // 방어 무시 50%: DEF 5 → floor(2.5) = 2 → 6 − 2 = 4
            var p = Hit(6, 5);
            p.PenetrationRate = 0.5f;
            Assert.AreEqual(4, DamageCalculator.AttackDamage(p));
        }

        [Test]
        public void DMG_02_TickIgnoresModifiersButShieldApplies()
        {
            Assert.AreEqual(3, DamageCalculator.TickDamage(3));
            // Shield 2, HP 10에 틱 3 → Shield 0, HP 9
            var (shield, hp) = DamageCalculator.ApplyToShieldAndHp(DamageCalculator.TickDamage(3), 2, 10);
            Assert.AreEqual(0, shield);
            Assert.AreEqual(9, hp);
        }

        [Test]
        public void ShieldAbsorbsFirst()
        {
            var (shield, hp) = DamageCalculator.ApplyToShieldAndHp(4, 6, 10);
            Assert.AreEqual(2, shield);
            Assert.AreEqual(10, hp);

            (shield, hp) = DamageCalculator.ApplyToShieldAndHp(4, 6, 10, ignoreShield: true);
            Assert.AreEqual(6, shield);
            Assert.AreEqual(6, hp);
        }
    }
}
