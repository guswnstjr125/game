using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>
    /// 1단계 유물 8종의 전투 동작 (`24` §1, D-04·D-15·D-69·D-76). 전투를 만들 때 ctx.Relics를 보고 TriggerBus 등에 붙는다.
    /// REL_BOSS_2F는 1단계에서 지급·표시만 한다 (D-69).
    /// </summary>
    public static class RelicEffects
    {
        public static readonly IReadOnlyList<string> Implemented = new[]
        {
            "REL_BLOOD_CHALICE", "REL_BOSS_1F", "REL_COM_001", "REL_COM_002", "REL_COM_003", "REL_UNC_001", "REL_UNC_002", "REL_UNC_003"
        };

        public const double GloveChance = 0.1;
        public const int CrystalShardDrawsPerTurn = 2;
        public const int SealMaxBonus = 3;
        public const int ClawMaxBonus = 5;

        public static void Attach(BattleContext ctx)
        {
            var relics = new HashSet<string>(ctx.Relics);

            // 피의 잔(D-04)·굶주린 자의 뼈(D-15): 턴의 첫 희생에 각각 +1, 겹친다
            ctx.FirstSacrificeBonus = (relics.Contains("REL_BLOOD_CHALICE") ? 1 : 0) + (relics.Contains("REL_BOSS_1F") ? 1 : 0);

            if (relics.Contains("REL_COM_001") && ctx.Player != null)
            {
                // 낡은 뼈조각: 비용을 내서 0보다 크다가 0이 된 순간 +1, 턴당 1회
                int usedTurn = -1;
                ctx.Player.CrystalSpent += (before, after) =>
                {
                    if (before > 0 && after == 0 && usedTurn != ctx.Turn)
                    {
                        usedTurn = ctx.Turn;
                        ctx.Player.CurrentCrystal += 1;
                    }
                };
            }

            if (relics.Contains("REL_COM_002"))
            {
                // 독이 든 가시: 적의 공격 피해 1건마다 공격자 Poison 1 (아군 몬스터·플레이어, 공격자가 살아 있을 때)
                ctx.Triggers.EnemyAttackHit += (c, attacker, target) =>
                {
                    if (attacker.IsDead) return;
                    c.Queue.EnqueueTrigger(new EffectAction(BattleActionType.Trigger, "PoisonThorn", x =>
                    {
                        if (!attacker.IsDead) attacker.Statuses.Apply(StatusType.Poison, 1, x);
                    }));
                };
            }

            if (relics.Contains("REL_COM_003"))
            {
                // 피 묻은 장갑: 아군 커맨드 공격 1회마다 전투 난수 1회로 10%, 대상이 살아 있으면 Vulnerable 1턴
                ctx.Triggers.AllyAttacked += (c, attacker, target, killed) =>
                {
                    bool hit = c.Random.Chance(GloveChance);
                    if (!hit || killed || target.IsDead) return;
                    c.Queue.EnqueueTrigger(new EffectAction(BattleActionType.Trigger, "BloodGlove", x =>
                    {
                        if (!target.IsDead) target.Statuses.Apply(StatusType.Vulnerable, 1, x);
                    }));
                };
            }

            if (relics.Contains("REL_UNC_001"))
            {
                // 부서진 크리스탈: 희생마다 Draw 1, 그 턴 처음 2번
                int turn = -1, count = 0;
                ctx.Triggers.UnitDied += (c, dead, isSacrifice) =>
                {
                    if (!isSacrifice || !(dead is RuntimeMonster)) return;
                    if (turn != c.Turn)
                    {
                        turn = c.Turn;
                        count = 0;
                    }
                    if (count >= CrystalShardDrawsPerTurn) return;
                    count++;
                    c.Queue.EnqueueTrigger(new EffectAction(BattleActionType.Draw, "BrokenCrystalDraw", x => x.Player.Deck.Draw(1)));
                };
            }

            if (relics.Contains("REL_UNC_002"))
            {
                // 재앙의 인장: 아군 몬스터 ATK + min(3, 살아 있는 Burn 적 수), 읽을 때마다 계산
                ctx.AttackBonuses.Add((c, m) => System.Math.Min(SealMaxBonus, c.Enemies.Count(e => !e.IsDead && e.Statuses.Has(StatusType.Burn))));
            }

            if (relics.Contains("REL_UNC_003"))
            {
                // 맹수의 발톱: 아군 몬스터의 커맨드 공격 피해로 처치하면 그 몬스터 ATK +1, 전투 동안, 몬스터당 최대 +5
                var gained = new Dictionary<RuntimeMonster, int>();
                ctx.Triggers.AllyAttacked += (c, attacker, target, killed) =>
                {
                    if (!killed) return;
                    gained.TryGetValue(attacker, out int n);
                    if (n >= ClawMaxBonus) return;
                    gained[attacker] = n + 1;
                    c.Queue.EnqueueTrigger(new EffectAction(BattleActionType.Trigger, "BeastClaw", x => attacker.Attack.AdditiveModifier += 1));
                };
            }
        }
    }
}
