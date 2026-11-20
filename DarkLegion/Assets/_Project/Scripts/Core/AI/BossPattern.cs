using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkLegion.Core
{
    /// <summary>
    /// 보스 턴 사이클 한 줄 (`14_보스_패턴.xlsx` Boss_TurnCycle). 임포터가 Excel JSON에서 만든다.
    /// Damage 칸은 SingleAttack·LifeSteal에서는 보스 ATK와 같은 값이라(D-05) 실행은 현재 ATK로 하고,
    /// AoE에서는 고정 피해다 (`22` "광역 6").
    /// </summary>
    public sealed class BossStep
    {
        public int Phase { get; }
        public int TurnIndex { get; }
        public string Action { get; }
        public int Damage { get; }
        public string StatusEffect { get; }
        public string TargetRule { get; }
        public bool EnqueueTwice { get; }
        public string Note { get; }

        public BossStep(int phase, int turnIndex, string action, int damage, string statusEffect, string targetRule, bool enqueueTwice, string note = null)
        {
            Phase = phase;
            TurnIndex = turnIndex;
            Action = action;
            Damage = damage;
            StatusEffect = statusEffect ?? "";
            TargetRule = targetRule ?? "";
            EnqueueTwice = enqueueTwice;
            Note = note ?? "";
        }

        private static readonly Regex Number = new Regex(@"(\d+)");
        private static readonly Regex SpawnNote = new Regex(@"^([A-Z]+_\d+)\s*×\s*(\d+)");

        /// <summary>
        /// 실행용 행동으로 바꾼다. EnqueueTwice면 같은 행동 2개 (대상은 실행할 때마다 다시 고른다, `12` §6.3).
        /// 소환 Note "GHO_001 ×1"은 보스와 같은 층의 적 ID(ENE_1F_GHO_001)로 읽는다.
        /// </summary>
        public IEnumerable<EnemyActionDef> ToActions(string bossEnemyId)
        {
            var list = new List<EnemyActionDef>();
            string id = $"{bossEnemyId}:P{Phase}T{TurnIndex}:{Action}";
            switch (Action)
            {
                case "SingleAttack":
                    list.Add(new EnemyActionDef { ActionId = id, ActionType = "Attack", Value = 1, Target = TargetRule });
                    break;
                case "LifeSteal":
                {
                    var m = Number.Match(StatusEffect);
                    list.Add(new EnemyActionDef
                    {
                        ActionId = id, ActionType = "LifeSteal", Value = 1, Target = TargetRule,
                        DrainRatio = m.Success ? int.Parse(m.Value) / 100.0 : 0.5
                    });
                    break;
                }
                case "AoE":
                    list.Add(new EnemyActionDef { ActionId = id, ActionType = "AoE", Value = Damage, Target = "All" });
                    break;
                case "ApplyWeak":
                case "ApplyBurn":
                case "ApplyBind":
                case "ApplyVulnerable":
                {
                    var m = Number.Match(StatusEffect);
                    list.Add(new EnemyActionDef
                    {
                        ActionId = id, ActionType = "ApplyStatus", Status = Action.Substring("Apply".Length),
                        Duration = m.Success ? int.Parse(m.Value) : 1, Target = TargetRule
                    });
                    break;
                }
                case "Summon":
                {
                    var m = SpawnNote.Match(Note);
                    int cut = bossEnemyId.IndexOf("BOS_", System.StringComparison.Ordinal);
                    string prefix = cut >= 0 ? bossEnemyId.Substring(0, cut) : "";
                    int count = m.Success ? int.Parse(m.Groups[2].Value) : 1;
                    for (int i = 0; i < count; i++)
                    {
                        list.Add(new EnemyActionDef { ActionId = id, ActionType = "Summon", SpawnId = m.Success ? prefix + m.Groups[1].Value : "", Target = "EnemySlot" });
                    }
                    break;
                }
                default:
                    // 3~5층 전용 행동(Execute·RemoveShield·CrystalDrain·AddCurseCard·ExecuteAura 등)은 2단계에서 구현
                    list.Add(new EnemyActionDef { ActionId = id, ActionType = Action, Value = Damage, Target = TargetRule });
                    break;
            }
            if (EnqueueTwice) list.AddRange(list.ToList());
            return list;
        }
    }

    /// <summary>적 ID → 보스 턴 사이클 (`14` Boss_TurnCycle).</summary>
    public sealed class BossPatternCatalog
    {
        private readonly Dictionary<string, List<BossStep>> _steps = new Dictionary<string, List<BossStep>>();

        public void Add(string bossEnemyId, params BossStep[] steps) => _steps[bossEnemyId] = steps.ToList();

        public IReadOnlyList<BossStep> Get(string bossEnemyId) => _steps.TryGetValue(bossEnemyId, out var s) ? s : null;
    }

    /// <summary>
    /// 보스 행동 결정 (`22`, `12` §6, `01` D-28·D-65).
    /// - 페이즈: 의도를 정할 때(플레이어 턴 시작)의 HP로 판정. HP ≤ 20% → 3, ≤ 50% → 2, 그 외 1. 한 번 올라간 페이즈는 내려가지 않는다.
    /// - 새 페이즈에 들어가면 사이클은 T0부터. 사이클 길이 = 그 페이즈의 TurnIndex 최댓값 + 1.
    /// - 같은 TurnIndex 줄이 여럿이면 표 순서대로 모두 실행한다 (2층 Phase 3).
    /// - Bind·Stun이면 행동 없음, 사이클은 진행하지 않는다 (D-55 #4와 같은 규칙).
    /// </summary>
    public sealed class BossBrain
    {
        public const int Phase2HpPct = 50;
        public const int Phase3HpPct = 20;

        private readonly string _bossId;
        private readonly List<BossStep> _steps;
        private int _cycleIndex;
        private int _prevCycleIndex;

        public int Phase { get; private set; } = 1;

        public BossBrain(string bossEnemyId, IEnumerable<BossStep> steps)
        {
            _bossId = bossEnemyId;
            _steps = steps.ToList();
        }

        public static int PhaseForHp(int currentHp, int maxHp)
        {
            if (currentHp * 100 <= maxHp * Phase3HpPct) return 3;
            if (currentHp * 100 <= maxHp * Phase2HpPct) return 2;
            return 1;
        }

        /// <summary>이번 턴 행동 목록. 페이즈가 바뀌면 phaseChanged에 새 페이즈를 넣는다.</summary>
        public IReadOnlyList<EnemyActionDef> Decide(RuntimeEnemy self, out int? phaseChanged)
        {
            phaseChanged = null;
            int target = PhaseForHp(self.CurrentHP, self.MaxHP.FinalValue);
            if (target > Phase)
            {
                Phase = target;
                _cycleIndex = 0;
                phaseChanged = target;
            }
            _prevCycleIndex = _cycleIndex;
            if (!self.Statuses.CanAct) return null;

            var phaseSteps = _steps.Where(s => s.Phase == Phase).ToList();
            if (phaseSteps.Count == 0) return null;
            int length = phaseSteps.Max(s => s.TurnIndex) + 1;
            int index = _cycleIndex % length;
            _cycleIndex++;
            return phaseSteps.Where(s => s.TurnIndex == index).SelectMany(s => s.ToActions(_bossId)).ToList();
        }

        /// <summary>확정한 행동을 실행하지 못했을 때(그 사이 Bind·Stun) 사이클 위치를 결정 전으로 되돌린다.</summary>
        public void UndoLastDecision() => _cycleIndex = _prevCycleIndex;
    }
}
