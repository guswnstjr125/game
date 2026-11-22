using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    public enum RestScreenPhase
    {
        /// <summary>회복·강화 선택지를 보여 준다. 둘 다 안 되면 [계속]만 보인다 (D-69).</summary>
        Choosing,
        /// <summary>회복을 골랐다. 회복 후 HP 미리보기를 보여 주고 확정을 기다린다. 아직 런은 바뀌지 않았다.</summary>
        PreviewHeal,
        /// <summary>강화를 골랐다. 덱에서 1장을 고른다. 아직 런은 바뀌지 않았다.</summary>
        PickingUpgrade,
        /// <summary>확정됨. 결과를 보여 주고 [계속]을 기다린다.</summary>
        Result
    }

    public sealed class RestCardView
    {
        public int InstanceId;
        public string CardId;
        public string Name;
    }

    /// <summary>휴식 확정 결과. 맵은 이 값을 받은 뒤 노드를 완료한다 (`37` §2.4).</summary>
    public sealed class RestScreenResult
    {
        public string NodeId;
        public bool Healed;
        public bool Upgraded;
        /// <summary>선택지가 없어 [계속]만 눌렀다 (D-69).</summary>
        public bool ContinuedOnly;
        public int HpBefore;
        public int HpAfter;
        public int MaxHp;
        public int UpgradedInstanceId;
        public string UpgradedCardId;
    }

    public sealed class RestScreenSnapshot
    {
        public string NodeId;
        public int Hp;
        public int MaxHp;
        public RestScreenPhase Phase;
        public bool CanHeal;
        public int HealAmount;
        /// <summary>회복하면 되는 HP: min(MaxHp, Hp + HealAmount).</summary>
        public int HealPreviewHp;
        public bool CanUpgrade;
        public bool ContinueOnly;
        public RestCardView[] UpgradeCandidates = new RestCardView[0];
        public int SelectedUpgrade = -1;
        public bool CanConfirm;
        public RestScreenResult Result;
    }

    /// <summary>
    /// 휴식 화면 (`37` §2.4·§2.9, D-31·D-69, `32` REST-01). 규칙은 <see cref="RestFlow"/>를 그대로 부르고 새 규칙은 두지 않는다.
    /// 선택 → (강화는 카드 고르기) → 확정 → 결과 → [계속]. 확정 전에는 런이 바뀌지 않고, 회복·강화·계속은 한 번만 적용된다.
    /// 노드 완료(<see cref="RunFlow.CompleteNode"/>)는 [계속]에서 부르는 <c>onComplete</c>를 받은 맵이 한다.
    /// </summary>
    public sealed class RestScreenSession
    {
        readonly RunState _run;
        readonly GameDataLoadResult _data;
        readonly Action<RestScreenResult> _onComplete;
        readonly Func<string, string> _nameOf;
        readonly string _nodeId;
        int _selectedUpgrade = -1;
        bool _completed;

        public RestScreenPhase Phase { get; private set; } = RestScreenPhase.Choosing;
        public RestScreenResult Result { get; private set; }
        public bool Completed => _completed;

        /// <param name="run">현재 노드가 휴식 노드인 런. 확정 때 이 객체를 바꾼다.</param>
        /// <param name="onComplete">결과에서 [계속]을 누르면 한 번 부른다.</param>
        /// <param name="nameOf">카드 ID → 표시 이름. 없으면 ID를 그대로 쓴다.</param>
        public RestScreenSession(RunState run, GameDataLoadResult data, Action<RestScreenResult> onComplete = null, Func<string, string> nameOf = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (run.Outcome != RunOutcome.InProgress) throw new ArgumentException("끝난 런이다", nameof(run));
            if (RunMap.KindOf(run.Floor, run.Node, run.Stage1Build) != NodeKind.Rest)
                throw new ArgumentException(run.CurrentNodeId + "은 휴식 노드가 아니다", nameof(run));
            _nodeId = run.CurrentNodeId;
            _onComplete = onComplete;
            _nameOf = nameOf ?? (id => id);
        }

        RestOptions Options => RestFlow.Options(_run, _data);

        List<RunCard> Candidates() => _run.Deck.Where(c => RunDeckRules.IsUpgradable(c, _data.Cards)).ToList();

        public bool ChooseHeal()
        {
            if (Phase != RestScreenPhase.Choosing || !Options.CanHeal) return false;
            Phase = RestScreenPhase.PreviewHeal;
            return true;
        }

        public bool ChooseUpgrade()
        {
            if (Phase != RestScreenPhase.Choosing || !Options.CanUpgrade) return false;
            _selectedUpgrade = -1;
            Phase = RestScreenPhase.PickingUpgrade;
            return true;
        }

        /// <summary>강화할 카드 개체를 고른다. 같은 카드 ID가 여러 장이어도 고른 개체만 대상이다.</summary>
        public bool SelectUpgrade(int instanceId)
        {
            if (Phase != RestScreenPhase.PickingUpgrade) return false;
            if (!Candidates().Any(c => c.InstanceId == instanceId)) return false;
            _selectedUpgrade = instanceId;
            return true;
        }

        /// <summary>선택을 취소하고 선택지로 돌아간다. 런은 바뀌지 않았다.</summary>
        public bool Back()
        {
            if (Phase != RestScreenPhase.PreviewHeal && Phase != RestScreenPhase.PickingUpgrade) return false;
            _selectedUpgrade = -1;
            Phase = RestScreenPhase.Choosing;
            return true;
        }

        bool CanConfirm => Phase == RestScreenPhase.PreviewHeal || (Phase == RestScreenPhase.PickingUpgrade && _selectedUpgrade >= 0);

        /// <summary>마지막 확인. <see cref="RestFlow"/>가 성공할 때만 런을 바꾸고 결과로 간다.</summary>
        public bool Confirm()
        {
            if (!CanConfirm) return false;
            int before = _run.CurrentHp;
            var result = new RestScreenResult { NodeId = _nodeId, HpBefore = before, MaxHp = _run.MaxHp };
            if (Phase == RestScreenPhase.PreviewHeal)
            {
                if (!RestFlow.Heal(_run, _data)) return false;
                result.Healed = true;
            }
            else
            {
                var card = _run.Deck.First(c => c.InstanceId == _selectedUpgrade);
                if (!RestFlow.Upgrade(_run, _data, _selectedUpgrade)) return false;
                result.Upgraded = true;
                result.UpgradedInstanceId = card.InstanceId;
                result.UpgradedCardId = card.CardId;
            }
            result.HpAfter = _run.CurrentHp;
            Result = result;
            Phase = RestScreenPhase.Result;
            _selectedUpgrade = -1;
            return true;
        }

        /// <summary>
        /// [계속]. 결과에서, 또는 회복·강화가 모두 안 될 때 선택지에서 누른다 (D-69). onComplete를 한 번만 부른다.
        /// </summary>
        public bool Continue()
        {
            if (_completed) return false;
            if (Phase == RestScreenPhase.Choosing && Options.ContinueOnly)
            {
                Result = new RestScreenResult { NodeId = _nodeId, ContinuedOnly = true, HpBefore = _run.CurrentHp, HpAfter = _run.CurrentHp, MaxHp = _run.MaxHp };
                Phase = RestScreenPhase.Result;
            }
            else if (Phase != RestScreenPhase.Result)
            {
                return false;
            }
            _completed = true;
            _onComplete?.Invoke(Result);
            return true;
        }

        public RestScreenSnapshot Capture()
        {
            var o = Options;
            var snap = new RestScreenSnapshot
            {
                NodeId = _nodeId,
                Hp = _run.CurrentHp,
                MaxHp = _run.MaxHp,
                Phase = Phase,
                CanHeal = o.CanHeal,
                HealAmount = o.HealAmount,
                HealPreviewHp = Math.Min(_run.MaxHp, _run.CurrentHp + o.HealAmount),
                CanUpgrade = o.CanUpgrade,
                ContinueOnly = o.ContinueOnly && Phase == RestScreenPhase.Choosing,
                SelectedUpgrade = _selectedUpgrade,
                CanConfirm = CanConfirm,
                Result = Result
            };
            if (Phase == RestScreenPhase.PickingUpgrade)
                snap.UpgradeCandidates = Candidates().Select(c => new RestCardView { InstanceId = c.InstanceId, CardId = c.CardId, Name = _nameOf(c.CardId) }).ToArray();
            return snap;
        }
    }
}
