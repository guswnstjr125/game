using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>
    /// 전투 이벤트 알림 (`07`, `06` §10의 최소 구현). 구독자는 상태를 바로 바꾸지 않고 액션을 Trigger 큐에 넣는다.
    /// 지금은 카드·몬스터 효과에 필요한 사망·소환만 있다. 유물·OnAttack 등은 해당 이슈에서 늘린다.
    /// </summary>
    public sealed class TriggerBus
    {
        /// <summary>유닛이 죽을 때 (칸에서 빠지기 직전, `09` §5). isSacrifice = 희생으로 죽음.</summary>
        public event System.Action<BattleContext, RuntimeUnit, bool> UnitDied;

        /// <summary>아군 몬스터가 칸에 놓인 뒤.</summary>
        public event System.Action<BattleContext, RuntimeMonster> AllySummoned;

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
