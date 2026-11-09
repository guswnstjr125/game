namespace DarkLegion.Core
{
    public enum BattleState
    {
        Idle,
        PlayerPhase,
        CommandPhase,
        Resolving,
        EnemyPhase,
        TurnEnd,
        End
    }

    public enum BattleOutcome
    {
        None,
        Victory,
        Defeat
    }

    /// <summary>
    /// 전투 하나의 런타임 상태 진입점 (`06` §14). 유닛·카드·트리거는 이후 이슈(#37·#38·#40)에서 붙인다.
    /// </summary>
    public sealed class BattleContext
    {
        public BattleRandom Random { get; }
        public QueueProcessor Queue { get; }
        public BattleState State { get; set; } = BattleState.Idle;
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.None;

        /// <summary>현재 턴 (1부터). <see cref="BeginTurn"/>마다 1 증가.</summary>
        public int Turn { get; private set; }

        public RuntimePlayer Player { get; set; }
        public System.Collections.Generic.List<RuntimeMonster> Allies { get; } = new System.Collections.Generic.List<RuntimeMonster>();
        public System.Collections.Generic.List<RuntimeEnemy> Enemies { get; } = new System.Collections.Generic.List<RuntimeEnemy>();
        /// <summary>사망한 유닛 (`06` §12.2 Graveyard). 순서 = 사망 순서.</summary>
        public System.Collections.Generic.List<RuntimeUnit> Graveyard { get; } = new System.Collections.Generic.List<RuntimeUnit>();

        public BattleContext(int seed)
        {
            Random = new BattleRandom(seed);
            Queue = new QueueProcessor(this);
        }

        /// <summary>살아 있고 타겟 가능한 유닛 (팀 필터). 사망 처리 대기 중(IsDead)인 유닛은 빠진다.</summary>
        public System.Collections.Generic.List<RuntimeUnit> Targetable(TeamType team)
        {
            var list = new System.Collections.Generic.List<RuntimeUnit>();
            if (team == TeamType.Player)
            {
                foreach (var a in Allies) if (a.IsTargetable) list.Add(a);
            }
            else if (team == TeamType.Enemy)
            {
                foreach (var e in Enemies) if (e.IsTargetable) list.Add(e);
            }
            return list;
        }

        public int LivingEnemyCount
        {
            get
            {
                int n = 0;
                foreach (var e in Enemies) if (!e.IsDead) n++;
                return n;
            }
        }

        internal void RemoveUnit(RuntimeUnit unit)
        {
            bool removed = unit is RuntimeMonster m ? Allies.Remove(m) : unit is RuntimeEnemy e && Enemies.Remove(e);
            if (removed) Graveyard.Add(unit);
        }

        /// <summary>현재 상태로 승패 판정 (플레이어·적 목록 사용).</summary>
        public BattleOutcome EvaluateOutcome()
        {
            return EvaluateOutcome(Player != null ? Player.CurrentHP : 1, LivingEnemyCount);
        }

        public void BeginTurn()
        {
            Turn++;
            State = BattleState.PlayerPhase;
        }

        /// <summary>
        /// 승패 판정 진입점 (`06` §13.1 끝). 적이 모두 죽으면 승리, 플레이어 HP ≤ 0이면 패배,
        /// 30턴째 턴 종료에도 끝나지 않으면 패배 (`03` §2 노드 30턴 초과). 큐 비상 정지도 패배로 끝낸다.
        /// </summary>
        public BattleOutcome EvaluateOutcome(int playerHp, int livingEnemies)
        {
            if (Outcome != BattleOutcome.None) return Outcome;
            if (playerHp <= 0 || Queue.EmergencyStopped)
            {
                Outcome = BattleOutcome.Defeat;
            }
            else if (livingEnemies <= 0)
            {
                Outcome = BattleOutcome.Victory;
            }
            else if (Turn >= BattleLimits.MaxTurnCount && State == BattleState.TurnEnd)
            {
                Outcome = BattleOutcome.Defeat;
            }
            if (Outcome != BattleOutcome.None) State = BattleState.End;
            return Outcome;
        }
    }
}
