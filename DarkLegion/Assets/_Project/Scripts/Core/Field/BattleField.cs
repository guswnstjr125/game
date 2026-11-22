namespace DarkLegion.Core
{
    /// <summary>슬롯 위치 (`09` §1). 아군은 Front·Middle·Back, 적은 Extra1·Extra2까지.</summary>
    public enum SlotPosition
    {
        Front = 0,
        Middle = 1,
        Back = 2,
        Extra1 = 3,
        Extra2 = 4
    }

    /// <summary>아군 소환 요청의 결과 (`09` §3).</summary>
    public enum SummonResult
    {
        /// <summary>칸을 잡았고 SummonAction을 큐에 넣었다.</summary>
        Placed,
        /// <summary>몬스터 카드인데 3칸이 다 찼거나, 토큰인데 Player Phase가 아니다. 거부, 비용을 쓰지 않는다.</summary>
        RejectedFullField,
        /// <summary>토큰인데 3칸이 다 찼다. Front 아군을 고를 때까지 대기 (BattleState.WaitingReplace).</summary>
        WaitingReplace,
        /// <summary>지정한 칸이 차 있다.</summary>
        RejectedOccupied,
        /// <summary>아군에게 없는 칸(Extra1·Extra2)을 지정했다.</summary>
        InvalidPosition
    }

    /// <summary>
    /// 아군 3 / 적 5 슬롯 (`09`). 유닛 배치는 SummonAction·DeathAction·RemoveTokenAction을 거쳐서만 바뀐다 (`06` §2).
    /// 소환 요청은 칸을 예약하고 큐에 넣으므로, 큐를 처리하기 전에 두 번 소환해도 같은 칸을 고르지 않는다.
    /// Front 위치의 유닛은 DEF +1 (`04` Position_Bonus, D-01). 사망 후 자동 당김은 없다.
    /// </summary>
    public sealed class BattleField
    {
        public const int AllySlotCount = 3;
        public const int EnemySlotCount = 5;
        public const int FrontDefBonus = 1;

        private readonly RuntimeUnit[] _allies = new RuntimeUnit[AllySlotCount];
        private readonly RuntimeUnit[] _enemies = new RuntimeUnit[EnemySlotCount];
        private readonly bool[] _allyReserved = new bool[AllySlotCount];
        private RuntimeMonster _pendingToken;

        /// <summary>그 칸의 유닛. 비었으면 null.</summary>
        public RuntimeUnit At(TeamType team, SlotPosition pos)
        {
            var slots = Slots(team);
            int i = (int)pos;
            return i < slots.Length ? slots[i] : null;
        }

        /// <summary>유닛이 있는 칸. 필드에 없으면 null.</summary>
        public SlotPosition? PositionOf(RuntimeUnit unit)
        {
            if (unit == null) return null;
            var slots = Slots(unit.Team);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == unit) return (SlotPosition)i;
            }
            return null;
        }

        public bool HasFreeEnemySlot => System.Array.IndexOf(_enemies, null) >= 0;

        /// <summary>앞줄 = 살아 있는 유닛이 있는 첫 칸 Front → Middle → Back (`09` §7).</summary>
        public RuntimeUnit FrontLine(TeamType team)
        {
            var slots = Slots(team);
            for (int i = 0; i < AllySlotCount; i++)
            {
                if (slots[i] != null && !slots[i].IsDead) return slots[i];
            }
            return null;
        }

        /// <summary>칸을 지정하지 않은 소환이 놓일 칸: Back → Middle → Front 첫 빈 칸 (`09` §2.1). 다 찼으면 null.</summary>
        public SlotPosition? AutoAllySlot()
        {
            for (int i = AllySlotCount - 1; i >= 0; i--)
            {
                if (IsAllyFree(i)) return (SlotPosition)i;
            }
            return null;
        }

        /// <summary>그 칸의 옆 칸(슬롯 번호 ±1)에 있는 살아 있는 아군 (D-70 모방 슬라임).</summary>
        public System.Collections.Generic.List<RuntimeMonster> AdjacentAllies(SlotPosition pos)
        {
            var list = new System.Collections.Generic.List<RuntimeMonster>();
            foreach (int i in new[] { (int)pos - 1, (int)pos + 1 })
            {
                if (i >= 0 && i < AllySlotCount && _allies[i] is RuntimeMonster m && !m.IsDead) list.Add(m);
            }
            return list;
        }

        /// <summary>
        /// 아군 소환 요청. 칸을 고르고(지정이 없으면 Back → Middle → Front 첫 빈 칸, `09` §2.1) 예약한 뒤 SummonAction을 Main 큐에 넣는다.
        /// 3칸이 다 차면 몬스터 카드는 거부, 토큰은 Player Phase에서만 교체 대기로 들어간다 (`09` §3·§3.2).
        /// </summary>
        public SummonResult TrySummonAlly(RuntimeMonster monster, SlotPosition? position, BattleContext context)
        {
            int index;
            if (position.HasValue)
            {
                index = (int)position.Value;
                if (index >= AllySlotCount) return SummonResult.InvalidPosition;
                if (!IsAllyFree(index)) return SummonResult.RejectedOccupied;
            }
            else
            {
                var auto = AutoAllySlot();
                index = auto.HasValue ? (int)auto.Value : -1;
                if (index < 0)
                {
                    if (!monster.IsToken || context.State != BattleState.PlayerPhase) return SummonResult.RejectedFullField;
                    _pendingToken = monster;
                    context.State = BattleState.WaitingReplace;
                    return SummonResult.WaitingReplace;
                }
            }
            _allyReserved[index] = true;
            context.Queue.EnqueueMain(new SummonAction { Unit = monster, Position = (SlotPosition)index });
            return SummonResult.Placed;
        }

        /// <summary>
        /// 토큰 교체 대상 선택 (`09` §3). Front 칸의 살아 있는 아군만 고를 수 있다. 잘못 고르면 false이고 대기 상태를 유지한다.
        /// 고른 아군은 사망 처리(희생 아님, Crystal 없음)되고, 그 다음에 SummonAction이 같은 칸에 토큰을 놓는다.
        /// </summary>
        public bool ResolveReplace(RuntimeUnit victim, BattleContext context)
        {
            if (_pendingToken == null || context.State != BattleState.WaitingReplace) return false;
            if (victim == null || victim != _allies[(int)SlotPosition.Front] || victim.IsDead) return false;
            if (victim is RuntimeMonster vm && vm.SacrificeReserved) return false;
            var token = _pendingToken;
            _pendingToken = null;
            victim.MarkDead();
            _allyReserved[(int)SlotPosition.Front] = true;
            context.Queue.EnqueueDeath(new DeathAction { Unit = victim });
            context.Queue.EnqueueMain(new SummonAction { Unit = token, Position = SlotPosition.Front });
            context.State = BattleState.PlayerPhase;
            return true;
        }

        /// <summary>
        /// 적 배치 (`09` §2.1, 지정이 없으면 Front부터). Wave를 시작할 때의 전투 준비라서 큐를 거치지 않는다.
        /// 전투 중 적 소환은 SummonAction을 쓴다. 빈 칸이 없으면 false.
        /// </summary>
        public bool PlaceEnemy(RuntimeEnemy enemy, SlotPosition? position, BattleContext context)
        {
            int index = position.HasValue ? (int)position.Value : System.Array.IndexOf(_enemies, null);
            if (index < 0 || _enemies[index] != null) return false;
            Put(index, enemy, context);
            return true;
        }

        /// <summary>아군을 인접한 빈 칸으로 옮길 수 있는지 (`09` §4: 인접 빈 칸만).</summary>
        public bool CanMoveAlly(RuntimeUnit unit, SlotPosition to)
        {
            var from = PositionOf(unit);
            int t = (int)to;
            return from.HasValue && unit.Team == TeamType.Player && t < AllySlotCount
                && System.Math.Abs(t - (int)from.Value) == 1 && IsAllyFree(t);
        }

        /// <summary>위치 변경. MoveUnit 효과(액션)에서만 부른다.</summary>
        internal void MoveAlly(RuntimeUnit unit, SlotPosition to)
        {
            if (!CanMoveAlly(unit, to)) return;
            int from = (int)PositionOf(unit).Value;
            _allies[from] = null;
            _allies[(int)to] = unit;
            unit.PositionDefBonus = to == SlotPosition.Front ? FrontDefBonus : 0;
        }

        /// <summary>SummonAction이 부른다: 예약한 칸에 유닛을 놓는다.</summary>
        internal void Put(int index, RuntimeUnit unit, BattleContext context)
        {
            var slots = Slots(unit.Team);
            slots[index] = unit;
            if (unit.Team != TeamType.Enemy) _allyReserved[index] = false;
            unit.PositionDefBonus = index == (int)SlotPosition.Front ? FrontDefBonus : 0;
            if (unit is RuntimeMonster m && !context.Allies.Contains(m)) context.Allies.Add(m);
            if (unit is RuntimeEnemy e && !context.Enemies.Contains(e)) context.Enemies.Add(e);
        }

        /// <summary>칸에서 뺀다. DeathAction·RemoveTokenAction이 부른다.</summary>
        internal void Remove(RuntimeUnit unit)
        {
            var slots = Slots(unit.Team);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == unit)
                {
                    slots[i] = null;
                    unit.PositionDefBonus = 0;
                }
            }
        }

        /// <summary>Wave 전환: 적 칸을 비우고, 대기 중인 토큰 교체를 취소한다.</summary>
        internal void ResetForNextWave(BattleContext context)
        {
            for (int i = 0; i < EnemySlotCount; i++) _enemies[i] = null;
            _pendingToken = null;
            if (context.State == BattleState.WaitingReplace) context.State = BattleState.PlayerPhase;
        }

        private bool IsAllyFree(int index) => _allies[index] == null && !_allyReserved[index];

        private RuntimeUnit[] Slots(TeamType team) => team == TeamType.Enemy ? _enemies : _allies;
    }
}
