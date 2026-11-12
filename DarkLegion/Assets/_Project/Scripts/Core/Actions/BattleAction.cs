namespace DarkLegion.Core
{
    public enum BattleActionType
    {
        Damage, Heal,
        ApplyStatus, RemoveStatus,
        Draw, Discard, Exhaust, MoveCard,
        GainCrystal,
        Summon, Sacrifice,
        Death, Destroy,
        Trigger
    }

    /// <summary>
    /// 모든 전투 상태 변화의 단위 (`06` §8). 직접 실행하지 않고 <see cref="QueueProcessor"/>에 넣는다.
    /// </summary>
    public abstract class BattleAction
    {
        public abstract BattleActionType Type { get; }

        /// <summary>트리거가 만든 액션의 중첩 깊이. TriggerBus가 Enqueue할 때 검사한다 (MaxTriggerDepth).</summary>
        public int TriggerDepth { get; set; }

        /// <summary>실행 전 취소되면 Execute를 부르지 않는다 (Before 트리거의 무효화 등).</summary>
        public bool IsCancelled { get; private set; }

        public void Cancel()
        {
            IsCancelled = true;
        }

        public abstract void Execute(BattleContext context);

        public override string ToString()
        {
            return Type.ToString();
        }
    }
}
