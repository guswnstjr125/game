using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>상태이상 8종 (`12` Status_Master).</summary>
    public enum StatusType
    {
        Burn,
        Poison,
        Weak,
        Vulnerable,
        Shield,
        Taunt,
        Bind,
        Stun
    }

    /// <summary>
    /// 유닛 하나의 상태이상 (`11` §1). 값의 뜻은 종류마다 다르다:
    /// Burn·Poison = 스택(최대 20, D-08), Weak·Vulnerable·Bind·Stun = 남은 턴, Taunt = 있음(1), Shield = 유닛의 Shield 값.
    /// </summary>
    public sealed class StatusContainer
    {
        public const int MaxDotStacks = 20;
        public const int MaxShield = 99;

        private readonly RuntimeUnit _owner;
        private readonly Dictionary<StatusType, int> _values = new Dictionary<StatusType, int>();
        // Enemy Phase·턴 종료 처리 중에 걸린 지속형 상태는 그 턴 종료에 줄이지 않는다.
        // 안 그러면 적이 건 Stun(1턴)이 같은 턴 종료에 사라져 효과가 없다 (`01` D-57).
        private readonly HashSet<StatusType> _skipNextExpiry = new HashSet<StatusType>();

        public StatusContainer(RuntimeUnit owner)
        {
            _owner = owner;
        }

        public int Stacks(StatusType type)
        {
            if (type == StatusType.Shield) return _owner.Shield;
            return _values.TryGetValue(type, out int v) ? v : 0;
        }

        public bool Has(StatusType type) => Stacks(type) > 0;

        /// <summary>Bind·Stun이면 공격·스킬을 쓸 수 없다. 희생은 허용된다 (`11` §2).</summary>
        public bool CanAct => !Has(StatusType.Bind) && !Has(StatusType.Stun);

        /// <summary>
        /// 상태 부여. 스택형은 더하고(상한에서 자름), 지속형은 남은 턴과 새 턴 중 큰 값으로 갱신한다.
        /// 죽은 유닛에는 걸리지 않는다. 걸렸으면 true.
        /// </summary>
        public bool Apply(StatusType type, int amount, BattleContext context)
        {
            if (amount <= 0 || _owner.IsDead) return false;
            switch (type)
            {
                case StatusType.Shield:
                    _owner.Shield = System.Math.Min(MaxShield, _owner.Shield + amount);
                    return true;
                case StatusType.Burn:
                case StatusType.Poison:
                    _values[type] = System.Math.Min(MaxDotStacks, Stacks(type) + amount);
                    return true;
                case StatusType.Taunt:
                    _values[type] = 1;
                    return true;
                default:
                    _values[type] = System.Math.Max(Stacks(type), amount);
                    if (context != null && (context.State == BattleState.EnemyPhase || context.State == BattleState.TurnEnd))
                    {
                        _skipNextExpiry.Add(type);
                    }
                    return true;
            }
        }

        public void Remove(StatusType type)
        {
            if (type == StatusType.Shield) _owner.Shield = 0;
            _values.Remove(type);
            _skipNextExpiry.Remove(type);
        }

        /// <summary>턴 종료: Burn −1, Shield 전량 소멸, 지속형 −1 (`11` §1 Tick 순서). 틱 피해는 BattleContext.EndTurn이 먼저 넣는다.</summary>
        internal void ExpireAtTurnEnd()
        {
            Decrement(StatusType.Burn);
            _owner.Shield = 0;
            foreach (var type in Durations)
            {
                if (_skipNextExpiry.Remove(type)) continue;
                Decrement(type);
            }
        }

        /// <summary>전투 종료: Poison을 포함해 전부 지운다 (D-08).</summary>
        internal void ClearAll()
        {
            _values.Clear();
            _skipNextExpiry.Clear();
            _owner.Shield = 0;
        }

        private static readonly StatusType[] Durations = { StatusType.Weak, StatusType.Vulnerable, StatusType.Bind, StatusType.Stun };

        private void Decrement(StatusType type)
        {
            int v = Stacks(type);
            if (v <= 0) return;
            if (v == 1) _values.Remove(type);
            else _values[type] = v - 1;
        }
    }
}
