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

        private RuntimePlayer _player;

        /// <summary>플레이어. 넣을 때 덱의 드로우 알림을 구독한다 (드로우 시 저주, D-75).</summary>
        public RuntimePlayer Player
        {
            get => _player;
            set
            {
                if (_player != null) _player.Deck.DrawnToHand -= OnCardDrawn;
                _player = value;
                if (_player?.Deck != null) _player.Deck.DrawnToHand += OnCardDrawn;
            }
        }

        private void OnCardDrawn(RuntimeCard card) => CurseEffects.OnDrawn(this, card);
        public BattleField Field { get; } = new BattleField();
        public TriggerBus Triggers { get; } = new TriggerBus();
        /// <summary>카드 정의 (임포터가 채운다). 없으면 카드를 쓸 수 없다.</summary>
        public CardCatalog Cards { get; set; }
        /// <summary>적 행동표 (임포터가 채운다, `10` Enemy_Actions).</summary>
        public EnemyKitCatalog EnemyKits { get; set; }
        /// <summary>융합 레시피·랜덤 풀 (임포터가 채운다, `09` Fusion_Recipes·Fusion_RandomPool).</summary>
        public FusionCatalog Fusion { get; set; }
        /// <summary>이번 전투의 융합 기록. 런 계층이 전투 뒤 런 덱에 반영한다.</summary>
        public System.Collections.Generic.List<FusionRecord> FusionHistory { get; } = new System.Collections.Generic.List<FusionRecord>();

        /// <summary>융합 요청 (`13` §4 RequestFusion). 검사에 실패하면 아무것도 바꾸지 않고 이유를 돌려준다.</summary>
        public FusionOutcome Fuse(RuntimeCard a, RuntimeCard b) => FusionSystem.Fuse(this, a, b);

        /// <summary>보스 턴 사이클 (임포터가 채운다, `14` Boss_TurnCycle).</summary>
        public BossPatternCatalog BossPatterns { get; set; }
        /// <summary>이번 턴 확정된 적 행동 (D-28). 플레이어 턴 시작에 정한다. 보스는 한 턴에 여럿일 수 있다.</summary>
        public System.Collections.Generic.Dictionary<RuntimeEnemy, System.Collections.Generic.IReadOnlyList<EnemyActionDef>> Intents { get; } =
            new System.Collections.Generic.Dictionary<RuntimeEnemy, System.Collections.Generic.IReadOnlyList<EnemyActionDef>>();
        private readonly System.Collections.Generic.Dictionary<RuntimeEnemy, EnemyBrain> _brains =
            new System.Collections.Generic.Dictionary<RuntimeEnemy, EnemyBrain>();
        private readonly System.Collections.Generic.Dictionary<RuntimeEnemy, BossBrain> _bossBrains =
            new System.Collections.Generic.Dictionary<RuntimeEnemy, BossBrain>();

        /// <summary>보스 페이즈가 바뀌었다 (연출·UI 페이즈 아이콘, `22` §2.4·`28` §4). 새 행동은 다음 턴부터 (D-28).</summary>
        public event System.Action<RuntimeEnemy, int> BossPhaseChanged;

        internal EnemyBrain BrainOf(RuntimeEnemy e)
        {
            if (_brains.TryGetValue(e, out var b)) return b;
            var kit = EnemyKits?.Get(e.DataId);
            if (kit == null) return null;
            return _brains[e] = EnemyAi.BuildBrain(kit);
        }

        private BossBrain BossBrainOf(RuntimeEnemy e)
        {
            if (!e.IsBoss) return null;
            if (_bossBrains.TryGetValue(e, out var b)) return b;
            var steps = BossPatterns?.Get(e.DataId);
            if (steps == null) return null;
            return _bossBrains[e] = new BossBrain(e.DataId, steps);
        }

        /// <summary>이번 턴 행동을 정한다 (D-28). 보스 패턴이 있으면 보스 사이클, 없으면 행동표.</summary>
        internal System.Collections.Generic.IReadOnlyList<EnemyActionDef> DecideIntent(RuntimeEnemy e)
        {
            var boss = BossBrainOf(e);
            if (boss != null)
            {
                var actions = boss.Decide(e, out var changed);
                if (changed.HasValue)
                {
                    Log($"[BOSS] {e.UnitId} Phase {changed.Value}");
                    BossPhaseChanged?.Invoke(e, changed.Value);
                }
                return actions;
            }
            var pick = BrainOf(e)?.Decide(e, this);
            return pick?.Tag is EnemyActionDef d ? new[] { d } : null;
        }

        /// <summary>확정한 행동을 Bind·Stun으로 실행하지 못했을 때 결정을 되돌린다 (D-55 #4).</summary>
        internal void UndoIntent(RuntimeEnemy e)
        {
            var boss = BossBrainOf(e);
            if (boss != null) boss.UndoLastDecision();
            else BrainOf(e)?.UndoLastDecision();
        }

        /// <summary>보스의 현재 페이즈 (1~3). 보스 패턴이 없으면 0.</summary>
        public int BossPhaseOf(RuntimeEnemy e) => BossBrainOf(e)?.Phase ?? 0;

        /// <summary>그 적의 이번 턴 의도 종류 (화면은 종류만 보여 준다, D-28). 행동이 여럿이면 D-52 규칙.</summary>
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

        private readonly System.Collections.Generic.List<RuntimeEnemy[]> _pendingWaves = new System.Collections.Generic.List<RuntimeEnemy[]>();

        /// <summary>아직 나오지 않은 Wave 수. 0보다 크면 적이 모두 죽어도 승리가 아니다 (D-72, CORE-03).</summary>
        public int PendingWaveCount => _pendingWaves.Count;

        /// <summary>다음 Wave 적을 예약한다 (전투를 만드는 쪽이 Encounters 순서대로). 배치는 중간 Wave를 끝낸 턴 종료 뒤 (D-72).</summary>
        public void QueueWave(params RuntimeEnemy[] enemies) => _pendingWaves.Add(enemies);

        /// <summary>D-72: 토큰 제거·적 칸 비우기(StartNextWave) → 예약한 적을 빈 칸 앞쪽부터 배치.</summary>
        private void AdvanceWave()
        {
            var next = _pendingWaves[0];
            _pendingWaves.RemoveAt(0);
            StartNextWave();
            foreach (var e in next) Field.PlaceEnemy(e, null, this);
            Log($"[WAVE] next wave {next.Length} enemies, {_pendingWaves.Count} left");
        }

        /// <summary>
        /// Wave 정리 (`09` §6, `03` §2): 토큰은 RemoveTokenAction으로 제거(사망 아님), 몬스터는 HP·상태·위치 유지, 적 칸 비움.
        /// 예약한 Wave(<see cref="QueueWave"/>)는 턴 종료가 이것을 부른 뒤 배치한다. Crystal 리셋·Draw 5는 다음 턴 시작이 한다 (D-72).
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
            var outcome = EvaluateOutcome();
            // 중간 Wave를 끝낸 턴: 턴 종료가 끝난 뒤 다음 Wave (D-72). 30턴 패배는 위에서 먼저 정해진다
            if (outcome == BattleOutcome.None && LivingEnemyCount == 0 && _pendingWaves.Count > 0) AdvanceWave();
            return outcome;
        }

        /// <summary>전투가 끝나면 상태이상을 전부 지운다 (Poison 포함, D-08). Wave 전환에서는 부르지 않는다.</summary>
        public void EndBattleCleanup()
        {
            foreach (var a in Allies) a.Statuses.ClearAll();
            foreach (var e in Enemies) e.Statuses.ClearAll();
            if (Player != null)
            {
                Player.Statuses.ClearAll();
                Player.NextTurnCrystal = 0; // 다음 턴 Crystal은 전투 안에서만 (D-70)
            }
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
            else if (livingEnemies <= 0 && _pendingWaves.Count == 0)
            {
                Outcome = BattleOutcome.Victory; // 마지막 Wave까지 끝났을 때만 (D-72)
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
