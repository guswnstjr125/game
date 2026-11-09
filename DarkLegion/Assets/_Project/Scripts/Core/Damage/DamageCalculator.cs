namespace DarkLegion.Core
{
    /// <summary>피해 계산 입력 (`01` D-01, `06` §11).</summary>
    public struct DamageRequest
    {
        /// <summary>유닛 공격 = 공격자 FinalAtk, 카드 피해 = 카드 Value.</summary>
        public int BaseDamage;
        public bool IsCritical;
        /// <summary>공격자(유닛)가 Weak. 플레이어 카드 피해에는 false로 넘긴다.</summary>
        public bool AttackerWeak;
        /// <summary>대상 FinalDef (Front 보너스 +1 포함). 플레이어는 0.</summary>
        public int TargetDef;
        public bool TargetVulnerable;
        public bool IgnoreDefense;
        /// <summary>부분 관통률 0~1 (예: 방어 무시 50% = 0.5). DEF × (1 − 관통률)을 내림 후 차감.</summary>
        public float PenetrationRate;
    }

    /// <summary>
    /// D-01: 공격 피해 = max(1, floor((기본피해 × 치명 × Weak − DEF) × Vulnerable)).
    /// Shield 적용은 <see cref="ApplyToShieldAndHp"/>에서 따로 한다.
    /// </summary>
    public static class DamageCalculator
    {
        public const double CriticalMultiplier = 1.5;
        public const double WeakMultiplier = 0.75;
        public const double VulnerableMultiplier = 1.25;
        /// <summary>Front 위치 DEF 보너스 (`04` Position_Bonus). 아군·적 모두.</summary>
        public const int FrontDefBonus = 1;

        public static int AttackDamage(DamageRequest r)
        {
            double damage = r.BaseDamage;
            if (r.IsCritical) damage *= CriticalMultiplier;
            if (r.AttackerWeak) damage *= WeakMultiplier;

            int def = 0;
            if (!r.IgnoreDefense)
            {
                def = r.TargetDef;
                if (r.PenetrationRate > 0)
                {
                    def = (int)System.Math.Floor(def * (1.0 - r.PenetrationRate));
                }
            }
            damage -= def;
            if (r.TargetVulnerable) damage *= VulnerableMultiplier;

            int result = (int)System.Math.Floor(damage);
            return result < 1 ? 1 : result;
        }

        /// <summary>Burn·Poison 틱: 스택 값 그대로. DEF·Weak·Vulnerable 미적용 (D-01).</summary>
        public static int TickDamage(int stacks)
        {
            return stacks < 0 ? 0 : stacks;
        }

        /// <summary>
        /// Shield 먼저, 남은 피해만 HP (`06` §11.2). ignoreShield면 HP에 바로.
        /// 반환: (새 Shield, 새 HP).
        /// </summary>
        public static (int shield, int hp) ApplyToShieldAndHp(int damage, int shield, int hp, bool ignoreShield = false)
        {
            if (damage <= 0) return (shield, hp);
            if (ignoreShield) return (shield, hp - damage);
            int absorbed = damage < shield ? damage : shield;
            return (shield - absorbed, hp - (damage - absorbed));
        }
    }
}
