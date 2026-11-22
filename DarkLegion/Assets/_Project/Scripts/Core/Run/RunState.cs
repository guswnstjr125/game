using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>노드 종류 (`03` §2). Treasure는 Mimic 폴백 전용이고 MVP 템플릿은 만들지 않는다 (`25` §4, D-73).</summary>
    public enum NodeKind
    {
        Battle,
        Elite,
        Boss,
        Event,
        Rest,
        Shop,
        Treasure
    }

    public enum RunOutcome
    {
        InProgress,
        Victory,
        Defeat,
        Forfeit
    }

    /// <summary>런 덱의 카드 개체 (`14` §2 CardSaveDTO). 개체 ID는 런 안에서 고유하고 전투에 들어가도 그대로다.</summary>
    public sealed class RunCard
    {
        public int InstanceId;
        public string CardId;
        public bool Upgraded;
        /// <summary>융합으로 만든 카드 (D-38).</summary>
        public bool FromFusion;
    }

    /// <summary>
    /// 런 상태 (`14` §2 SaveData의 런 부분). 저장(#47)은 이 필드를 DTO로 옮긴다.
    /// 위치는 Floor(1~) + StageIndex(0~8, 노드 − 1). 노드 시드는 저장하지 않고 <see cref="Seeds.NodeSeed"/>로 계산한다.
    /// </summary>
    public sealed class RunState
    {
        public string RunId;
        public long RngSeed;
        public string StartClass;
        public int Floor = 1;
        public int StageIndex;
        /// <summary>런 승리 판정 층: 1단계 빌드 2 (D-66), 최종 5.</summary>
        public int LastFloor = 2;
        public bool Stage1Build = true;
        public int CurrentHp;
        public int MaxHp;
        public int Gold;
        /// <summary>융합 충전 0~3, 층 이동 시 유지 (D-03).</summary>
        public int FusionCharge;
        public List<RunCard> Deck = new List<RunCard>();
        /// <summary>새 카드에만 발급하는 다음 개체 ID. 재사용하지 않는다 (`14` §2, D-73).</summary>
        public int NextCardInstanceId = 1;
        public List<string> Relics = new List<string>();
        public List<string> VisitedNodeIds = new List<string>();
        /// <summary>이벤트 노드 ID → 나온 이벤트 ID (D-69). 이벤트는 2-3에서 채운다.</summary>
        public Dictionary<string, string> NodeEventIds = new Dictionary<string, string>();
        public RunOutcome Outcome = RunOutcome.InProgress;
        /// <summary>새 런을 만들 때 `!Meta.TutorialDone` (`14` §2·§3). 1층 노드 1 희생 안내 전투 (D-32·D-76).</summary>
        public bool TutorialEnabled;
        /// <summary>안내 전투의 승리 또는 패배를 기록했다. 앱 종료·포기는 완료가 아니다 (`14` §3).</summary>
        public bool TutorialCompleted;

        public int Node => StageIndex + 1;
        public string CurrentNodeId => NodeId(Floor, Node);
        public int NodeSeed => Seeds.NodeSeed(RngSeed, Floor, StageIndex);

        /// <summary>노드 ID `F{층}-N{노드}` (D-73).</summary>
        public static string NodeId(int floor, int node) => $"F{floor}-N{node}";

        public RunCard AddCard(string cardId, bool upgraded = false, bool fromFusion = false)
        {
            var card = new RunCard { InstanceId = NextCardInstanceId++, CardId = cardId, Upgraded = upgraded, FromFusion = fromFusion };
            Deck.Add(card);
            return card;
        }
    }

    /// <summary>층당 9노드 단일 경로 (`03` §2, D-11). 1단계 빌드는 2층 노드 6도 이벤트 (D-34).</summary>
    public static class RunMap
    {
        public const int NodesPerFloor = 9;
        /// <summary>융합 충전 +1 노드 (D-03·D-73).</summary>
        public static bool GivesFusionCharge(int node) => node % 3 == 0;

        public static NodeKind KindOf(int floor, int node, bool stage1Build)
        {
            switch (node)
            {
                case 1:
                case 2:
                case 4:
                case 7: return NodeKind.Battle;
                case 3: return NodeKind.Event;
                case 5: return NodeKind.Elite;
                case 6: return floor == 1 || stage1Build ? NodeKind.Event : NodeKind.Shop;
                case 8: return NodeKind.Rest;
                case 9: return NodeKind.Boss;
                default: throw new System.ArgumentOutOfRangeException(nameof(node), "node is 1~9");
            }
        }

        /// <summary>데이터의 노드 종류 문자열. Mimic은 Treasure로 폴백한다 (`25` §4, PROG-04). 모르는 값은 예외.</summary>
        public static NodeKind Parse(string s)
        {
            switch (s)
            {
                case "Battle": return NodeKind.Battle;
                case "Elite": return NodeKind.Elite;
                case "Boss": return NodeKind.Boss;
                case "Event": return NodeKind.Event;
                case "Rest": return NodeKind.Rest;
                case "Shop": return NodeKind.Shop;
                case "Treasure":
                case "Mimic": return NodeKind.Treasure;
                default: throw new System.ArgumentException($"unknown node kind '{s}'");
            }
        }
    }

    /// <summary>런 진행: 새 런, 노드 완료(충전·층 이동·승리, D-03·D-66), 포기.</summary>
    public static class RunFlow
    {
        public static RunState NewRun(string runId, long rngSeed, string startClass, int maxHp, IEnumerable<string> startDeck, IEnumerable<string> startRelics, bool tutorialEnabled = false)
        {
            var run = new RunState { RunId = runId, RngSeed = rngSeed, StartClass = startClass, MaxHp = maxHp, CurrentHp = maxHp, TutorialEnabled = tutorialEnabled };
            foreach (var id in startDeck) run.AddCard(id);
            run.Relics.AddRange(startRelics);
            return run;
        }

        /// <summary>
        /// 현재 노드를 완료한다. 노드 3·6·9는 종류와 상관없이 충전 +1(최대 3). 노드 9 뒤 다음 층 노드 1, 마지막 층이면 승리 (D-66).
        /// 보상 확정(2-2)·이벤트(2-3)는 이 호출 전에 끝낸다.
        /// </summary>
        public static void CompleteNode(RunState run)
        {
            if (run.Outcome != RunOutcome.InProgress) throw new System.InvalidOperationException($"run is over ({run.Outcome})");
            run.VisitedNodeIds.Add(run.CurrentNodeId);
            if (RunMap.GivesFusionCharge(run.Node)) run.FusionCharge = System.Math.Min(FusionSystem.MaxCharge, run.FusionCharge + 1);
            if (run.Node < RunMap.NodesPerFloor)
            {
                run.StageIndex++;
                return;
            }
            if (run.Floor >= run.LastFloor)
            {
                run.Outcome = RunOutcome.Victory;
                return;
            }
            run.Floor++;
            run.StageIndex = 0;
        }

        public static void Forfeit(RunState run)
        {
            if (run.Outcome == RunOutcome.InProgress) run.Outcome = RunOutcome.Forfeit;
        }
    }

    /// <summary>
    /// 런 → 전투, 전투 → 런 (`14` §2·§3, D-61·D-72·D-73).
    /// </summary>
    public static class RunBattle
    {
        /// <summary>
        /// 현재 노드의 전투를 만든다. 노드 시드의 전투 난수에서 먼저 엘리트 A/B, 그다음 Wave 2를 굴리고(D-73) 같은 난수로 덱을 섞는다.
        /// 적은 Enemies_Master × 스테이지 인덱스 스케일, 보스는 그대로 (`16` §2).
        /// </summary>
        public static BattleContext Start(RunState run, GameDataLoadResult data)
        {
            var ctx = NewContext(run, data);
            var variants = data.Encounters.Variants(run.Floor, run.Node);
            if (variants.Count == 0) throw new System.InvalidOperationException($"no encounter for {run.CurrentNodeId}");
            var enc = variants.Count > 1 && ctx.Random.Chance(0.5) ? variants[1] : variants[0];
            bool wave2 = enc.Waves.Count > 1 && ctx.Random.Chance(0.5);
            ctx.IsTutorial = IsTutorialBattle(run);
            AttachPlayer(run, data, ctx, ctx.IsTutorial);
            EncounterFactory.Spawn(ctx, enc, wave2, run.StageIndex);
            return ctx;
        }

        /// <summary>
        /// 영혼 감옥 강제 전투 (D-68): 그 층 노드 1 A/Wave 1 편성, 이벤트 위치의 스테이지 인덱스·노드 시드. 튜토리얼 없음.
        /// 승리 뒤 RewardFlow.ForBattle(run, NodeKind.Battle, …)로 일반 전투 보상 1회 (D-75 보상 난수).
        /// </summary>
        public static BattleContext StartForced(RunState run, GameDataLoadResult data)
        {
            var ctx = NewContext(run, data);
            var enc = data.Encounters.Variants(run.Floor, 1).FirstOrDefault(e => e.Variant == "A")
                ?? throw new System.InvalidOperationException($"no F{run.Floor}-N1 A encounter");
            AttachPlayer(run, data, ctx);
            EncounterFactory.Spawn(ctx, enc, false, run.StageIndex);
            return ctx;
        }

        /// <summary>D-32 첫 턴 고정 손패: 파편·뼈 병사·굶주린 구울·영혼 추출·피의 의식.</summary>
        public static readonly IReadOnlyList<string> TutorialHand = new[] { "NEU_MON_001", "NEC_MON_001", "NEC_MON_002", "NEC_SKL_001", "NEC_ACT_001" };

        /// <summary>이 노드가 희생 안내 전투인가: 1층 노드 1, 안내 켜짐, 아직 완료 아님 (D-32·D-76).</summary>
        public static bool IsTutorialBattle(RunState run) => run.TutorialEnabled && !run.TutorialCompleted && run.Floor == 1 && run.Node == 1;

        private static BattleContext NewContext(RunState run, GameDataLoadResult data)
        {
            return new BattleContext(run.NodeSeed)
            {
                Cards = data.Cards,
                EnemyKits = data.EnemyKits,
                BossPatterns = data.BossPatterns,
                Fusion = data.Fusion
            };
        }

        private static void AttachPlayer(RunState run, GameDataLoadResult data, BattleContext ctx, bool tutorial = false)
        {
            var cards = run.Deck.Select(c =>
            {
                var rc = data.Cards.Get(c.CardId).CreateCard(c.InstanceId, c.Upgraded);
                rc.FromFusion = c.FromFusion;
                return rc;
            }).ToList();
            // 안내 전투: 고정 5장은 그 카드 ID의 첫 개체, 나머지는 섞는다. 고정 5장을 뽑을 더미 맨 위에 둬 첫 턴 드로우가 그 5장이다 (D-76)
            var fixedHand = new List<RuntimeCard>();
            if (tutorial)
            {
                foreach (var id in TutorialHand)
                {
                    var first = cards.Where(c => c.CardId == id && !fixedHand.Contains(c)).OrderBy(c => c.InstanceId).FirstOrDefault();
                    if (first != null) fixedHand.Add(first);
                }
            }
            var deck = new RuntimeDeck(cards.Where(c => !fixedHand.Contains(c)), ctx.Random) { MinNextInstanceId = run.NextCardInstanceId };
            deck.PlaceOnTop(fixedHand);
            ctx.Player = new RuntimePlayer("P", run.MaxHp, deck)
            {
                CurrentHP = run.CurrentHp,
                FusionCharge = run.FusionCharge
            };
            ctx.Relics.AddRange(run.Relics);
            RelicEffects.Attach(ctx); // 플레이어를 넣은 뒤 (낡은 뼈조각이 Crystal 사용을 구독)
        }

        /// <summary>
        /// 전투 결과를 런에 반영한다. HP·충전은 항상, 융합 결과는 승리일 때만 개체 ID로 (D-73). 패배면 런 패배.
        /// 전투 중 소멸·버림은 런 덱에 반영하지 않는다 (`14` §2).
        /// </summary>
        public static void Finish(RunState run, BattleContext ctx)
        {
            run.CurrentHp = System.Math.Max(0, ctx.Player.CurrentHP);
            run.FusionCharge = ctx.Player.FusionCharge;
            if (ctx.IsTutorial && ctx.Outcome != BattleOutcome.None) run.TutorialCompleted = true; // 승리·패배 기록 (`14` §3)
            if (ctx.Outcome == BattleOutcome.Defeat)
            {
                run.Outcome = RunOutcome.Defeat;
                return;
            }
            if (ctx.Outcome != BattleOutcome.Victory) throw new System.InvalidOperationException("battle is not over");
            foreach (var f in ctx.FusionHistory)
            {
                run.Deck.RemoveAll(c => c.InstanceId == f.MaterialAInstance || c.InstanceId == f.MaterialBInstance);
                run.Deck.Add(new RunCard { InstanceId = f.ResultInstance, CardId = f.Result, FromFusion = true });
                run.NextCardInstanceId = System.Math.Max(run.NextCardInstanceId, f.ResultInstance + 1);
            }
        }
    }

    /// <summary>Encounters 편성 → 적 배치 (D-61·D-72). 첫 Wave는 칸에, 다음 Wave는 예약.</summary>
    public static class EncounterFactory
    {
        public static void Spawn(BattleContext ctx, Encounter enc, bool includeWave2, int stageIndex)
        {
            bool boss = RunMap.Parse(enc.NodeType) == NodeKind.Boss;
            int id = 0;
            bool first = true;
            foreach (var kv in enc.Waves)
            {
                if (kv.Key >= 2 && !includeWave2) continue;
                var enemies = new List<RuntimeEnemy>();
                foreach (var (eid, count) in kv.Value)
                {
                    var stats = ctx.EnemyKits?.SpawnStats(eid) ?? throw new System.InvalidOperationException($"no Enemies_Master stats for {eid}");
                    var (hp, atk, def) = EnemyScaler.Scale(stats.hp, stats.atk, stats.def, stageIndex, boss);
                    for (int i = 0; i < count; i++) enemies.Add(new RuntimeEnemy($"E{++id}", eid, hp, atk, def, boss));
                }
                if (first)
                {
                    foreach (var e in enemies) ctx.Field.PlaceEnemy(e, null, ctx);
                    first = false;
                }
                else ctx.QueueWave(enemies.ToArray());
            }
        }
    }
}
