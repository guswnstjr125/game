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
    /// Burn·Poison = 스택(최대 20, D-08), Weak·Vulnerable·Bind·Stun = 남은 턴, Taunt = 있음(1), Shield = 일반분과 Enemy Phase분의 합 (D-78).
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
        // 보이는 Shield는 둘의 합이고 상한 99도 합에 적용한다 (`01` D-78).
        private int _normalShield;
        private int _enemyPhaseShield;

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
                    AddShield(amount, context != null && context.State == BattleState.EnemyPhase);
                    return true;
                case StatusType.Burn:
                case StatusType.Poison:
                    _values[type] = System.Math.Min(MaxDotStacks, Stacks(type) + amount);
                    return true;
                case StatusType.Taunt:
                    _values[type] = 1;
                    return true;
                default:
                    if (amount < Stacks(type)) return true; // 더 짧은 재부여는 아무것도 바꾸지 않는다
                    _values[type] = amount;
                    if (context != null && (context.State == BattleState.EnemyPhase || context.State == BattleState.TurnEnd))
                    {
                        _skipNextExpiry.Add(type);
                    }
                    return true;
            }
        }

        public void Remove(StatusType type)
        {
            if (type == StatusType.Shield)
            {
                _normalShield = 0;
                _enemyPhaseShield = 0;
                WriteShield();
            }
            _values.Remove(type);
            _skipNextExpiry.Remove(type);
        }

        /// <summary>턴 종료: Burn −1, 일반 Shield만 제거, 지속형 −1 (`11` §1, D-78). 틱 피해는 BattleContext.EndTurn이 먼저 넣는다.</summary>
        internal void ExpireAtTurnEnd()
        {
            Decrement(StatusType.Burn);
            _normalShield = 0;
            WriteShield();
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
            _normalShield = 0;
            _enemyPhaseShield = 0;
            _owner.SetShieldAmount(0);
        }

        /// <summary>다음 턴이 시작되면 지난 Enemy Phase분은 그 턴의 일반분이 된다 (D-78).</summary>
        internal void PromoteEnemyPhaseShield()
        {
            _normalShield += _enemyPhaseShield;
            _enemyPhaseShield = 0;
            WriteShield();
        }

        /// <summary>
        /// 피해를 일반분부터 흡수하고, 일반분이 0이면 Enemy Phase분을 깎는다 (D-78). 흡수한 양을 반환한다.
        /// </summary>
        public int Absorb(int damage)
        {
            if (damage <= 0) return 0;
            int fromNormal = damage < _normalShield ? damage : _normalShield;
            _normalShield -= fromNormal;
            int rest = damage - fromNormal;
            int fromEnemyPhase = rest < _enemyPhaseShield ? rest : _enemyPhaseShield;
            _enemyPhaseShield -= fromEnemyPhase;
            WriteShield();
            return fromNormal + fromEnemyPhase;
        }

        private void AddShield(int amount, bool enemyPhase)
        {
            int room = MaxShield - (_normalShield + _enemyPhaseShield);
            if (room < 0) room = 0;
            int gain = amount < room ? amount : room;
            if (enemyPhase) _enemyPhaseShield += gain;
            else _normalShield += gain;
            WriteShield();
        }

        /// <summary>보이는 양을 일반분으로 바꾼다. 테스트가 Shield 속성에 직접 넣는 경우다.</summary>
        internal void AdoptVisibleShield(int value)
        {
            _normalShield = value;
            _enemyPhaseShield = 0;
            _owner.SetShieldAmount(value);
        }

        private void WriteShield()
        {
            _owner.SetShieldAmount(_normalShield + _enemyPhaseShield);
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
