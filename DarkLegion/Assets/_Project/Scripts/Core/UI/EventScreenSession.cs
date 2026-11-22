using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    public enum EventScreenPhase
    {
        /// <summary>선택지 3개를 보여 준다.</summary>
        Choosing,
        /// <summary>고른 선택지가 카드·유물·희생·강화 대상을 더 골라야 한다. 아직 런은 바뀌지 않았다.</summary>
        Picking,
        /// <summary>확정됨. 결과 문구와 변화를 보여 주고 [계속]을 기다린다.</summary>
        Result
    }

    public sealed class EventOptionView
    {
        public int Index;
        public string Label;
        /// <summary>조건 문구 (`40`). 조건이 없으면 빈 문자열.</summary>
        public string Condition;
        public bool Enabled;
    }

    public sealed class EventCardView
    {
        public int InstanceId;
        public string CardId;
        public bool Upgraded;
    }

    /// <summary>이벤트 확정 결과. 맵은 이 값으로 노드를 완료하거나 강제 전투를 시작한다.</summary>
    public sealed class EventScreenResult
    {
        public string NodeId;
        public string EventId;
        public int Option;
        public string Text;
        public int HpBefore;
        public int HpAfter;
        public int MaxHpBefore;
        public int MaxHpAfter;
        public int GoldBefore;
        public int GoldAfter;
        public string[] CardsGained = new string[0];
        public string[] CursesGained = new string[0];
        public string RelicGained;
        public string SacrificedCardId;
        public string[] UpgradedCardIds = new string[0];
        /// <summary>영혼 감옥 50%: 노드 완료 전에 맵이 RunBattle.StartForced로 전투를 열고 승리 뒤 일반 전투 보상을 준다 (D-68).</summary>
        public bool ForcedBattle;
        /// <summary>HP가 0 이하가 되면 Defeat (D-51).</summary>
        public RunOutcome RunOutcome;

        public int HpDelta => HpAfter - HpBefore;
        public int MaxHpDelta => MaxHpAfter - MaxHpBefore;
        public int GoldDelta => GoldAfter - GoldBefore;
    }

    public sealed class EventScreenSnapshot
    {
        public int Floor;
        public string NodeId;
        public string EventId;
        public string Title;
        public string Intro;
        public int Hp;
        public int MaxHp;
        public int Gold;
        public EventScreenPhase Phase;
        public EventOptionView[] Options = new EventOptionView[0];
        public int ChosenOption;
        public bool NeedsCard;
        public string[] CardChoices = new string[0];
        public int SelectedCard = -1;
        public bool NeedsRelic;
        public string[] RelicChoices = new string[0];
        public int SelectedRelic = -1;
        public bool NeedsSacrifice;
        public EventCardView[] SacrificeCandidates = new EventCardView[0];
        public int SelectedSacrifice = -1;
        public bool NeedsUpgrade;
        public EventCardView[] UpgradeCandidates = new EventCardView[0];
        public int SelectedUpgrade = -1;
        public bool CanConfirm;
        public EventScreenResult Result;
    }

    /// <summary>
    /// 이벤트 선택 화면 (`37` §2.5, `24` §2, `40`). 규칙은 <see cref="EventFlow"/>를 그대로 부르고 새 규칙은 두지 않는다.
    /// 선택 → (필요하면 대상 고르기) → 확정 → 결과 → [계속]. 노드 완료(RunFlow.CompleteNode)와 강제 전투는 호출하는 맵의 몫이다.
    /// 확정 전에는 런이 바뀌지 않는다.
    /// </summary>
    public sealed class EventScreenSession
    {
        readonly RunState _run;
        readonly GameDataLoadResult _data;
        readonly Action<EventScreenResult> _onComplete;
        readonly Func<string, string> _nameOf;
        readonly int _floor;
        readonly string _nodeId;
        EventOutcome _outcome;
        EventChoice _choice = new EventChoice();
        int _selectedCard = -1;
        int _selectedRelic = -1;
        EventScreenResult _result;
        bool _completed;

        public EventScreenPhase Phase { get; private set; } = EventScreenPhase.Choosing;
        public string EventId { get; }
        public EventScreenResult Result => _result;
        public bool Completed => _completed;

        /// <param name="floor">이벤트 노드의 층. 1·2층만 받는다.</param>
        /// <param name="eventId">EventFlow.Enter가 정한 이벤트 ID. 그 층 풀에 있어야 한다.</param>
        /// <param name="run">현재 노드가 그 이벤트 노드인 런. 확정 때 이 객체를 바꾼다.</param>
        /// <param name="onComplete">결과에서 [계속]을 누르면 한 번 부른다.</param>
        /// <param name="nameOf">카드·유물 ID → 표시 이름. 없으면 ID를 그대로 쓴다.</param>
        public EventScreenSession(int floor, string eventId, RunState run, GameDataLoadResult data,
            Action<EventScreenResult> onComplete = null, Func<string, string> nameOf = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (floor < 1 || floor > 2) throw new ArgumentException("이벤트 화면은 1·2층만 연다: " + floor, nameof(floor));
            if (run.Floor != floor) throw new ArgumentException($"런 층 {run.Floor}과 입력 층 {floor}이 다르다", nameof(floor));
            if (run.Outcome != RunOutcome.InProgress) throw new ArgumentException("끝난 런이다", nameof(run));
            if (!EventFlow.Implemented.Contains(eventId) || !EventScreenText.Has(eventId))
                throw new ArgumentException("구현되지 않은 이벤트: " + eventId, nameof(eventId));
            if (!data.Events.Floor(floor).Any(e => e.id == eventId))
                throw new ArgumentException($"{eventId}는 {floor}층 풀에 없다", nameof(eventId));
            _floor = floor;
            _nodeId = run.CurrentNodeId;
            _onComplete = onComplete;
            _nameOf = nameOf ?? (id => id);
            EventId = eventId;
        }

        public bool IsEnabled(int option) =>
            Phase == EventScreenPhase.Choosing && option >= 1 && option <= 3 && EventFlow.IsEnabled(_run, _data, EventId, option);

        /// <summary>선택지를 고른다. 대상을 더 골라야 하면 Picking, 아니면 바로 확정해 Result로 간다.</summary>
        public bool Choose(int option)
        {
            if (!IsEnabled(option)) return false;
            _outcome = EventFlow.Choose(_run, _data, EventId, option);
            _choice = new EventChoice();
            _selectedCard = -1;
            _selectedRelic = -1;
            if (!NeedsPick)
            {
                if (Confirm()) return true;
                _outcome = null;
                return false;
            }
            Phase = EventScreenPhase.Picking;
            return true;
        }

        bool NeedsPick => _outcome != null
            && ((_outcome.CardChooseOne && _outcome.CardChoices.Count > 0)
                || _outcome.RelicChoices.Count > 0
                || _outcome.NeedsSacrificeChoice
                || _outcome.NeedsUpgradeChoice);

        /// <summary>대상 고르기를 취소하고 선택지로 돌아간다. 런은 바뀌지 않았다.</summary>
        public bool Back()
        {
            if (Phase != EventScreenPhase.Picking) return false;
            _outcome = null;
            Phase = EventScreenPhase.Choosing;
            return true;
        }

        /// <summary>카드 3택1에서 한 장 고른다. null이면 건너뛰기 (D-75).</summary>
        public bool SelectCard(int? index)
        {
            if (Phase != EventScreenPhase.Picking || !_outcome.CardChooseOne || _outcome.CardChoices.Count == 0) return false;
            if (index.HasValue && (index < 0 || index >= _outcome.CardChoices.Count)) return false;
            _choice.CardIndex = index;
            _selectedCard = index ?? -1;
            return true;
        }

        public bool SelectRelic(int index)
        {
            if (Phase != EventScreenPhase.Picking || _outcome.RelicChoices.Count == 0) return false;
            if (index < 0 || index >= _outcome.RelicChoices.Count) return false;
            _choice.RelicIndex = index;
            _selectedRelic = index;
            return true;
        }

        public bool SelectSacrifice(int instanceId)
        {
            if (Phase != EventScreenPhase.Picking || !_outcome.NeedsSacrificeChoice) return false;
            if (!SacrificeCandidates().Any(c => c.InstanceId == instanceId)) return false;
            _choice.SacrificeInstanceId = instanceId;
            if (_choice.UpgradeInstanceId == instanceId) _choice.UpgradeInstanceId = null;
            return true;
        }

        public bool SelectUpgrade(int instanceId)
        {
            if (Phase != EventScreenPhase.Picking || !_outcome.NeedsUpgradeChoice) return false;
            if (!UpgradeCandidates().Any(c => c.InstanceId == instanceId)) return false;
            _choice.UpgradeInstanceId = instanceId;
            return true;
        }

        public bool CanConfirm
        {
            get
            {
                if (Phase != EventScreenPhase.Picking) return false;
                if (_outcome.RelicChoices.Count > 0 && !_choice.RelicIndex.HasValue) return false;
                if (_outcome.NeedsSacrificeChoice && !_choice.SacrificeInstanceId.HasValue) return false;
                if (_outcome.NeedsUpgradeChoice && !_choice.UpgradeInstanceId.HasValue) return false;
                return true;
            }
        }

        /// <summary>마지막 확인. EventFlow.Confirm이 입력을 모두 검사한 뒤에만 런을 바꾼다.</summary>
        public bool Confirm()
        {
            if (_outcome == null || (Phase == EventScreenPhase.Picking && !CanConfirm)) return false;
            if (Phase == EventScreenPhase.Result) return false;

            int hp = _run.CurrentHp;
            int maxHp = _run.MaxHp;
            int gold = _run.Gold;
            var deckBefore = _run.Deck.ToDictionary(c => c.InstanceId, c => (c.CardId, c.Upgraded));
            var relicsBefore = new HashSet<string>(_run.Relics);
            if (!EventFlow.Confirm(_run, _data, _outcome, _choice)) return false;

            var gained = _run.Deck.Where(c => !deckBefore.ContainsKey(c.InstanceId)).ToList();
            var curses = gained.Where(c => _data.Cards.Get(c.CardId)?.Type == CardType.Curse).Select(c => c.CardId).ToArray();
            var cards = gained.Where(c => _data.Cards.Get(c.CardId)?.Type != CardType.Curse).Select(c => c.CardId).ToArray();
            string sacrificed = deckBefore.Where(kv => _run.Deck.All(c => c.InstanceId != kv.Key)).Select(kv => kv.Value.CardId).FirstOrDefault();
            var upgraded = _run.Deck.Where(c => deckBefore.TryGetValue(c.InstanceId, out var before) && !before.Upgraded && c.Upgraded)
                .Select(c => c.CardId).ToArray();
            string curseName = curses.Length > 0 ? _nameOf(curses[0]) : "";
            _result = new EventScreenResult
            {
                NodeId = _outcome.NodeId,
                EventId = EventId,
                Option = _outcome.Option,
                Text = EventScreenText.ResultText(EventId, _outcome.Option, _run.Stage1Build, _outcome, curseName),
                HpBefore = hp,
                HpAfter = _run.CurrentHp,
                MaxHpBefore = maxHp,
                MaxHpAfter = _run.MaxHp,
                GoldBefore = gold,
                GoldAfter = _run.Gold,
                CardsGained = cards,
                CursesGained = curses,
                RelicGained = _run.Relics.FirstOrDefault(r => !relicsBefore.Contains(r)),
                SacrificedCardId = sacrificed,
                UpgradedCardIds = upgraded,
                ForcedBattle = _outcome.ForcedBattle,
                RunOutcome = _run.Outcome
            };
            Phase = EventScreenPhase.Result;
            return true;
        }

        /// <summary>결과에서 [계속]. onComplete를 한 번만 부른다. 노드 완료는 부르는 쪽.</summary>
        public bool Continue()
        {
            if (Phase != EventScreenPhase.Result || _completed) return false;
            _completed = true;
            _onComplete?.Invoke(_result);
            return true;
        }

        List<EventCardView> SacrificeCandidates() =>
            RunDeckRules.MapSacrificeCandidates(_run, _data.Cards).Select(View).ToList();

        List<EventCardView> UpgradeCandidates()
        {
            int? sacrifice = _choice.SacrificeInstanceId;
            return _run.Deck.Where(c => RunDeckRules.IsUpgradable(c, _data.Cards) && !(_outcome.NeedsSacrificeChoice && c.InstanceId == sacrifice))
                .Select(View).ToList();
        }

        static EventCardView View(RunCard c) => new EventCardView { InstanceId = c.InstanceId, CardId = c.CardId, Upgraded = c.Upgraded };

        public EventScreenSnapshot Capture()
        {
            var snap = new EventScreenSnapshot
            {
                Floor = _floor,
                NodeId = _nodeId,
                EventId = EventId,
                Title = EventScreenText.Title(EventId),
                Intro = EventScreenText.Intro(EventId),
                Hp = _run.CurrentHp,
                MaxHp = _run.MaxHp,
                Gold = _run.Gold,
                Phase = Phase,
                Result = _result,
                Options = Enumerable.Range(1, 3).Select(i => new EventOptionView
                {
                    Index = i,
                    Label = EventScreenText.OptionLabel(EventId, i, _run.Stage1Build),
                    Condition = EventScreenText.OptionCondition(EventId, i),
                    Enabled = Phase == EventScreenPhase.Choosing && EventFlow.IsEnabled(_run, _data, EventId, i)
                }).ToArray()
            };
            if (_outcome != null) snap.ChosenOption = _outcome.Option;
            if (Phase == EventScreenPhase.Picking)
            {
                snap.NeedsCard = _outcome.CardChooseOne && _outcome.CardChoices.Count > 0;
                snap.CardChoices = _outcome.CardChoices.ToArray();
                snap.SelectedCard = _selectedCard;
                snap.NeedsRelic = _outcome.RelicChoices.Count > 0;
                snap.RelicChoices = _outcome.RelicChoices.ToArray();
                snap.SelectedRelic = _selectedRelic;
                snap.NeedsSacrifice = _outcome.NeedsSacrificeChoice;
                snap.SacrificeCandidates = snap.NeedsSacrifice ? SacrificeCandidates().ToArray() : new EventCardView[0];
                snap.SelectedSacrifice = _choice.SacrificeInstanceId ?? -1;
                snap.NeedsUpgrade = _outcome.NeedsUpgradeChoice;
                snap.UpgradeCandidates = snap.NeedsUpgrade ? UpgradeCandidates().ToArray() : new EventCardView[0];
                snap.SelectedUpgrade = _choice.UpgradeInstanceId ?? -1;
                snap.CanConfirm = CanConfirm;
            }
            return snap;
        }
    }
}
