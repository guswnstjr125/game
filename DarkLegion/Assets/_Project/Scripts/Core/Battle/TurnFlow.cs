using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>
    /// 한 턴 진행 (`06` §13.1): 플레이어 턴 시작(Crystal·드로우·의도 확정) → Main·Command → Resolve(방어 먼저, 공격 Front→Middle→Back) → Enemy Phase(확정된 행동) → 턴 종료.
    /// D-27 커맨드, D-28 의도, D-29 피격, D-02 소환 턴 행동.
    /// </summary>
    public static class TurnFlow
    {
        public const int DefendShieldBase = 3;

        public static void StartPlayerTurn(BattleContext ctx)
        {
            ctx.BeginTurn();
            ctx.Player.ResetCrystalForTurn();
            ctx.Player.Deck.Draw(RuntimeDeck.TurnStartDraw);
            foreach (var a in ctx.Allies)
            {
                a.Command = UnitCommand.Attack;
                a.AttackTarget = null;
            }
            ctx.Intents.Clear();
            foreach (var e in ctx.Enemies.Where(e => !e.IsDead).ToList())
            {
                var brain = ctx.BrainOf(e);
                var pick = brain?.Decide(e, ctx);
                if (pick?.Tag is EnemyActionDef d) ctx.Intents[e] = d;
            }
        }

        public static BattleOutcome EndPlayerTurn(BattleContext ctx)
        {
            ctx.State = BattleState.Resolving;
            var order = Enumerable.Range(0, BattleField.AllySlotCount)
                .Select(i => ctx.Field.At(TeamType.Player, (SlotPosition)i) as RuntimeMonster)
                .Where(m => m != null && !m.IsDead).ToList();

            // 방어는 Resolve 시작에 먼저 (D-27)
            foreach (var m in order.Where(m => m.Command == UnitCommand.Defend && m.Statuses.CanAct))
            {
                m.Statuses.Apply(StatusType.Shield, DefendShieldBase + m.EffectiveDefense, ctx);
            }
            foreach (var m in order.Where(m => m.Command == UnitCommand.Attack))
            {
                for (int i = 0; i <= m.ExtraActionsThisTurn; i++)
                {
                    if (m.IsDead || !m.Statuses.CanAct) break;
                    var target = m.AttackTarget != null && m.AttackTarget.IsTargetable ? m.AttackTarget : DefaultAllyTarget(ctx);
                    if (target == null) break;
                    ctx.Queue.EnqueueMain(new DamageAction { Source = m, Target = target, BaseDamage = m.Attack.FinalValue });
                    ctx.Queue.Process();
                }
            }
            if (ctx.EvaluateOutcome() != BattleOutcome.None) return ctx.Outcome;

            ctx.State = BattleState.EnemyPhase;
            foreach (var e in EnemyOrder(ctx))
            {
                if (e.IsDead || !ctx.Intents.TryGetValue(e, out var d)) continue;
                if (!e.Statuses.CanAct) continue;
                EnemyAi.Execute(ctx, e, d);
                ctx.Queue.Process();
                if (ctx.EvaluateOutcome() != BattleOutcome.None) return ctx.Outcome;
            }
            return ctx.EndTurn();
        }

        /// <summary>아군 공격의 기본 대상: Taunt 적, 없으면 적 앞줄 (D-27).</summary>
        public static RuntimeEnemy DefaultAllyTarget(BattleContext ctx)
        {
            var taunt = ctx.Enemies.FirstOrDefault(e => e.IsTargetable && e.Statuses.Has(StatusType.Taunt));
            return taunt ?? ctx.Field.FrontLine(TeamType.Enemy) as RuntimeEnemy;
        }

        private static List<RuntimeEnemy> EnemyOrder(BattleContext ctx)
        {
            return Enumerable.Range(0, BattleField.EnemySlotCount)
                .Select(i => ctx.Field.At(TeamType.Enemy, (SlotPosition)i) as RuntimeEnemy)
                .Where(e => e != null).ToList();
        }
    }
}
