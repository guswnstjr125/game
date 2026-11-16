using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    public enum PlayResult
    {
        Ok,
        InvalidPhase,
        NotInHand,
        UnknownCard,
        NotEnoughCrystal,
        InvalidTarget,
        FieldFull,
        NotImplemented
    }

    /// <summary>카드가 고르는 대상의 종류 (`10` §1 TargetRule).</summary>
    public enum CardTarget
    {
        None,
        SelectedEnemy,
        SelectedAlly,
        /// <summary>몬스터 카드: 아군 1마리를 고를 수도 있다(피의 사제). 안 고르면 null.</summary>
        OptionalAlly
    }

    /// <summary>카드 사용 요청 하나 (누가·무엇을·어디에).</summary>
    public sealed class CardPlay
    {
        public RuntimeCard Card;
        public CardDefinition Def;
        public RuntimeUnit Target;
        public SlotPosition? Position;
        /// <summary>카드를 고르는 효과(묘지 도굴, 기회 포착, 연금술)가 고른 카드.</summary>
        public RuntimeCard ChosenCard;
        public bool Upgraded => Card.Upgraded;
        public double P(string key, double fallback = 0) => Def.Param(key, Card.Upgraded, fallback);
    }

    /// <summary>카드 하나의 동작: 대상 검사와, 큐에 넣을 효과.</summary>
    public sealed class CardHandler
    {
        public CardTarget Target = CardTarget.None;
        /// <summary>대상 외의 추가 검사 (고른 카드, 칸 등). null이면 통과.</summary>
        public System.Func<BattleContext, CardPlay, bool> Validate;
        public System.Action<BattleContext, CardPlay> Enqueue;
    }

    /// <summary>
    /// 카드 사용 흐름 (`04` §4 카드 실행 흐름, `10`): Phase·손패·비용·대상을 검사하고, 통과하면 비용을 내고 카드를 옮긴 뒤 효과를 큐에 넣는다.
    /// 검사에서 거부되면 아무것도 바뀌지 않는다 (몬스터 칸 부족도 비용 미소모, `09` §3).
    /// </summary>
    public static class CardPlayer
    {
        public static PlayResult Play(BattleContext ctx, CardPlay play)
        {
            if (ctx.State != BattleState.PlayerPhase) return PlayResult.InvalidPhase;
            if (play.Card == null || play.Card.Zone != CardZone.Hand) return PlayResult.NotInHand;
            play.Def = ctx.Cards?.Get(play.Card.CardId);
            if (play.Def == null)
            {
                ctx.Log($"[Card] unknown card id {play.Card.CardId}");
                return PlayResult.UnknownCard;
            }
            int cost = play.Def.CostFor(play.Card.Upgraded);
            if (ctx.Player.CurrentCrystal < cost) return PlayResult.NotEnoughCrystal;

            if (play.Def.Type == CardType.Monster) return PlayMonster(ctx, play, cost);

            var handler = CardEffects.For(play.Def.Id);
            if (handler == null) return PlayResult.NotImplemented;
            if (!TargetOk(ctx, handler.Target, play.Target)) return PlayResult.InvalidTarget;
            if (handler.Validate != null && !handler.Validate(ctx, play)) return PlayResult.InvalidTarget;

            ctx.Player.TrySpendCrystal(cost);
            ctx.Player.Deck.MoveAfterUse(play.Card, isMonster: false);
            handler.Enqueue(ctx, play);
            return PlayResult.Ok;
        }

        private static PlayResult PlayMonster(BattleContext ctx, CardPlay play, int cost)
        {
            var handler = CardEffects.For(play.Def.Id);
            var targetRule = handler?.Target ?? CardTarget.None;
            if (play.Target != null && targetRule != CardTarget.OptionalAlly) return PlayResult.InvalidTarget;
            if (play.Target != null && !TargetOk(ctx, CardTarget.SelectedAlly, play.Target)) return PlayResult.InvalidTarget;

            var monster = MonsterFactory.FromCard(ctx, play.Def, play.Card);
            var result = ctx.Field.TrySummonAlly(monster, play.Position, ctx);
            if (result == SummonResult.RejectedFullField) return PlayResult.FieldFull;
            if (result != SummonResult.Placed) return PlayResult.InvalidTarget;

            ctx.Player.TrySpendCrystal(cost);
            ctx.Player.Deck.MoveAfterUse(play.Card, isMonster: true);
            handler?.Enqueue?.Invoke(ctx, play);
            return PlayResult.Ok;
        }

        private static bool TargetOk(BattleContext ctx, CardTarget rule, RuntimeUnit target)
        {
            switch (rule)
            {
                case CardTarget.None: return target == null;
                case CardTarget.SelectedEnemy: return target is RuntimeEnemy e && e.IsTargetable && ctx.Enemies.Contains(e);
                case CardTarget.SelectedAlly: return target is RuntimeMonster m && m.IsTargetable && ctx.Allies.Contains(m) && !m.SacrificeReserved;
                case CardTarget.OptionalAlly: return target == null || TargetOk(ctx, CardTarget.SelectedAlly, target);
                default: return false;
            }
        }
    }

    /// <summary>몬스터 카드 → 런타임 몬스터 (`08` 스탯, `09` Monster_Effects는 MonsterEffects가 구독).</summary>
    public static class MonsterFactory
    {
        private static int _seq;

        public static RuntimeMonster FromCard(BattleContext ctx, CardDefinition def, RuntimeCard card)
        {
            var m = new RuntimeMonster($"M{++_seq}", def.Id, def.Hp, def.Atk, def.Def, card)
            {
                Level = def.Level,
                SacrificeCrystalOverride = def.SacrificeCrystal
            };
            if (def.Soulbound) m.Tags.Add("Soulbound");
            return m;
        }

        public static RuntimeMonster Token(int hp, int atk, int def)
        {
            return new RuntimeMonster($"T{++_seq}", "TOKEN_SKELETON", hp, atk, def, null, isToken: true) { Level = 1 };
        }
    }
}
