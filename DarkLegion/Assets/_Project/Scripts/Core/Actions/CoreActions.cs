namespace DarkLegion.Core
{
    /// <summary>
    /// 피해 액션 (`06` §11). D-01 계산 → Shield → HP → HP ≤ 0이면 IsDead + Death Queue.
    /// 피해 감소(수정 골렘)는 틱이 아닐 때 D-01 계산 안에서 뺀다 (D-70). 아군 커맨드 공격이면 처리 뒤 AllyAttacked를 알린다.
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
        /// <summary>행동 배율 (적 강타 ×1.5, 독물기 ×0.5). 0이면 1.</summary>
        public double Multiplier;
        /// <summary>아군 몬스터의 커맨드 공격(D-27)이면 true. 처리 뒤 공격 시 효과(OnAttack)를 알린다 (D-70).</summary>
        public bool IsAttack;

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
                    Multiplier = Multiplier,
                    IsCritical = IsCritical,
                    // Weak는 유닛 공격에만 (D-01: 플레이어 카드 피해에는 적용 안 함)
                    AttackerWeak = Source != null && !(Source is RuntimePlayer) && Source.Statuses.Has(StatusType.Weak),
                    TargetDef = Target.EffectiveDefense,
                    TargetVulnerable = Target.Statuses.Has(StatusType.Vulnerable),
                    IgnoreDefense = IgnoreDefense,
                    FlatReduction = MonsterEffects.DamageReductionFor(context, Target)
                });
            var (shield, hp) = DamageCalculator.ApplyToShieldAndHp(damage, Target.Shield, Target.CurrentHP, IgnoreShield);
            LastDealt = Target.CurrentHP - hp;
            Target.Shield = shield;
            Target.CurrentHP = hp;
            bool killed = Target.CurrentHP <= 0 && Target.MarkDead();
            if (killed)
            {
                context.Queue.EnqueueDeath(new DeathAction { Unit = Target });
            }
            if (IsAttack && Source is RuntimeMonster attacker) context.Triggers.RaiseAllyAttacked(context, attacker, Target, killed);
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
            context.Triggers.RaiseUnitDied(context, Unit, IsSacrifice); // 칸에서 빠지기 전 (`09` §5)
            context.RemoveUnit(Unit);
            if (Unit is RuntimeMonster m && context.Player != null)
            {
                context.Player.Deck.ReturnFromField(m.SourceCard);
            }
        }

        public override string ToString() => $"Death {Unit?.UnitId}";
    }
}
