using System.Collections.Generic;

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

    public enum SummonResult
    {
        Placed,
        /// <summary>몬스터 카드 소환인데 아군 3칸이 다 참: 거부, 비용을 쓰지 않는다 (`09` §3).</summary>
        RejectedFullField,
        /// <summary>토큰 소환인데 3칸이 다 참: 교체할 아군을 고를 때까지 대기 (`09` §3, BattleState.WaitingReplace).</summary>
        WaitingReplace,
        RejectedOccupied
    }

    /// <summary>
    /// 아군 3 / 적 5 슬롯 (`09`). 유닛이 들어가고 나가는 곳은 여기 하나다.
    /// Front 위치의 유닛은 DEF +1을 받는다 (`04` Position_Bonus, D-01). 사망 후 자동 당김은 없다.
    /// </summary>
    public sealed class BattleField
    {
        public const int AllySlotCount = 3;
        public const int EnemySlotCount = 5;
        public const int FrontDefBonus = 1;

        private readonly RuntimeUnit[] _allies = new RuntimeUnit[AllySlotCount];
        private readonly RuntimeUnit[] _enemies = new RuntimeUnit[EnemySlotCount];
        private RuntimeMonster _pendingToken;

        public RuntimeUnit At(TeamType team, SlotPosition pos)
        {
            var slots = Slots(team);
            int i = (int)pos;
            return i < slots.Length ? slots[i] : null;
        }

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

        /// <summary>앞줄 = 비어 있지 않은 첫 슬롯 Front → Middle → Back (`09` §7). 살아 있는 유닛만.</summary>
        public RuntimeUnit FrontLine(TeamType team)
        {
            var slots = Slots(team);
            for (int i = 0; i < 3; i++)
            {
                if (slots[i] != null && !slots[i].IsDead) return slots[i];
            }
            return null;
        }

        /// <summary>아군 소환. position이 없으면 Back → Middle → Front 첫 빈 칸 (`09` §2.1).</summary>
        public SummonResult TrySummonAlly(RuntimeMonster monster, SlotPosition? position, BattleContext context)
        {
            if (position.HasValue)
            {
                if ((int)position.Value >= AllySlotCount) return SummonResult.RejectedOccupied;
                if (_allies[(int)position.Value] != null) return SummonResult.RejectedOccupied;
                Place(_allies, (int)position.Value, monster, context);
                return SummonResult.Placed;
            }
            for (int i = AllySlotCount - 1; i >= 0; i--)
            {
                if (_allies[i] == null)
                {
                    Place(_allies, i, monster, context);
                    return SummonResult.Placed;
                }
            }
            if (!monster.IsToken) return SummonResult.RejectedFullField;
            _pendingToken = monster;
            context.State = BattleState.WaitingReplace;
            return SummonResult.WaitingReplace;
        }

        /// <summary>
        /// 토큰 교체: 고른 아군은 사망 처리(희생 아님, Crystal 없음)되고 그 칸에 대기 중인 토큰이 들어간다 (`09` §3).
        /// 칸은 DeathAction이 비우므로, 사망 처리가 끝난 뒤 토큰을 놓는다.
        /// </summary>
        public void ResolveReplace(RuntimeUnit victim, BattleContext context)
        {
            if (_pendingToken == null || victim == null || victim.Team != TeamType.Player) return;
            var pos = PositionOf(victim);
            if (!pos.HasValue) return;
            var token = _pendingToken;
            _pendingToken = null;
            if (victim.MarkDead())
            {
                context.Queue.EnqueueDeath(new DeathAction { Unit = victim, OnRemoved = c => Place(_allies, (int)pos.Value, token, c) });
            }
            context.State = BattleState.PlayerPhase;
        }

        /// <summary>적 배치. position이 없으면 Front부터 첫 빈 칸 (`09` §2.1). 빈 칸이 없으면 false.</summary>
        public bool PlaceEnemy(RuntimeEnemy enemy, SlotPosition? position, BattleContext context)
        {
            if (position.HasValue)
            {
                if (_enemies[(int)position.Value] != null) return false;
                Place(_enemies, (int)position.Value, enemy, context);
                return true;
            }
            for (int i = 0; i < EnemySlotCount; i++)
            {
                if (_enemies[i] == null)
                {
                    Place(_enemies, i, enemy, context);
                    return true;
                }
            }
            return false;
        }

        /// <summary>칸에서 뺀다. 사망 처리(DeathAction)와 토큰 제거가 부른다.</summary>
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

        internal void ClearEnemies()
        {
            for (int i = 0; i < EnemySlotCount; i++) _enemies[i] = null;
        }

        private void Place(RuntimeUnit[] slots, int index, RuntimeUnit unit, BattleContext context)
        {
            slots[index] = unit;
            unit.PositionDefBonus = index == (int)SlotPosition.Front ? FrontDefBonus : 0;
            if (unit is RuntimeMonster m && !context.Allies.Contains(m)) context.Allies.Add(m);
            if (unit is RuntimeEnemy e && !context.Enemies.Contains(e)) context.Enemies.Add(e);
        }

        private RuntimeUnit[] Slots(TeamType team) => team == TeamType.Enemy ? _enemies : _allies;
    }

    public enum SacrificeResult
    {
        Ok,
        InvalidPhase,
        NotAlly,
        TargetDead,
        Soulbound
    }

    /// <summary>
    /// 희생 (`13` §2): Crystal 지급 → (OnSacrifice 트리거 자리) → 사망 처리. 몬스터 카드는 버린 더미로.
    /// Crystal: Lv1 +1, Lv2 +2, Lv3 +3. 파편·수정 골렘처럼 다른 값은 SacrificeCrystalOverride (`13` §2.3, D-04).
    /// </summary>
    public sealed class SacrificeAction : BattleAction
    {
        public RuntimeMonster Target;

        public override BattleActionType Type => BattleActionType.Sacrifice;

        public static int CrystalFor(RuntimeMonster m)
        {
            return m.SacrificeCrystalOverride ?? m.Level;
        }

        public override void Execute(BattleContext context)
        {
            if (Target == null || Target.IsDead && !Target.SacrificeReserved) return;
            context.Player.CurrentCrystal += CrystalFor(Target);
            context.SacrificesThisTurn++;
            context.Queue.EnqueueDeath(new DeathAction { Unit = Target, IsSacrifice = true });
        }

        public override string ToString() => $"Sacrifice {Target?.UnitId}";
    }
}
