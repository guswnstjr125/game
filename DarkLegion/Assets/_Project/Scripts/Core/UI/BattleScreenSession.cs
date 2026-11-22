using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>전투 화면 입력 (`37` §2.1). 화면은 이 목록으로 잠그고, 통과한 입력만 Core를 부른다.</summary>
    public enum BattleScreenInput
    {
        Summon,
        PlaySkillOrAction,
        Sacrifice,
        Fusion,
        ToggleCommand,
        SetAttackTarget,
        Move,
        EndTurn,
        Inspect,
        ChooseDiscard,
        ChooseReplace
    }

    /// <summary>
    /// 페이즈별 입력 잠금. Main Phase 밖에서는 `37` §2.1 표대로 막고, 커맨드·턴 종료만 Command Phase에서도 연다.
    /// 상세 보기는 항상. 버릴 카드·토큰 교체는 Core가 그 상태로 들어갈 때만 (`09` §3.2, D-63).
    /// </summary>
    public static class BattleScreenRules
    {
        /// <summary>2턴 팁이 닫히는 시간 (`37` §3, 단계 4·5).</summary>
        public const int TipDismissSeconds = 3;

        public static bool PhaseAllows(BattleScreenInput input, BattleState state)
        {
            switch (input)
            {
                case BattleScreenInput.Inspect:
                    return true;
                case BattleScreenInput.Summon:
                case BattleScreenInput.PlaySkillOrAction:
                case BattleScreenInput.Sacrifice:
                case BattleScreenInput.Fusion:
                case BattleScreenInput.Move:
                    return state == BattleState.PlayerPhase;
                case BattleScreenInput.ToggleCommand:
                case BattleScreenInput.SetAttackTarget:
                case BattleScreenInput.EndTurn:
                    return state == BattleState.PlayerPhase || state == BattleState.CommandPhase;
                case BattleScreenInput.ChooseDiscard:
                    return state == BattleState.WaitingDiscard;
                case BattleScreenInput.ChooseReplace:
                    return state == BattleState.WaitingReplace;
                default:
                    return false;
            }
        }

        /// <summary>의도 종류 아이콘의 글자 (D-28). 그림이 없어도 종류만 보인다.</summary>
        public static string IntentLabel(IntentKind kind)
        {
            switch (kind)
            {
                case IntentKind.SingleAttack: return "단일 공격";
                case IntentKind.AoE: return "광역 공격";
                case IntentKind.Buff: return "방어·버프";
                case IntentKind.Debuff: return "디버프";
                case IntentKind.Summon: return "소환";
                case IntentKind.Special: return "특수";
                default: return "";
            }
        }

        public static string CommandLabel(UnitCommand command)
        {
            return command == UnitCommand.Defend ? "방어" : "공격";
        }
    }

    /// <summary>손패 한 장. 사용 불가면 화면이 어둡게 둔다 (`37` §2.1).</summary>
    public sealed class HandCardView
    {
        public int InstanceId;
        public string CardId;
        public CardType Type;
        public int Cost;
        public bool Upgraded;
        public bool Playable;
        public bool NeedsTarget;
    }

    /// <summary>아군 3칸·적 5칸 (`37` §2.1).</summary>
    public sealed class SlotView
    {
        public SlotPosition Position;
        public bool Occupied;
        /// <summary>적 Ex1·Ex2가 비어 있으면 숨긴다.</summary>
        public bool Hidden;
        public string UnitId;
        public string DataId;
        public int Hp;
        public int MaxHp;
        public int Atk;
        public int Def;
        public int Shield;
        public UnitCommand Command;
        public string CommandLabel;
        public IntentKind Intent;
        public string IntentLabel;
        public string AttackTargetId;
        public StatusView[] Statuses = new StatusView[0];
    }

    public sealed class StatusView
    {
        public string Name;
        public int Amount;
    }

    /// <summary>융합 창에 보여줄 비용·예상 결과 (`37` §2.1). 확인 전에 난수를 굴리지 않는다.</summary>
    public sealed class FusionPreview
    {
        public int Cost;
        public string ResultCardId;
        public bool Random;
        public bool CanConfirm;
        public FusionError Error;
    }

    /// <summary>전투 화면이 그릴 값. Core 상태를 읽기만 한다.</summary>
    public sealed class BattleScreenSnapshot
    {
        public int Floor;
        public int Node;
        public int Turn;
        public int MaxTurn;
        public int Hp;
        public int MaxHp;
        public int PlayerShield;
        public int Crystal;
        public int FusionCharge;
        public int DrawCount;
        public int DiscardCount;
        public int ExhaustCount;
        public bool EndTurnEnabled;
        public bool FusionEnabled;
        public bool SacrificeEnabled;
        public BattleOutcome Outcome;
        public BattleState State;
        /// <summary>0이면 안내 문구 없음. 1~5는 `37` §3.</summary>
        public int TutorialStep;
        public string TutorialMessage;
        public bool TutorialBlocking;
        public string HighlightCardId;
        public string HighlightCardId2;
        public SlotPosition? HighlightAllySlot;
        public bool HighlightSacrifice;
        public bool HighlightIntents;
        public bool HighlightCommands;
        public bool WaitingDiscard;
        public bool WaitingReplace;
        public HandCardView[] Hand = new HandCardView[0];
        public SlotView[] Allies = new SlotView[BattleField.AllySlotCount];
        public SlotView[] Enemies = new SlotView[BattleField.EnemySlotCount];
        public StatusView[] PlayerStatuses = new StatusView[0];
        public string[] DrawPile = new string[0];
        public string[] DiscardPile = new string[0];
        public string[] ExhaustPile = new string[0];
    }

    /// <summary>
    /// 전투 화면의 입력과 표시 (`27` BattleViewModel, `37` §2.1·§3).
    /// 전투 규칙은 다시 만들지 않고 <see cref="BattleContext"/>를 부른다.
    /// 1층 노드 1 안내 전투는 1~3단계에서 안내한 입력만 받고, 그 동안 턴 종료·융합을 잠근다.
    /// </summary>
    public sealed class BattleScreenSession
    {
        /// <summary>`18` §2 NEC 시작 덱 10장.</summary>
        public static readonly string[] NecStartDeck =
        {
            "NEC_MON_001", "NEC_MON_002", "NEC_MON_003", "NEC_MON_004", "NEC_SKL_001",
            "NEC_SKL_003", "NEC_ACT_001", "NEU_MON_001", "NEU_MON_003", "NEU_ACT_001"
        };

        public const int StartHp = 80;
        public const string StartRelicId = "REL_BLOOD_CHALICE";

        const string ShardId = "NEU_MON_001";
        const string ExtractId = "NEC_SKL_001";
        const string BoneId = "NEC_MON_001";
        const string GhoulId = "NEC_MON_002";

        enum Guide
        {
            Off,
            Shard,
            Extract,
            Sacrifice,
            Legion,
            Open,
            TipIntent,
            TipCommand,
            TipsDone
        }

        readonly RunState _run;
        Guide _guide;
        float _tipSeconds;
        bool _busy;
        bool _finished;

        public BattleContext Context { get; }
        public RunState Run => _run;

        BattleScreenSession(RunState run, BattleContext context)
        {
            _run = run;
            Context = context;
            _guide = context.IsTutorial ? Guide.Shard : Guide.Off;
        }

        /// <summary>타이틀이 없을 때 1층 노드 1. 시작 덱·피의 잔·HP 80 (`18` §2, D-32).</summary>
        public static BattleScreenSession StartFirstNecBattle(GameDataLoadResult data, long seed, bool tutorialEnabled)
        {
            var run = RunFlow.NewRun("RUN-1", seed, "NEC", StartHp, NecStartDeck, new[] { StartRelicId }, tutorialEnabled);
            return Start(run, data);
        }

        public static BattleScreenSession Start(RunState run, GameDataLoadResult data)
        {
            return Start(run, data, forced: false);
        }

        public static BattleScreenSession StartForced(RunState run, GameDataLoadResult data)
        {
            return Start(run, data, forced: true);
        }

        static BattleScreenSession Start(RunState run, GameDataLoadResult data, bool forced)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (data == null) throw new ArgumentNullException(nameof(data));
            var context = forced ? RunBattle.StartForced(run, data) : RunBattle.Start(run, data);
            var session = new BattleScreenSession(run, context);
            session.BeginPlayerTurn();
            return session;
        }

        public int TutorialStep
        {
            get
            {
                switch (_guide)
                {
                    case Guide.Shard: return 1;
                    case Guide.Extract:
                    case Guide.Sacrifice: return 2;
                    case Guide.Legion: return 3;
                    case Guide.TipIntent: return 4;
                    case Guide.TipCommand: return 5;
                    default: return 0;
                }
            }
        }

        public string TutorialMessage
        {
            get
            {
                switch (_guide)
                {
                    case Guide.Shard:
                        return "지옥에서 힘은 대가로 얻는다. 먼저 수정 골렘 파편을 소환하라. 비용은 없다.";
                    case Guide.Extract:
                    case Guide.Sacrifice:
                        return "영혼 추출로 파편을 지정하고, 그 파편을 희생하라. 지정한 몬스터가 죽으면 카드를 뽑고, 희생한 몬스터는 Crystal이 된다.";
                    case Guide.Legion:
                        return "얻은 Crystal로 군단을 세워라. 뼈 병사와 굶주린 구울을 소환하라.";
                    case Guide.TipIntent:
                        return "적 머리 위 아이콘은 이번 턴 적이 할 행동의 종류다. 공격이 오면 대비하라.";
                    case Guide.TipCommand:
                        return "몬스터를 누르면 공격과 방어를 바꿀 수 있다. 방어하면 이번 턴 Shield를 얻는다.";
                    default:
                        return null;
                }
            }
        }

        public bool TutorialBlocking =>
            _guide == Guide.Shard || _guide == Guide.Extract || _guide == Guide.Sacrifice || _guide == Guide.Legion;

        public bool Allows(BattleScreenInput input)
        {
            if (_busy) return false;
            if (Context.Outcome != BattleOutcome.None) return input == BattleScreenInput.Inspect;
            if (!BattleScreenRules.PhaseAllows(input, Context.State)) return false;
            if (!TutorialBlocking) return true;
            switch (_guide)
            {
                case Guide.Shard:
                case Guide.Legion:
                    return input == BattleScreenInput.Summon || input == BattleScreenInput.Inspect;
                case Guide.Extract:
                    return input == BattleScreenInput.PlaySkillOrAction || input == BattleScreenInput.Inspect;
                case Guide.Sacrifice:
                    return input == BattleScreenInput.Sacrifice || input == BattleScreenInput.Inspect;
                default:
                    return false;
            }
        }

        public BattleScreenSnapshot Capture()
        {
            var player = Context.Player;
            var deck = player.Deck;
            var snap = new BattleScreenSnapshot
            {
                Floor = _run.Floor,
                Node = _run.Node,
                Turn = Context.Turn,
                MaxTurn = BattleLimits.MaxTurnCount,
                Hp = player.CurrentHP,
                MaxHp = player.MaxHP.FinalValue,
                PlayerShield = player.Shield,
                Crystal = player.CurrentCrystal,
                FusionCharge = player.FusionCharge,
                DrawCount = deck.DrawPile.Count,
                DiscardCount = deck.DiscardPile.Count,
                ExhaustCount = deck.ExhaustPile.Count,
                EndTurnEnabled = Allows(BattleScreenInput.EndTurn),
                FusionEnabled = Allows(BattleScreenInput.Fusion) && player.FusionCharge > 0,
                SacrificeEnabled = Allows(BattleScreenInput.Sacrifice) && LivingAlly(null) != null,
                Outcome = Context.Outcome,
                State = Context.State,
                TutorialStep = TutorialStep,
                TutorialMessage = TutorialMessage,
                TutorialBlocking = TutorialBlocking,
                WaitingDiscard = Context.State == BattleState.WaitingDiscard,
                WaitingReplace = Context.State == BattleState.WaitingReplace,
                Hand = deck.Hand.Select(ViewOf).ToArray(),
                PlayerStatuses = StatusesOf(player),
                DrawPile = Ids(deck.DrawPile),
                DiscardPile = Ids(deck.DiscardPile),
                ExhaustPile = Ids(deck.ExhaustPile)
            };
            FillHighlight(snap);
            for (int i = 0; i < BattleField.AllySlotCount; i++)
                snap.Allies[i] = ViewSlot(TeamType.Player, (SlotPosition)i);
            for (int i = 0; i < BattleField.EnemySlotCount; i++)
                snap.Enemies[i] = ViewSlot(TeamType.Enemy, (SlotPosition)i);
            return snap;
        }

        public bool TrySummon(int instanceId, SlotPosition slot)
        {
            if (!Allows(BattleScreenInput.Summon)) return false;
            var card = FindHand(instanceId);
            var def = card == null ? null : Context.Cards?.Get(card.CardId);
            if (def == null || def.Type != CardType.Monster || !TutorialCard(card.CardId)) return false;
            return Commit(() => Context.PlayCard(card, null, slot) == PlayResult.Ok, AfterSummon);
        }

        public bool TryPlay(int instanceId, string targetUnitId)
        {
            if (!Allows(BattleScreenInput.PlaySkillOrAction)) return false;
            var card = FindHand(instanceId);
            var def = card == null ? null : Context.Cards?.Get(card.CardId);
            if (def == null || def.Type == CardType.Monster || def.Type == CardType.Curse || !TutorialPlay(card.CardId, targetUnitId))
                return false;
            var target = FindUnit(targetUnitId);
            return Commit(() => Context.PlayCard(card, target) == PlayResult.Ok, () =>
            {
                if (_guide == Guide.Extract) _guide = Guide.Sacrifice;
            });
        }

        public int SacrificeGain(string unitId)
        {
            var ally = LivingAlly(unitId);
            if (ally == null || ally.IsSoulbound) return 0;
            return SacrificeAction.PreviewGain(Context, ally);
        }

        public bool TrySacrifice(string unitId)
        {
            if (!Allows(BattleScreenInput.Sacrifice)) return false;
            var ally = LivingAlly(unitId);
            if (ally == null || ally.IsSoulbound) return false;
            if (_guide == Guide.Sacrifice && ally.DataId != ShardId) return false;
            int handBefore = Context.Player.Deck.Hand.Count;
            return Commit(() => Context.Sacrifice(ally) == SacrificeResult.Ok, () =>
            {
                if (_guide != Guide.Sacrifice) return;
                // `37` §3 단계 2: Crystal 5, 카드 2장 드로우
                if (Context.Player.CurrentCrystal == 5 && Context.Player.Deck.Hand.Count == handBefore + 2)
                    _guide = Guide.Legion;
            });
        }

        public FusionPreview PreviewFusion(int instanceA, int instanceB)
        {
            var preview = new FusionPreview { Error = FusionError.NotInHand };
            var a = FindHand(instanceA);
            var b = FindHand(instanceB);
            if (a == null || b == null) return preview;
            var da = Context.Cards?.Get(a.CardId);
            var db = Context.Cards?.Get(b.CardId);
            if (da != null && db != null && da.Type == CardType.Monster && db.Type == CardType.Monster
                && da.Level == db.Level && a != b)
            {
                preview.Cost = FusionSystem.CostFor(da.Level);
                if (Context.Fusion != null && Context.Fusion.TryGetFixed(a.CardId, b.CardId, out _, out var result))
                    preview.ResultCardId = result;
                else
                    preview.Random = true;
            }
            var error = FusionSystem.Validate(Context, a, b);
            if (error == FusionError.None && !Allows(BattleScreenInput.Fusion)) error = FusionError.InvalidPhase;
            preview.Error = error;
            preview.CanConfirm = error == FusionError.None;
            return preview;
        }

        public bool TryFuse(int instanceA, int instanceB)
        {
            if (!PreviewFusion(instanceA, instanceB).CanConfirm) return false;
            var a = FindHand(instanceA);
            var b = FindHand(instanceB);
            return Commit(() => Context.Fuse(a, b).Ok, null);
        }

        public bool ToggleCommand(string unitId)
        {
            if (!Allows(BattleScreenInput.ToggleCommand)) return false;
            var ally = LivingAlly(unitId);
            if (ally == null) return false;
            var next = ally.Command == UnitCommand.Attack ? UnitCommand.Defend : UnitCommand.Attack;
            return Context.SetCommand(ally, next);
        }

        public bool TrySetAttackTarget(string allyId, string enemyId)
        {
            if (!Allows(BattleScreenInput.SetAttackTarget)) return false;
            var ally = LivingAlly(allyId);
            var enemy = FindUnit(enemyId) as RuntimeEnemy;
            if (ally == null || enemy == null) return false;
            return Context.SetAttackTarget(ally, enemy);
        }

        public bool TryMove(string unitId, SlotPosition to)
        {
            if (!Allows(BattleScreenInput.Move)) return false;
            var ally = LivingAlly(unitId);
            if (ally == null) return false;
            return Commit(() => Context.TryMoveAlly(ally, to), null);
        }

        public bool TryEndTurn()
        {
            if (!Allows(BattleScreenInput.EndTurn)) return false;
            _busy = true;
            var outcome = Context.EndPlayerTurn();
            _busy = false;
            if (outcome == BattleOutcome.None && Context.Outcome == BattleOutcome.None) BeginPlayerTurn();
            else Settle();
            return true;
        }

        public bool TryDiscard(int instanceId)
        {
            if (!Allows(BattleScreenInput.ChooseDiscard)) return false;
            var card = FindHand(instanceId);
            if (card == null) return false;
            _busy = true;
            bool ok = Context.ResolveDiscard(card);
            _busy = false;
            return ok;
        }

        public bool TryReplace(string unitId)
        {
            if (!Allows(BattleScreenInput.ChooseReplace)) return false;
            var ally = FindUnit(unitId);
            if (ally == null) return false;
            return Commit(() => Context.Field.ResolveReplace(ally, Context), null);
        }

        /// <summary>팁을 누른다 (`37` §3). 4 다음은 5, 5 다음은 안내 끝.</summary>
        public bool DismissTip()
        {
            if (_guide == Guide.TipIntent)
            {
                _guide = Guide.TipCommand;
                _tipSeconds = 0;
                return true;
            }
            if (_guide == Guide.TipCommand)
            {
                _guide = Guide.TipsDone;
                _tipSeconds = 0;
                return true;
            }
            return false;
        }

        /// <summary>팁이 떠 있는 동안만 쌓인다. <see cref="BattleScreenRules.TipDismissSeconds"/>초면 닫힌다.</summary>
        public void AdvanceTipClock(float seconds)
        {
            if (_guide != Guide.TipIntent && _guide != Guide.TipCommand) return;
            if (seconds <= 0) return;
            _tipSeconds += seconds;
            if (_tipSeconds >= BattleScreenRules.TipDismissSeconds) DismissTip();
        }

        void BeginPlayerTurn()
        {
            if (Context.Outcome != BattleOutcome.None)
            {
                Settle();
                return;
            }
            Context.StartPlayerTurn();
            Settle();
            if (Context.Outcome != BattleOutcome.None) return;
            if (_guide == Guide.Open && Context.Turn == 2)
            {
                _guide = Guide.TipIntent;
                _tipSeconds = 0;
            }
        }

        bool Commit(Func<bool> act, Action after)
        {
            _busy = true;
            bool ok = act();
            if (ok) Context.Queue.Process();
            _busy = false;
            if (!ok) return false;
            after?.Invoke();
            Settle();
            return true;
        }

        void AfterSummon()
        {
            if (_guide == Guide.Shard && OnField(ShardId)) _guide = Guide.Extract;
            if (_guide == Guide.Legion && OnField(BoneId) && OnField(GhoulId)) _guide = Guide.Open;
        }

        public void Settle()
        {
            if (_finished || Context.Outcome == BattleOutcome.None) return;
            RunBattle.Finish(_run, Context);
            _finished = true;
        }

        bool TutorialCard(string cardId)
        {
            if (_guide == Guide.Shard) return cardId == ShardId;
            if (_guide == Guide.Legion) return (cardId == BoneId || cardId == GhoulId) && !OnField(cardId);
            return !TutorialBlocking;
        }

        bool TutorialPlay(string cardId, string targetUnitId)
        {
            if (_guide != Guide.Extract) return !TutorialBlocking;
            var target = FindUnit(targetUnitId) as RuntimeMonster;
            return cardId == ExtractId && target != null && target.DataId == ShardId && !target.IsDead;
        }

        bool OnField(string dataId)
        {
            return Context.Allies.Any(a => !a.IsDead && a.DataId == dataId);
        }

        RuntimeCard FindHand(int instanceId)
        {
            return Context.Player.Deck.Hand.FirstOrDefault(c => c.InstanceId == instanceId);
        }

        RuntimeUnit FindUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            RuntimeUnit ally = Context.Allies.FirstOrDefault(a => a.UnitId == unitId);
            if (ally != null) return ally;
            return Context.Enemies.FirstOrDefault(e => e.UnitId == unitId);
        }

        RuntimeMonster LivingAlly(string unitId)
        {
            if (unitId == null)
            {
                return Context.Allies.FirstOrDefault(a => !a.IsDead && !a.IsSoulbound && !a.SacrificeReserved
                    && (_guide != Guide.Sacrifice || a.DataId == ShardId));
            }
            var ally = FindUnit(unitId) as RuntimeMonster;
            if (ally == null || ally.IsDead) return null;
            return ally;
        }

        HandCardView ViewOf(RuntimeCard card)
        {
            var def = Context.Cards?.Get(card.CardId);
            var handler = CardEffects.For(card.CardId);
            var target = handler == null ? CardTarget.None : handler.Target;
            bool monster = def != null && def.Type == CardType.Monster;
            int cost = def == null ? card.Cost : def.CostFor(card.Upgraded);
            bool playable = false;
            if (Context.Outcome == BattleOutcome.None && def != null && def.Type != CardType.Curse
                && Context.Player.CurrentCrystal >= cost)
            {
                if (monster)
                    playable = Allows(BattleScreenInput.Summon) && TutorialCard(card.CardId) && Context.Field.AutoAllySlot().HasValue;
                else if (Allows(BattleScreenInput.PlaySkillOrAction) && TutorialPlay(card.CardId, ExtractTargetId()))
                    playable = TargetAvailable(target);
            }
            return new HandCardView
            {
                InstanceId = card.InstanceId,
                CardId = card.CardId,
                Type = def == null ? CardType.Skill : def.Type,
                Cost = cost,
                Upgraded = card.Upgraded,
                Playable = playable,
                NeedsTarget = target == CardTarget.SelectedEnemy || target == CardTarget.SelectedAlly || target == CardTarget.AdjacentAlly
            };
        }

        /// <summary>추출 단계의 사용 가능 표시는 파편을 대상으로 둔다.</summary>
        string ExtractTargetId()
        {
            if (_guide != Guide.Extract) return null;
            var shard = Context.Allies.FirstOrDefault(a => !a.IsDead && a.DataId == ShardId);
            return shard == null ? "" : shard.UnitId;
        }

        bool TargetAvailable(CardTarget target)
        {
            switch (target)
            {
                case CardTarget.None:
                case CardTarget.OptionalAlly:
                    return true;
                case CardTarget.SelectedEnemy:
                    return Context.Enemies.Any(e => e.IsTargetable);
                case CardTarget.SelectedAlly:
                    return Context.Allies.Any(a => a.IsTargetable && !a.SacrificeReserved);
                case CardTarget.AdjacentAlly:
                    return Context.Field.AutoAllySlot().HasValue && Context.Allies.Any(a => a.IsTargetable);
                default:
                    return false;
            }
        }

        SlotView ViewSlot(TeamType team, SlotPosition pos)
        {
            var unit = Context.Field.At(team, pos);
            bool extra = team == TeamType.Enemy && (pos == SlotPosition.Extra1 || pos == SlotPosition.Extra2);
            var view = new SlotView
            {
                Position = pos,
                Hidden = extra && unit == null,
                Occupied = unit != null && !unit.IsDead
            };
            if (unit == null) return view;
            view.UnitId = unit.UnitId;
            view.DataId = unit.DataId;
            view.Hp = unit.CurrentHP;
            view.MaxHp = unit.MaxHP.FinalValue;
            view.Def = unit.EffectiveDefense;
            view.Shield = unit.Shield;
            view.Statuses = StatusesOf(unit);
            if (unit is RuntimeMonster monster)
            {
                view.Atk = Context.EffectiveAttack(monster);
                view.Command = monster.Command;
                view.CommandLabel = BattleScreenRules.CommandLabel(monster.Command);
                view.AttackTargetId = monster.AttackTarget == null ? null : monster.AttackTarget.UnitId;
            }
            else if (unit is RuntimeEnemy enemy)
            {
                view.Atk = enemy.Attack.FinalValue;
                view.Intent = Context.IntentOf(enemy);
                view.IntentLabel = BattleScreenRules.IntentLabel(view.Intent);
            }
            return view;
        }

        void FillHighlight(BattleScreenSnapshot snap)
        {
            switch (_guide)
            {
                case Guide.Shard:
                    snap.HighlightCardId = ShardId;
                    snap.HighlightAllySlot = SlotPosition.Front;
                    break;
                case Guide.Extract:
                    snap.HighlightCardId = ExtractId;
                    snap.HighlightAllySlot = SlotOf(ShardId);
                    break;
                case Guide.Sacrifice:
                    snap.HighlightSacrifice = true;
                    snap.HighlightAllySlot = SlotOf(ShardId);
                    break;
                case Guide.Legion:
                    snap.HighlightCardId = BoneId;
                    snap.HighlightCardId2 = GhoulId;
                    break;
                case Guide.TipIntent:
                    snap.HighlightIntents = true;
                    break;
                case Guide.TipCommand:
                    snap.HighlightCommands = true;
                    break;
            }
        }

        SlotPosition? SlotOf(string dataId)
        {
            var unit = Context.Allies.FirstOrDefault(a => !a.IsDead && a.DataId == dataId);
            return unit == null ? (SlotPosition?)null : Context.Field.PositionOf(unit);
        }

        static StatusView[] StatusesOf(RuntimeUnit unit)
        {
            var list = new List<StatusView>();
            foreach (StatusType type in Enum.GetValues(typeof(StatusType)))
            {
                if (type == StatusType.Shield) continue;
                int amount = unit.Statuses.Stacks(type);
                if (amount > 0) list.Add(new StatusView { Name = type.ToString(), Amount = amount });
            }
            return list.ToArray();
        }

        /// <summary>더미 목록은 카드 ID 순이다. 뽑을 순서는 보여 주지 않는다 (`37` §2.1 장수·목록).</summary>
        static string[] Ids(IReadOnlyList<RuntimeCard> cards)
        {
            return cards.Select(c => c.CardId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }
    }
}
