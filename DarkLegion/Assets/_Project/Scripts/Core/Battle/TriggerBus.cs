using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>
    /// 전투 이벤트 알림 (`07`, `06` §10의 최소 구현). 구독자는 상태를 바로 바꾸지 않고 액션을 Trigger 큐에 넣는다.
    /// 지금은 카드·몬스터 효과에 필요한 사망·소환·아군 공격만 있다. 유물 등은 해당 이슈에서 늘린다.
    /// </summary>
    public sealed class TriggerBus
    {
        /// <summary>유닛이 죽을 때 (칸에서 빠지기 직전, `09` §5). isSacrifice = 희생으로 죽음.</summary>
        public event System.Action<BattleContext, RuntimeUnit, bool> UnitDied;

        /// <summary>아군 몬스터가 칸에 놓인 뒤.</summary>
        public event System.Action<BattleContext, RuntimeMonster> AllySummoned;

        /// <summary>
        /// 아군 몬스터의 커맨드 공격 1회가 처리된 뒤 (D-70). killed = 이 공격 피해로 대상이 죽음.
        /// 구독자는 Trigger 큐에 넣으므로 공격 피해 다음에 실행된다.
        /// </summary>
        public event System.Action<BattleContext, RuntimeMonster, RuntimeUnit, bool> AllyAttacked;

        internal void RaiseAllyAttacked(BattleContext ctx, RuntimeMonster attacker, RuntimeUnit target, bool killed) => AllyAttacked?.Invoke(ctx, attacker, target, killed);

        /// <summary>적의 공격 피해 1건이 아군 몬스터·플레이어에게 처리된 뒤 (틱 제외, D-76 "피격"). Shield가 다 막아도 알린다.</summary>
        public event System.Action<BattleContext, RuntimeEnemy, RuntimeUnit> EnemyAttackHit;

        internal void RaiseEnemyAttackHit(BattleContext ctx, RuntimeEnemy attacker, RuntimeUnit target) => EnemyAttackHit?.Invoke(ctx, attacker, target);

        internal void RaiseUnitDied(BattleContext ctx, RuntimeUnit unit, bool isSacrifice) => UnitDied?.Invoke(ctx, unit, isSacrifice);

        internal void RaiseAllySummoned(BattleContext ctx, RuntimeMonster m) => AllySummoned?.Invoke(ctx, m);
    }

    /// <summary>
    /// 효과 한 덩어리를 큐에서 실행하는 액션. 카드·몬스터 효과가 "이 순서로 이 일을 한다"를 큐에 넣을 때 쓴다.
    /// 실행 내용은 BattleContext의 공개 기능(피해·회복·상태·드로우)만 쓴다.
    /// </summary>
    public sealed class EffectAction : BattleAction
    {
        private readonly BattleActionType _type;
        private readonly System.Action<BattleContext> _run;
        private readonly string _label;

        public EffectAction(BattleActionType type, string label, System.Action<BattleContext> run)
        {
            _type = type;
            _label = label;
            _run = run;
        }

        public override BattleActionType Type => _type;

        public override void Execute(BattleContext context) => _run(context);

        public override string ToString() => _label;
    }
}
