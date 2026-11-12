using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>적 행동표의 한 줄 (`21` §2~§6). 조건이나 확률이 있으면 "조건 행동"이다.</summary>
    public sealed class EnemyActionOption
    {
        public string Id { get; }
        public System.Func<RuntimeEnemy, BattleContext, bool> Condition { get; }
        /// <summary>조건이 참일 때 고를 확률 (기본 1).</summary>
        public double Chance { get; }

        public bool IsConditional => Condition != null || Chance < 1.0;

        public EnemyActionOption(string id, System.Func<RuntimeEnemy, BattleContext, bool> condition = null, double chance = 1.0)
        {
            Id = id;
            Condition = condition;
            Chance = chance;
        }
    }

    /// <summary>
    /// 적 한 마리의 행동 선택 (`01` D-55, `21` §1.1). 플레이어 턴 시작에 한 번 부른다 (D-28).
    /// 1) 위에서부터 조건 행동: 조건이 참이고 직전 턴에 쓴 행동이 아니면 고른다 (확률은 BattleRandom).
    /// 2) 없으면 조건 없는 행동을 표 순서대로 돌아가며 쓴다.
    /// 3) Bind·Stun이면 null(NoAction)이고 순환 위치·직전 행동은 그대로다.
    /// </summary>
    public sealed class EnemyBrain
    {
        private readonly List<EnemyActionOption> _options;
        private readonly List<EnemyActionOption> _rotation = new List<EnemyActionOption>();
        private int _rotationIndex;
        private EnemyActionOption _last;

        public EnemyBrain(IEnumerable<EnemyActionOption> options)
        {
            _options = new List<EnemyActionOption>(options);
            foreach (var o in _options)
            {
                if (!o.IsConditional) _rotation.Add(o);
            }
        }

        public EnemyActionOption Decide(RuntimeEnemy self, BattleContext context)
        {
            if (!self.Statuses.CanAct) return null;

            foreach (var o in _options)
            {
                if (!o.IsConditional || o == _last) continue;
                if (o.Condition != null && !o.Condition(self, context)) continue;
                if (o.Chance < 1.0 && !context.Random.Chance(o.Chance)) continue;
                _last = o;
                return o;
            }

            if (_rotation.Count == 0) return null;
            var pick = _rotation[_rotationIndex % _rotation.Count];
            _rotationIndex++;
            _last = pick;
            return pick;
        }
    }
}
