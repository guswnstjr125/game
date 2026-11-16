using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>
    /// 1단계 스킬·액션 12장과 몬스터 카드 사용 시 효과 (`10` §3·§4, `08` Cards_Effect, `01` D-20·D-24·D-42·D-63).
    /// 효과는 EffectAction으로 큐에 넣는다. 수치는 CardDefinition.Params에서 읽는다 (Excel이 정본).
    /// </summary>
    public static class CardEffects
    {
        private static readonly Dictionary<string, CardHandler> Handlers = new Dictionary<string, CardHandler>
        {
            // ---- 스킬 ----
            ["NEC_SKL_001"] = new CardHandler
            {
                // 영혼 추출: 지정 아군이 이번 턴 죽으면(희생 포함) Draw (D-42)
                Target = CardTarget.SelectedAlly,
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("SoulExtract", x => x.WatchDeathDraw(p.Target, (int)p.P("DeathThisTurnDraw", 2))))
            },
            ["NEC_SKL_002"] = new CardHandler
            {
                // 어둠의 방벽: 아군 전체 Shield 3, Front 아군 +2
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("DarkBarrier", x =>
                {
                    foreach (var a in x.Allies.Where(a => !a.IsDead))
                    {
                        a.Statuses.Apply(StatusType.Shield, (int)p.P("Shield", 3), x);
                        if (x.Field.PositionOf(a) == SlotPosition.Front) a.Statuses.Apply(StatusType.Shield, (int)p.P("FrontBonus", 2), x);
                    }
                }))
            },
            ["NEC_SKL_003"] = new CardHandler
            {
                // 부패의 손길: Poison 3, Weak 2턴
                Target = CardTarget.SelectedEnemy,
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("DecayTouch", x =>
                {
                    p.Target.Statuses.Apply(StatusType.Poison, (int)p.P("Poison", 3), x);
                    p.Target.Statuses.Apply(StatusType.Weak, (int)p.P("Weak", 2), x);
                }))
            },
            ["NEC_SKL_004"] = new CardHandler
            {
                // 묘지 도굴: 버린 더미의 Monster 카드 1장을 손패로
                Validate = (c, p) => p.ChosenCard != null && p.ChosenCard.Zone == CardZone.DiscardPile && IsMonster(c, p.ChosenCard),
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("GraveRob", x => x.Player.Deck.ReturnToHand(p.ChosenCard)))
            },
            ["NEU_SKL_001"] = new CardHandler
            {
                // 신속한 지휘: 인접한 빈 칸으로 옮기고 이번 턴 추가 행동 1회 (`09` §4)
                Target = CardTarget.SelectedAlly,
                Validate = (c, p) => p.Position.HasValue && c.Field.CanMoveAlly(p.Target, p.Position.Value),
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("QuickCommand", x =>
                {
                    x.Field.MoveAlly(p.Target, p.Position.Value);
                    ((RuntimeMonster)p.Target).ExtraActionsThisTurn += (int)p.P("ExtraAction", 1);
                }))
            },
            ["NEU_SKL_002"] = new CardHandler
            {
                // 재정비: 손패를 모두 버리고 그 수만큼 드로우
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("Reorganize", x =>
                {
                    int n = x.Player.Deck.DiscardHand();
                    x.Player.Deck.Draw(n);
                }))
            },
            // ---- 액션 ----
            ["NEC_ACT_001"] = new CardHandler
            {
                // 피의 의식: 지정 아군 희생 + Crystal 보너스 1 (강화 2), Exhaust (D-20·D-24)
                Target = CardTarget.SelectedAlly,
                Validate = (c, p) => !((RuntimeMonster)p.Target).IsSoulbound,
                Enqueue = (c, p) =>
                {
                    c.Sacrifice((RuntimeMonster)p.Target);
                    c.Queue.EnqueueMain(Fx("BloodRiteBonus", x => x.Player.CurrentCrystal += (int)p.P("CrystalBonus", 1)));
                }
            },
            ["NEC_ACT_002"] = new CardHandler
            {
                // 망자의 군대: 해골 토큰 3 (HP4/ATK2/DEF0, 강화 HP6). 빈 칸만큼만 (D-63)
                Enqueue = (c, p) =>
                {
                    int count = (int)p.P("Tokens", 3);
                    for (int i = 0; i < count; i++)
                    {
                        var t = MonsterFactory.Token((int)p.P("TokenHp", 4), (int)p.P("TokenAtk", 2), 0);
                        if (c.Field.TrySummonAlly(t, null, c) != SummonResult.Placed) break;
                    }
                }
            },
            ["NEC_ACT_003"] = new CardHandler
            {
                // 죽음의 소용돌이: 아군 몬스터마다 자기 현재 HP(강화 ×1.2)만큼 정면의 적에게 카드 피해. 정면이 비면 적 앞줄.
                // 피해를 모두 더하면 "합산 HP"다 (`17` "해당 몬스터 전방의 적", `10` §4 Sum, D-63)
                Enqueue = (c, p) =>
                {
                    double mult = p.P("DamageMult", 1.0);
                    foreach (var a in c.Allies.Where(a => !a.IsDead).ToList())
                    {
                        var pos = c.Field.PositionOf(a);
                        var target = (pos.HasValue ? c.Field.At(TeamType.Enemy, pos.Value) : null) ?? c.Field.FrontLine(TeamType.Enemy);
                        if (target == null || target.IsDead) continue;
                        c.Queue.EnqueueMain(new DamageAction { Source = c.Player, Target = target, BaseDamage = (int)System.Math.Floor(a.CurrentHP * mult) });
                    }
                }
            },
            ["NEC_ACT_004"] = new CardHandler
            {
                // 생명력 흡수: 피해 15(강화 19), 준 피해의 50%만큼 HP가 가장 낮은 아군 몬스터 회복
                Target = CardTarget.SelectedEnemy,
                Enqueue = (c, p) =>
                {
                    var hit = new DamageAction { Source = c.Player, Target = p.Target, BaseDamage = (int)p.P("Damage", 15) };
                    c.Queue.EnqueueMain(hit);
                    c.Queue.EnqueueMain(Fx("LifeDrainHeal", x =>
                    {
                        var low = x.Allies.Where(a => !a.IsDead).OrderBy(a => a.CurrentHP).ThenBy(a => (int?)x.Field.PositionOf(a) ?? 9).FirstOrDefault();
                        if (low != null) x.Heal(low, (int)System.Math.Floor(hit.LastDealt * p.P("HealRatio", 0.5)));
                    }));
                }
            },
            ["NEU_ACT_001"] = new CardHandler
            {
                // 기회 포착: 2장(강화 3) 드로우한 뒤 손패 1장을 골라 버린다. 방금 뽑은 카드도 고를 수 있다 (D-63). Ethereal
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("Opportunity", x =>
                {
                    x.Player.Deck.Draw((int)p.P("Draw", 2));
                    x.RequestDiscard(1);
                }))
            },
            ["NEU_ACT_002"] = new CardHandler
            {
                // 연금술: 손패의 Monster 카드 1장을 소멸시키고 그 비용 × 2 Crystal
                Validate = (c, p) => p.ChosenCard != null && p.ChosenCard.Zone == CardZone.Hand && IsMonster(c, p.ChosenCard),
                Enqueue = (c, p) => c.Queue.EnqueueMain(Fx("Alchemy", x =>
                {
                    int gain = p.ChosenCard.Cost * (int)p.P("Multiplier", 2);
                    x.Player.Deck.ExhaustFromHand(p.ChosenCard);
                    x.Player.CurrentCrystal += gain;
                }))
            },
            // ---- 몬스터 카드의 사용 시 효과 (`09` Monster_Effects) ----
            ["NEC_MON_007"] = new CardHandler
            {
                // 피의 사제: 아군 1마리를 고르면 희생하고 Draw 2 (고르지 않아도 된다, D-61)
                Target = CardTarget.OptionalAlly,
                Enqueue = (c, p) =>
                {
                    if (p.Target is RuntimeMonster victim && c.Sacrifice(victim) == SacrificeResult.Ok)
                    {
                        c.Queue.EnqueueMain(Fx("BloodPriestDraw", x => x.Player.Deck.Draw((int)p.P("Draw", 2))));
                    }
                }
            },
        };

        public static CardHandler For(string cardId) => cardId != null && Handlers.TryGetValue(cardId, out var h) ? h : null;

        private static bool IsMonster(BattleContext c, RuntimeCard card) => c.Cards.Get(card.CardId)?.Type == CardType.Monster;

        private static EffectAction Fx(string label, System.Action<BattleContext> run) => new EffectAction(BattleActionType.Trigger, label, run);
    }

    /// <summary>
    /// 몬스터의 소환·사망·아군 사망 효과 (`09` Monster_Effects, 1단계분). TriggerBus를 구독하고 효과를 Trigger 큐에 넣는다.
    /// OnAttack 계열은 커맨드 공격(D-27) 구현 때 더한다.
    /// </summary>
    public static class MonsterEffects
    {
        public static void Attach(BattleContext ctx)
        {
            ctx.Triggers.AllySummoned += OnSummoned;
            ctx.Triggers.UnitDied += OnDied;
        }

        private static void OnSummoned(BattleContext c, RuntimeMonster m)
        {
            switch (m.DataId)
            {
                case "NEC_MON_003": // 소환 시 무작위 적 1명에게 Poison 1 (값은 Monster_Effects → Params "Value")
                    int poison = Value(c, m, 1);
                    Trigger(c, "CursedSkeleton", x =>
                    {
                        var enemies = x.Enemies.Where(e => e.IsTargetable).ToList();
                        if (enemies.Count > 0) enemies[x.Random.Next(0, enemies.Count)].Statuses.Apply(StatusType.Poison, poison, x);
                    });
                    break;
                case "NEU_MON_004": // 소환 시 Draw 1
                case "NEU_MON_008":
                    Trigger(c, "SummonDraw", x => x.Player.Deck.Draw(1));
                    break;
            }
        }

        private static void OnDied(BattleContext c, RuntimeUnit dead, bool isSacrifice)
        {
            if (!(dead is RuntimeMonster dm)) return;
            if (dm.DataId == "NEC_MON_002") // 죽을 때 무작위 아군 1명 HP 3 회복
            {
                int heal = Value(c, dm, 3);
                Trigger(c, "HungryGhoulDeath", x =>
                {
                    var others = x.Allies.Where(a => a != dm && !a.IsDead).ToList();
                    if (others.Count > 0) x.Heal(others[x.Random.Next(0, others.Count)], heal);
                });
            }
            foreach (var ally in c.Allies.Where(a => a != dm && !a.IsDead).ToList())
            {
                if (ally.DataId == "NEC_MON_005") // 아군 사망마다 ATK +1, 최대 +4 (희생·토큰 포함, D-61)
                {
                    Trigger(c, "WraithRage", x =>
                    {
                        if (ally.GrowthStacks < 4)
                        {
                            ally.GrowthStacks++;
                            ally.Attack.AdditiveModifier += 1;
                        }
                    });
                }
            }
        }

        /// <summary>몬스터 효과 수치: 카드 정의 Params "Value"(임포터가 Monster_Effects Value에서 채움), 없으면 기본값.</summary>
        private static int Value(BattleContext c, RuntimeMonster m, int fallback)
        {
            return (int)(c.Cards?.Get(m.DataId)?.Param("Value", m.SourceCard != null && m.SourceCard.Upgraded, fallback) ?? fallback);
        }

        private static void Trigger(BattleContext c, string label, System.Action<BattleContext> run)
        {
            c.Queue.EnqueueTrigger(new EffectAction(BattleActionType.Trigger, label, run));
        }
    }
}
