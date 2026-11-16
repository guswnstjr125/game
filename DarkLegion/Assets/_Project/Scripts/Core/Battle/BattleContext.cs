namespace DarkLegion.Core
{
    public enum BattleState
    {
        Idle,
        PlayerPhase,
        /// <summary>토큰 교체 대상 선택 대기 (`09` §3.2). Main Phase에서만 들어간다.</summary>
        WaitingReplace,
        /// <summary>손패에서 버릴 카드 선택 대기 (기회 포착, D-63).</summary>
        WaitingDiscard,
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
        public BattleField Field { get; } = new BattleField();
        public TriggerBus Triggers { get; } = new TriggerBus();
        /// <summary>카드 정의 (임포터가 채운다). 없으면 카드를 쓸 수 없다.</summary>
        public CardCatalog Cards { get; set; }
        /// <summary>적 행동표 (임포터가 채운다, `10` Enemy_Actions).</summary>
        public EnemyKitCatalog EnemyKits { get; set; }
        /// <summary>이번 턴 확정된 적 행동 (D-28). 플레이어 턴 시작에 정한다.</summary>
        public System.Collections.Generic.Dictionary<RuntimeEnemy, EnemyActionDef> Intents { get; } =
            new System.Collections.Generic.Dictionary<RuntimeEnemy, EnemyActionDef>();
        private readonly System.Collections.Generic.Dictionary<RuntimeEnemy, EnemyBrain> _brains =
            new System.Collections.Generic.Dictionary<RuntimeEnemy, EnemyBrain>();

        internal EnemyBrain BrainOf(RuntimeEnemy e)
        {
            if (_brains.TryGetValue(e, out var b)) return b;
            var kit = EnemyKits?.Get(e.DataId);
            if (kit == null) return null;
            return _brains[e] = EnemyAi.BuildBrain(kit);
        }

        /// <summary>그 적의 이번 턴 의도 종류 (화면은 종류만 보여 준다, D-28).</summary>
        public IntentKind IntentOf(RuntimeEnemy e) => Intents.TryGetValue(e, out var d) ? EnemyAi.KindOf(d) : IntentKind.None;

        private int _spawnIndex;
        internal int NextSpawnIndex() => ++_spawnIndex;

        public void StartPlayerTurn() => TurnFlow.StartPlayerTurn(this);

        public BattleOutcome EndPlayerTurn() => TurnFlow.EndPlayerTurn(this);

        /// <summary>커맨드 변경 (D-27). Player Phase에서만, Bind·Stun이면 불가.</summary>
        public bool SetCommand(RuntimeMonster m, UnitCommand cmd)
        {
            if (State != BattleState.PlayerPhase || m == null || m.IsDead || !Allies.Contains(m) || !m.Statuses.CanAct) return false;
            m.Command = cmd;
            return true;
        }

        /// <summary>공격 대상 변경 (D-27). 살아 있는 적만.</summary>
        public bool SetAttackTarget(RuntimeMonster m, RuntimeEnemy target)
        {
            if (State != BattleState.PlayerPhase || m == null || m.IsDead || !Allies.Contains(m) || !m.Statuses.CanAct
                || target == null || !target.IsTargetable || !Enemies.Contains(target)) return false;
            m.Command = UnitCommand.Attack;
            m.AttackTarget = target;
            return true;
        }
        /// <summary>이번 턴 희생 횟수. 피의 잔·REL_BOSS_1F의 "턴당 첫 희생" 판정에 쓴다 (D-04·D-15).</summary>
        public int SacrificesThisTurn { get; internal set; }
        /// <summary>턴의 첫 희생에만 더하는 Crystal. 피의 잔 +1, REL_BOSS_1F +1 (D-04·D-15). 유물을 얻을 때 유물 시스템이 정한다.</summary>
        public int FirstSacrificeBonus { get; set; }
        public System.Collections.Generic.List<RuntimeMonster> Allies { get; } = new System.Collections.Generic.List<RuntimeMonster>();
        public System.Collections.Generic.List<RuntimeEnemy> Enemies { get; } = new System.Collections.Generic.List<RuntimeEnemy>();
        /// <summary>사망한 유닛 (`06` §12.2 Graveyard). 순서 = 사망 순서.</summary>
        public System.Collections.Generic.List<RuntimeUnit> Graveyard { get; } = new System.Collections.Generic.List<RuntimeUnit>();

        public BattleContext(int seed)
        {
            Random = new BattleRandom(seed);
            Queue = new QueueProcessor(this);
            MonsterEffects.Attach(this);
            Triggers.UnitDied += FireDeathDrawWatchers;
        }

        public void Log(string message) => Queue.WriteLog(message);

        /// <summary>
        /// 카드 사용 (`04` §4, `10`). 검사에 통과하면 비용을 내고 카드를 옮긴 뒤 효과를 큐에 넣는다. 큐 처리는 호출하는 쪽이 한다.
        /// target: 대상 유닛, position: 몬스터 소환 칸·이동 칸, chosenCard: 카드를 고르는 효과의 선택.
        /// </summary>
        public PlayResult PlayCard(RuntimeCard card, RuntimeUnit target, SlotPosition? position = null, RuntimeCard chosenCard = null)
        {
            return CardPlayer.Play(this, new CardPlay { Card = card, Target = target, Position = position, ChosenCard = chosenCard });
        }

        public int PendingDiscards { get; private set; }

        /// <summary>손패에서 n장을 골라 버리게 한다 (효과 실행 중에 부른다). 손패가 비면 아무것도 하지 않는다.</summary>
        internal void RequestDiscard(int n)
        {
            if (Player.Deck.Hand.Count == 0 || n <= 0) return;
            PendingDiscards = System.Math.Min(n, Player.Deck.Hand.Count);
            State = BattleState.WaitingDiscard;
        }

        /// <summary>버릴 카드 선택. 손패의 카드만. 다 고르면 Player Phase로 돌아간다.</summary>
        public bool ResolveDiscard(RuntimeCard card)
        {
            if (State != BattleState.WaitingDiscard || card == null || card.Zone != CardZone.Hand) return false;
            Player.Deck.DiscardFromHand(card);
            if (--PendingDiscards <= 0 || Player.Deck.Hand.Count == 0)
            {
                PendingDiscards = 0;
                State = BattleState.PlayerPhase;
            }
            return true;
        }

        /// <summary>회복: 최대 HP를 넘지 않는다. CannotHeal이면 무시.</summary>
        public void Heal(RuntimeUnit unit, int amount)
        {
            if (unit == null || unit.IsDead || unit.Flags.CannotHeal || amount <= 0) return;
            unit.CurrentHP = System.Math.Min(unit.MaxHP.FinalValue, unit.CurrentHP + amount);
        }

        private readonly System.Collections.Generic.List<(RuntimeUnit unit, int draw, int turn)> _deathDrawWatchers =
            new System.Collections.Generic.List<(RuntimeUnit, int, int)>();

        /// <summary>영혼 추출 (D-42): 이번 턴 그 유닛이 죽으면(희생 포함) draw장 드로우.</summary>
        internal void WatchDeathDraw(RuntimeUnit unit, int draw) => _deathDrawWatchers.Add((unit, draw, Turn));

        private void FireDeathDrawWatchers(BattleContext c, RuntimeUnit dead, bool isSacrifice)
        {
            foreach (var w in _deathDrawWatchers.ToArray())
            {
                if (w.unit == dead && w.turn == Turn)
                {
                    _deathDrawWatchers.Remove(w);
                    Queue.EnqueueTrigger(new EffectAction(BattleActionType.Draw, "SoulExtractDraw", x => x.Player.Deck.Draw(w.draw)));
                }
            }
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

        /// <summary>
        /// 희생 요청 (`13` §2). Player Phase에서만, 아군 몬스터만, Soulbound 불가. Bind는 막지 않는다 (`11` §2).
        /// 통과하면 대상을 예약하고 SacrificeAction을 Main 큐에 넣는다 (즉시 제거 금지).
        /// </summary>
        public SacrificeResult Sacrifice(RuntimeMonster target)
        {
            if (State != BattleState.PlayerPhase) return SacrificeResult.InvalidPhase;
            if (target == null || !Allies.Contains(target)) return SacrificeResult.NotAlly;
            if (target.IsDead) return SacrificeResult.TargetDead;
            if (target.SacrificeReserved) return SacrificeResult.AlreadyReserved;
            if (target.IsSoulbound) return SacrificeResult.Soulbound;
            target.SacrificeReserved = true; // 예약만 한다. 칸과 타겟팅은 SacrificeAction이 실행될 때까지 그대로 (`13` §2.2)
            Queue.EnqueueMain(new SacrificeAction { Target = target });
            return SacrificeResult.Ok;
        }

        /// <summary>
        /// 다음 Wave (`09` §6, `03` §2): 토큰은 RemoveTokenAction으로 제거(사망 아님), 몬스터는 HP·상태·위치 유지, 적 칸 비움.
        /// 새 적 배치와 Crystal·손패 처리는 Wave를 시작하는 쪽이 한다.
        /// </summary>
        public void StartNextWave()
        {
            foreach (var a in Allies.ToArray())
            {
                if (a.IsToken) Queue.EnqueueMain(new RemoveTokenAction { Token = a });
            }
            Queue.Process();
            Field.ResetForNextWave(this);
            Enemies.Clear();
        }

        internal void RemoveUnit(RuntimeUnit unit)
        {
            Field.Remove(unit);
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
            SacrificesThisTurn = 0;
            _deathDrawWatchers.RemoveAll(w => w.turn < Turn);
            foreach (var a in Allies) a.ExtraActionsThisTurn = 0;
            State = BattleState.PlayerPhase;
        }

        /// <summary>전투에 있는 살아 있는 유닛 전부 (아군 Front→Back 순서는 슬롯 구현 #42에서, 지금은 목록 순서).</summary>
        private System.Collections.Generic.List<RuntimeUnit> LivingUnits()
        {
            var list = new System.Collections.Generic.List<RuntimeUnit>();
            foreach (var a in Allies) if (!a.IsDead) list.Add(a);
            foreach (var e in Enemies) if (!e.IsDead) list.Add(e);
            if (Player != null && !Player.IsDead) list.Add(Player);
            return list;
        }

        /// <summary>
        /// 턴 종료 (`06` §13.1): Burn·Poison 틱 → Burn −1·Shield 소멸·지속 턴 감소 → 손패 버림 → 승패 판정.
        /// </summary>
        public BattleOutcome EndTurn()
        {
            State = BattleState.TurnEnd;
            var units = LivingUnits();
            foreach (var u in units)
            {
                foreach (var dot in new[] { StatusType.Burn, StatusType.Poison })
                {
                    int stacks = u.Statuses.Stacks(dot);
                    if (stacks > 0)
                    {
                        Queue.EnqueueMain(new DamageAction { Target = u, BaseDamage = stacks, IsStatusTick = true });
                    }
                }
            }
            Queue.Process();
            foreach (var u in units) u.Statuses.ExpireAtTurnEnd();
            if (Player != null) Player.Deck.EndTurn();
            return EvaluateOutcome();
        }

        /// <summary>전투가 끝나면 상태이상을 전부 지운다 (Poison 포함, D-08). Wave 전환에서는 부르지 않는다.</summary>
        public void EndBattleCleanup()
        {
            foreach (var a in Allies) a.Statuses.ClearAll();
            foreach (var e in Enemies) e.Statuses.ClearAll();
            if (Player != null) Player.Statuses.ClearAll();
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
