namespace DarkLegion.Core
{
    /// <summary>
    /// 피해 액션 (`06` §11). D-01 계산 → Shield → HP → HP ≤ 0이면 IsDead + Death Queue.
    /// 트리거(Before/OnDamageTaken/After)는 TriggerBus(#36 후속)에서 붙인다.
    /// </summary>
    public sealed class DamageAction : BattleAction
    {
        public RuntimeUnit Source;
        public RuntimeUnit Target;
        public int BaseDamage;
        public bool IsCritical;
        public bool IgnoreDefense;
        public bool IgnoreShield;
        /// <summary>Burn·Poison 틱이면 true: 스택 값 그대로, DEF·Weak·Vulnerable 무시 (D-01).</summary>
        public bool IsStatusTick;
        /// <summary>Weak·Vulnerable은 상태이상(#40) 구현 전까지 호출하는 쪽이 넘긴다.</summary>
        public bool AttackerWeak;
        public bool TargetVulnerable;

        public int LastDealt { get; private set; }

        public override BattleActionType Type => BattleActionType.Damage;

        public override void Execute(BattleContext context)
        {
            if (Target == null || !Target.IsTargetable || Target.Flags.Invincible) return;
            int damage = IsStatusTick
                ? DamageCalculator.TickDamage(BaseDamage)
                : DamageCalculator.AttackDamage(new DamageRequest
                {
                    BaseDamage = BaseDamage,
                    IsCritical = IsCritical,
                    AttackerWeak = AttackerWeak,
                    TargetDef = Target.EffectiveDefense,
                    TargetVulnerable = TargetVulnerable,
                    IgnoreDefense = IgnoreDefense
                });
            var (shield, hp) = DamageCalculator.ApplyToShieldAndHp(damage, Target.Shield, Target.CurrentHP, IgnoreShield);
            LastDealt = Target.CurrentHP - hp;
            Target.Shield = shield;
            Target.CurrentHP = hp;
            if (Target.CurrentHP <= 0 && Target.MarkDead())
            {
                context.Queue.EnqueueDeath(new DeathAction { Unit = Target });
            }
        }

        public override string ToString() => $"Damage {BaseDamage} → {Target?.UnitId}";
    }

    /// <summary>사망 처리 (`06` §12). 전장에서 빼고 묘지에 넣는다. 몬스터 카드는 버린 더미로.</summary>
    public sealed class DeathAction : BattleAction
    {
        public RuntimeUnit Unit;
        public bool IsSacrifice;

        public override BattleActionType Type => BattleActionType.Death;

        public override void Execute(BattleContext context)
        {
            context.RemoveUnit(Unit);
            if (Unit is RuntimeMonster m && context.Player != null)
            {
                context.Player.Deck.ReturnFromField(m.SourceCard);
            }
        }

        public override string ToString() => $"Death {Unit?.UnitId}";
    }
}
