using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkLegion.Core
{
    /// <summary>적 의도 아이콘 6종 (D-28·D-48·D-52). None = 이번 턴 행동 없음.</summary>
    public enum IntentKind
    {
        None,
        SingleAttack,
        AoE,
        Buff,
        Debuff,
        Summon,
        Special
    }

    /// <summary>
    /// 적 행동 한 줄 (`10` Enemy_Actions, D-61). 임포터가 Excel JSON에서 만든다.
    /// ApplyStatus의 수치(스택·턴)는 Duration 칸, Summon의 소환 적 ID는 SpawnId.
    /// </summary>
    public sealed class EnemyActionDef
    {
        public string ActionId;
        public string Condition = "";
        public double Chance = 1;
        public string ActionType;
        public double Value;
        public string Status = "";
        public int Duration;
        public string Target;
        public string SpawnId;
        /// <summary>흡혈 회복 비율 (Note "피해의 N% 회복"). 기본 0.5.</summary>
        public double DrainRatio = 0.5;
    }

    /// <summary>적 ID → 행동표, 소환용 기본 스탯.</summary>
    public sealed class EnemyKitCatalog
    {
        private readonly Dictionary<string, List<EnemyActionDef>> _kits = new Dictionary<string, List<EnemyActionDef>>();
        private readonly Dictionary<string, (int hp, int atk, int def)> _spawn = new Dictionary<string, (int, int, int)>();

        public void Add(string enemyId, params EnemyActionDef[] actions)
        {
            foreach (var a in actions)
            {
                if (a.ActionType == "Summon" && string.IsNullOrEmpty(a.SpawnId)) a.SpawnId = a.Status;
            }
            _kits[enemyId] = actions.ToList();
        }

        public IReadOnlyList<EnemyActionDef> Get(string enemyId) => _kits.TryGetValue(enemyId, out var k) ? k : null;

        public void SetSpawnStats(string enemyId, int hp, int atk, int def) => _spawn[enemyId] = (hp, atk, def);

        public (int hp, int atk, int def)? SpawnStats(string enemyId) => _spawn.TryGetValue(enemyId, out var s) ? s : ((int, int, int)?)null;
    }

    /// <summary>
    /// 적 행동표 → EnemyBrain(D-55) 연결, 조건 판정, 의도 분류, 실행.
    /// 대상은 실행 시점에 고른다 (D-28). 아군이 Taunt면 단일 대상 행동은 그 아군 (`11` Taunt).
    /// 단일 공격은 아군이 하나도 없을 때만 플레이어, 광역은 아군 전체 + 플레이어 (D-29).
    /// </summary>
    public static class EnemyAi
    {
        public static EnemyBrain BuildBrain(IReadOnlyList<EnemyActionDef> defs)
        {
            return new EnemyBrain(defs.Select(d => new EnemyActionOption(
                d.ActionId,
                string.IsNullOrEmpty(d.Condition) ? null : (System.Func<RuntimeEnemy, BattleContext, bool>)((self, ctx) => Check(d.Condition, self, ctx)),
                d.Chance) { Tag = d }));
        }

        public static IntentKind KindOf(EnemyActionDef d)
        {
            switch (d.ActionType)
            {
                case "Attack":
                case "Attack+Status":
                case "Execute":
                    return IntentKind.SingleAttack;
                case "AoE":
                case "AoE+SelfDeath":
                    return IntentKind.AoE;
                case "LifeSteal":
                    return IntentKind.Special; // D-48
                case "GainShield":
                case "ModifyDef":
                case "GainCounter":
                    return IntentKind.Buff;
                case "ApplyStatus":
                    return d.Target == "Self" ? IntentKind.Buff : IntentKind.Debuff;
                case "CrystalDrain":
                    return IntentKind.Debuff; // D-48
                case "Summon":
                    return IntentKind.Summon;
                default:
                    return IntentKind.Special;
            }
        }

        // ---------- 조건 (`21` §1.1, Enemy_Actions Condition) ----------

        private static readonly Regex Cmp = new Regex(@"^Self\.(HpPct|Hp|SummonCount)(<=|>=|<|>)(\d+)$");
        private static readonly Regex AnyNo = new Regex(@"^AnyTarget\.NoStatus\((\w+)\)$");
        private static readonly Regex AnyAtLeast = new Regex(@"^AnyTarget\.Status\((\w+)\)>=(\d+)$");
        private static readonly Regex SelfNo = new Regex(@"^Self\.NoStatus\((\w+)\)$");

        public static bool Check(string cond, RuntimeEnemy self, BattleContext ctx)
        {
            Match m;
            if ((m = Cmp.Match(cond)).Success)
            {
                double v;
                switch (m.Groups[1].Value)
                {
                    case "HpPct": v = 100.0 * self.CurrentHP / self.MaxHP.FinalValue; break;
                    case "Hp": v = self.CurrentHP; break;
                    default: v = ctx.Enemies.Count(e => !e.IsDead && e.SummonedBy == self); break;
                }
                double n = double.Parse(m.Groups[3].Value);
                switch (m.Groups[2].Value)
                {
                    case "<": return v < n;
                    case "<=": return v <= n;
                    case ">": return v > n;
                    default: return v >= n;
                }
            }
            if ((m = AnyNo.Match(cond)).Success) return PlayerSide(ctx).Any(u => !u.Statuses.Has(Parse(m.Groups[1].Value)));
            if ((m = AnyAtLeast.Match(cond)).Success) return PlayerSide(ctx).Any(u => u.Statuses.Stacks(Parse(m.Groups[1].Value)) >= int.Parse(m.Groups[2].Value));
            if ((m = SelfNo.Match(cond)).Success) return !self.Statuses.Has(Parse(m.Groups[1].Value));
            if (cond == "Self.NoShield") return self.Shield == 0;
            ctx.Log($"[AI] unknown condition '{cond}' → false");
            return false;
        }

        private static StatusType Parse(string s) => (StatusType)System.Enum.Parse(typeof(StatusType), s);

        /// <summary>플레이어 편의 대상 후보: 살아 있는 아군 몬스터, 없으면 플레이어.</summary>
        private static List<RuntimeUnit> PlayerSide(BattleContext ctx)
        {
            var allies = ctx.Allies.Where(a => a.IsTargetable).Cast<RuntimeUnit>().ToList();
            if (allies.Count == 0 && ctx.Player != null) allies.Add(ctx.Player);
            return allies;
        }

        // ---------- 대상 ----------

        public static RuntimeUnit SingleTarget(BattleContext ctx, string rule)
        {
            var allies = ctx.Allies.Where(a => a.IsTargetable).ToList();
            if (allies.Count == 0) return ctx.Player;
            var taunt = allies.FirstOrDefault(a => a.Statuses.Has(StatusType.Taunt));
            if (taunt != null) return taunt;
            switch (rule)
            {
                case "LowestHp":
                case "LowestHpPct":
                    return allies.OrderBy(a => rule == "LowestHp" ? a.CurrentHP : (double)a.CurrentHP / a.MaxHP.FinalValue)
                                 .ThenBy(a => (int?)ctx.Field.PositionOf(a) ?? 9).First();
                case "Back":
                    for (int i = BattleField.AllySlotCount - 1; i >= 0; i--)
                    {
                        var u = ctx.Field.At(TeamType.Player, (SlotPosition)i);
                        if (u != null && u.IsTargetable) return u;
                    }
                    return ctx.Field.FrontLine(TeamType.Player) ?? ctx.Player;
                default:
                    return ctx.Field.FrontLine(TeamType.Player) ?? ctx.Player;
            }
        }

        private static List<RuntimeUnit> AllTargets(BattleContext ctx)
        {
            var list = ctx.Allies.Where(a => a.IsTargetable).Cast<RuntimeUnit>().ToList();
            if (ctx.Player != null) list.Add(ctx.Player);
            return list;
        }

        // ---------- 실행 ----------

        /// <summary>확정된 행동을 실행한다 (Enemy Phase). 효과는 큐에 넣고 처리는 호출하는 쪽.</summary>
        public static void Execute(BattleContext ctx, RuntimeEnemy self, EnemyActionDef d)
        {
            switch (d.ActionType)
            {
                case "Attack":
                    Hit(ctx, self, SingleTarget(ctx, d.Target), d.Value);
                    break;
                case "Attack+Status":
                {
                    var t = SingleTarget(ctx, d.Target);
                    Hit(ctx, self, t, d.Value);
                    ctx.Queue.EnqueueMain(Fx("EnemyStatus", x => t.Statuses.Apply(Parse(d.Status), d.Duration, x)));
                    break;
                }
                case "AoE":
                case "AoE+SelfDeath":
                    foreach (var t in AllTargets(ctx))
                    {
                        ctx.Queue.EnqueueMain(new DamageAction { Source = self, Target = t, BaseDamage = (int)d.Value });
                    }
                    if (d.ActionType == "AoE+SelfDeath")
                    {
                        ctx.Queue.EnqueueMain(Fx("SelfDestruct", x =>
                        {
                            if (self.MarkDead()) x.Queue.EnqueueDeath(new DeathAction { Unit = self });
                        }));
                    }
                    break;
                case "ApplyStatus":
                {
                    var targets = d.Target == "Self" ? new List<RuntimeUnit> { self }
                        : d.Target == "All" ? AllTargets(ctx)
                        : new List<RuntimeUnit> { SingleTarget(ctx, d.Target) };
                    int amount = d.Duration > 0 ? d.Duration : 1;
                    ctx.Queue.EnqueueMain(Fx("EnemyApplyStatus", x =>
                    {
                        foreach (var t in targets) t.Statuses.Apply(Parse(d.Status), amount, x);
                    }));
                    break;
                }
                case "GainShield":
                    ctx.Queue.EnqueueMain(Fx("EnemyShield", x => self.Statuses.Apply(StatusType.Shield, (int)d.Value, x)));
                    break;
                case "LifeSteal":
                {
                    var t = SingleTarget(ctx, d.Target);
                    var hit = Hit(ctx, self, t, d.Value);
                    ctx.Queue.EnqueueMain(Fx("EnemyDrain", x => x.Heal(self, (int)System.Math.Floor(hit.LastDealt * d.DrainRatio))));
                    break;
                }
                case "Summon":
                    ctx.Queue.EnqueueMain(Fx("EnemySummon", x =>
                    {
                        var stats = x.EnemyKits?.SpawnStats(d.SpawnId);
                        if (stats == null) return;
                        var e = new RuntimeEnemy($"S{++_seq}", d.SpawnId, stats.Value.hp, stats.Value.atk, stats.Value.def, false) { SummonedBy = self };
                        x.Field.PlaceEnemy(e, null, x);
                    }));
                    break;
                default:
                    ctx.Log($"[AI] action type '{d.ActionType}' not implemented yet ({self.DataId})");
                    break;
            }
        }

        private static int _seq;

        private static DamageAction Hit(BattleContext ctx, RuntimeEnemy self, RuntimeUnit target, double multiplier)
        {
            var hit = new DamageAction { Source = self, Target = target, BaseDamage = self.Attack.FinalValue, Multiplier = multiplier };
            ctx.Queue.EnqueueMain(hit);
            return hit;
        }

        private static EffectAction Fx(string label, System.Action<BattleContext> run) => new EffectAction(BattleActionType.Trigger, label, run);
    }
}
