using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    public enum MapNodeState
    {
        Visited,
        Current,
        Upcoming
    }

    public sealed class MapNodeView
    {
        public int Floor;
        public int Node;
        public string Id;
        public NodeKind Kind;
        public string KindLabel;
        public MapNodeState State;
        /// <summary>현재 노드이고 이 화면에서 들어갈 수 있는 전투 노드(전투·엘리트·보스)다.</summary>
        public bool Enterable;
    }

    public sealed class RunMapSnapshot
    {
        public int Floor;
        public int Node;
        public int Hp;
        public int MaxHp;
        public int Gold;
        public int FusionCharge;
        public int DeckCount;
        public string[] Relics = new string[0];
        public RunOutcome Outcome;
        public MapNodeView[] Nodes = new MapNodeView[0];
    }

    public sealed class BattleResultView
    {
        public BattleOutcome Battle;
        public RunOutcome Run;
        public string NodeId;
        public NodeKind Kind;
        public int HpBefore;
        public int HpAfter;
        public int MaxHp;
        /// <summary>승리 보상 골드 (`RewardFlow`, D-75). 패배면 0.</summary>
        public int RewardGold;
        /// <summary>엘리트 보상은 유물을 골라야 확정된다. 그 화면이 없어 이 결과에서는 계속할 수 없다.</summary>
        public bool NeedsRelicChoice;
        public bool CanContinue;
    }

    /// <summary>
    /// 18노드 맵 → 전투 → 결과 → 맵 (`37` §1·§2, D-37·D-66·D-67). 규칙은 <see cref="RunFlow"/>·<see cref="RunBattle"/>·
    /// <see cref="RewardFlow"/>를 그대로 부른다. 이 화면에는 카드·유물 선택 UI가 없어, 승리하면 보상 골드만 받고
    /// 카드는 건너뛴다 (D-37 건너뛰기). 이벤트·휴식·상점 노드는 화면이 생길 때까지 들어갈 수 없다.
    /// </summary>
    public sealed class RunMapSession
    {
        readonly GameDataLoadResult _data;
        BattleScreenSession _active;
        int _hpBefore;
        NodeKind _activeKind;
        string _activeNodeId;
        PendingReward _reward;

        public RunState Run { get; private set; }

        public RunMapSession(RunState run, GameDataLoadResult data)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>타이틀이 없을 때의 새 NEC 런: HP 80, 시작 덱 10장, 피의 잔 (`18` §2).</summary>
        public static RunMapSession NewNecRun(GameDataLoadResult data, long seed, bool tutorialEnabled)
        {
            return new RunMapSession(NewRun(seed, tutorialEnabled), data);
        }

        static RunState NewRun(long seed, bool tutorialEnabled) =>
            RunFlow.NewRun("RUN-1", seed, "NEC", BattleScreenSession.StartHp, BattleScreenSession.NecStartDeck,
                new[] { BattleScreenSession.StartRelicId }, tutorialEnabled);

        public void Restart(long seed)
        {
            bool tutorial = Run.TutorialEnabled;
            Run = NewRun(seed, tutorial);
            _active = null;
            _reward = null;
        }

        public static bool IsBattleKind(NodeKind kind) => kind == NodeKind.Battle || kind == NodeKind.Elite || kind == NodeKind.Boss;

        public static string KindLabel(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return "전투";
                case NodeKind.Elite: return "엘리트";
                case NodeKind.Boss: return "보스";
                case NodeKind.Event: return "이벤트";
                case NodeKind.Rest: return "휴식";
                case NodeKind.Shop: return "상점";
                default: return "보물";
            }
        }

        bool CanEnterCurrent => Run.Outcome == RunOutcome.InProgress && _active == null
            && IsBattleKind(RunMap.KindOf(Run.Floor, Run.Node, Run.Stage1Build));

        public bool CanEnter(int floor, int node) => CanEnterCurrent && floor == Run.Floor && node == Run.Node;

        public RunMapSnapshot Capture()
        {
            var snap = new RunMapSnapshot
            {
                Floor = Run.Floor,
                Node = Run.Node,
                Hp = Run.CurrentHp,
                MaxHp = Run.MaxHp,
                Gold = Run.Gold,
                FusionCharge = Run.FusionCharge,
                DeckCount = Run.Deck.Count,
                Relics = Run.Relics.ToArray(),
                Outcome = Run.Outcome
            };
            int current = (Run.Floor - 1) * RunMap.NodesPerFloor + (Run.Node - 1);
            var nodes = new List<MapNodeView>();
            for (int f = 1; f <= Run.LastFloor; f++)
            {
                for (int n = 1; n <= RunMap.NodesPerFloor; n++)
                {
                    int index = (f - 1) * RunMap.NodesPerFloor + (n - 1);
                    var kind = RunMap.KindOf(f, n, Run.Stage1Build);
                    var state = Run.Outcome == RunOutcome.Victory || index < current ? MapNodeState.Visited
                        : index == current ? MapNodeState.Current : MapNodeState.Upcoming;
                    nodes.Add(new MapNodeView
                    {
                        Floor = f,
                        Node = n,
                        Id = RunState.NodeId(f, n),
                        Kind = kind,
                        KindLabel = KindLabel(kind),
                        State = state,
                        Enterable = state == MapNodeState.Current && CanEnterCurrent
                    });
                }
            }
            snap.Nodes = nodes.ToArray();
            return snap;
        }

        /// <summary>현재 노드의 전투를 시작한다. 전투 화면은 이 세션을 그대로 쓴다.</summary>
        public BattleScreenSession EnterBattle()
        {
            if (!CanEnterCurrent) throw new InvalidOperationException($"{Run.CurrentNodeId}에는 들어갈 수 없다");
            _hpBefore = Run.CurrentHp;
            _activeKind = RunMap.KindOf(Run.Floor, Run.Node, Run.Stage1Build);
            _activeNodeId = Run.CurrentNodeId;
            _reward = null;
            _active = BattleScreenSession.Start(Run, _data);
            return _active;
        }

        /// <summary>끝난 전투의 결과. 아직 안 끝났으면 예외. 노드는 <see cref="Continue"/> 전까지 넘기지 않는다.</summary>
        public BattleResultView Result()
        {
            if (_active == null) throw new InvalidOperationException("전투 중이 아니다");
            _active.Settle();
            var outcome = _active.Context.Outcome;
            if (outcome == BattleOutcome.None) throw new InvalidOperationException("전투가 아직 끝나지 않았다");
            var view = new BattleResultView
            {
                Battle = outcome,
                Run = Run.Outcome,
                NodeId = _activeNodeId,
                Kind = _activeKind,
                HpBefore = _hpBefore,
                HpAfter = Run.CurrentHp,
                MaxHp = Run.MaxHp
            };
            if (outcome == BattleOutcome.Victory)
            {
                _reward = _reward ?? RewardFlow.ForBattle(Run, _activeKind, _data);
                view.RewardGold = _reward.Gold;
                view.NeedsRelicChoice = _reward.RelicChoices.Count > 0;
                view.CanContinue = !view.NeedsRelicChoice && !_reward.Confirmed;
            }
            return view;
        }

        /// <summary>
        /// 결과 화면의 "맵으로". 승리면 골드를 받고 카드는 건너뛰어(D-37) 보상을 확정한 뒤 노드를 완료한다 (D-67).
        /// 패배이거나 유물 선택이 필요하거나 이미 처리했으면 아무것도 바꾸지 않고 false.
        /// </summary>
        public bool Continue()
        {
            if (_active == null) return false;
            var view = Result();
            if (!view.CanContinue) return false;
            if (!RewardFlow.Confirm(Run, _reward, null, null)) return false;
            RunFlow.CompleteNode(Run);
            _active = null;
            return true;
        }
    }
}
