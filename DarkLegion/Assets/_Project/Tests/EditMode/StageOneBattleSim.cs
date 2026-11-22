using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkLegion.Core;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 1단계 카드 포함 전투 시뮬 (#76 1-3, `32` BAL-04 사전 확인, `01` D-14·D-29).
    /// 실제 export JSON(`DarkLegion/DesignData`)으로 Encounters 1~2층 편성을 만들고, NEC 시작 덱(`18` §2)을 <see cref="SimBot"/>이 플레이한다.
    /// 전투마다 HP 80에서 시작해 독립으로 잰다 (보상 카드·휴식·이전 전투 손실 없음). 수동 플레이(BAL-04)를 대신하지 않는다.
    /// </summary>
    public static class StageOneBattleSim
    {
        /// <summary>`18` §2 NEC 시작 덱 10장.</summary>
        public static readonly string[] NecStartDeck =
        {
            "NEC_MON_001", "NEC_MON_002", "NEC_MON_003", "NEC_MON_004", "NEC_SKL_001",
            "NEC_SKL_003", "NEC_ACT_001", "NEU_MON_001", "NEU_MON_003", "NEU_ACT_001"
        };

        public const int StartHp = 80;
        /// <summary>피의 잔 `REL_BLOOD_CHALICE`: 턴의 첫 희생 Crystal +1 (D-04·D-06).</summary>
        public const int BloodChaliceBonus = 1;

        public sealed class Result
        {
            public Encounter Encounter;
            public bool Wave2;
            public int Seed;
            public BattleOutcome Outcome;
            public int Turns;
            public int HpLoss;
        }

        private static GameData _data;
        private static GameDataLoadResult _loaded;

        public static GameData Data()
        {
            if (_data != null) return _data;
            string dir = DesignDataParityTests.FindDesignData();
            if (dir == null) throw new FileNotFoundException("DarkLegion/DesignData/*.json 없음 — python tools/export_game_data.py");
            var texts = Directory.GetFiles(dir, "*.json").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => File.ReadAllText(f));
            return _data = GameData.FromJson(texts);
        }

        public static GameDataLoadResult Loaded() => _loaded ?? (_loaded = GameDataLoader.Load(Data()));

        /// <summary>`06` Encounters의 그 층 편성 (Core 로더, D-61).</summary>
        public static List<Encounter> Encounters(int floor) => Loaded().Encounters.All.Where(e => e.Floor == floor).ToList();

        public static BattleContext Build(Encounter enc, bool wave2, int seed)
        {
            var loaded = Loaded();
            var ctx = new BattleContext(seed)
            {
                Cards = loaded.Cards,
                EnemyKits = loaded.EnemyKits,
                BossPatterns = loaded.BossPatterns,
                Fusion = loaded.Fusion,
                FirstSacrificeBonus = BloodChaliceBonus
            };
            int id = 0;
            var deck = NecStartDeck.Select(c => ctx.Cards.Get(c).CreateCard(++id, false)).ToList();
            ctx.Player = new RuntimePlayer("P", StartHp, new RuntimeDeck(deck, ctx.Random));

            EncounterFactory.Spawn(ctx, enc, wave2, enc.Node - 1);
            return ctx;
        }

        public static Result Run(Encounter enc, bool wave2, int seed)
        {
            var ctx = Build(enc, wave2, seed);
            var bot = new SimBot();
            while (ctx.Outcome == BattleOutcome.None && ctx.Turn < BattleLimits.MaxTurnCount)
            {
                ctx.StartPlayerTurn();
                bot.PlayMainPhase(ctx);
                ctx.EndPlayerTurn();
            }
            return new Result
            {
                Encounter = enc,
                Wave2 = wave2,
                Seed = seed,
                Outcome = ctx.Outcome,
                Turns = ctx.Turn,
                HpLoss = StartHp - System.Math.Max(0, ctx.Player.CurrentHP)
            };
        }
    }

    /// <summary>
    /// 시뮬용 탐욕 플레이어. 사람이 할 법한 단순한 선택만 한다 (최적이 아니다):
    /// 0비용 카드 → 파편 소환 → 비싼 몬스터부터 앞 칸에 → 파편·빈사 아군 희생(Crystal이 모자라거나 칸이 막혔을 때) → 부패의 손길.
    /// 커맨드는 전부 공격(기본값). 융합은 하지 않는다 (시작 덱에 FUS_001·014 재료 쌍이 한 장씩뿐).
    /// </summary>
    public sealed class SimBot
    {
        private const string Shard = "NEU_MON_001";
        /// <summary>이번 턴에 시도한 카드와 결과 (추적용).</summary>
        public readonly List<string> Actions = new List<string>();

        private bool Log(string what, PlayResult r)
        {
            Actions.Add(what + ":" + r);
            return r == PlayResult.Ok;
        }

        public void PlayMainPhase(BattleContext ctx)
        {
            for (int guard = 0; guard < 40 && ctx.Outcome == BattleOutcome.None; guard++)
            {
                if (!TryAct(ctx)) break;
                ctx.Queue.Process();
                while (ctx.State == BattleState.WaitingDiscard) ctx.ResolveDiscard(DiscardChoice(ctx));
            }
        }

        private static CardDefinition Def(BattleContext c, RuntimeCard card) => c.Cards.Get(card.CardId);

        private static bool Affordable(BattleContext c, RuntimeCard card) => c.Player.CurrentCrystal >= card.Cost;

        private static SlotPosition? FreeSlot(BattleContext c)
        {
            foreach (var p in new[] { SlotPosition.Front, SlotPosition.Middle, SlotPosition.Back })
            {
                if (c.Field.At(TeamType.Player, p) == null) return p;
            }
            return null;
        }

        private static List<RuntimeCard> Hand(BattleContext c) => c.Player.Deck.Hand.ToList();

        private static RuntimeEnemy HighestHpEnemy(BattleContext c) =>
            c.Enemies.Where(e => e.IsTargetable).OrderByDescending(e => e.CurrentHP).FirstOrDefault();

        /// <summary>희생해도 아까운 게 적은 아군: 파편, 그다음 HP 3 이하인 Lv1. Soulbound는 안 된다.</summary>
        private static RuntimeMonster Fodder(BattleContext c) =>
            c.Allies.Where(a => !a.IsDead && !a.SacrificeReserved && !a.IsSoulbound && (a.DataId == Shard || (a.Level == 1 && a.CurrentHP <= 3)))
                .OrderBy(a => a.DataId == Shard ? 0 : 1).ThenBy(a => a.CurrentHP).FirstOrDefault();

        /// <summary>손패에 Crystal이 모자라 못 내는 카드가 있다 (몬스터는 빈 칸이 있을 때만).</summary>
        private static bool WantsCrystal(BattleContext c, bool freeSlotAfter) =>
            Hand(c).Any(card => !Affordable(c, card) && (Def(c, card).Type != CardType.Monster || freeSlotAfter));

        private bool TryAct(BattleContext c)
        {
            var hand = Hand(c);

            // 1) 기회 포착 (0)
            var opp = hand.FirstOrDefault(x => x.CardId == "NEU_ACT_001");
            if (opp != null && Log("opp", c.PlayCard(opp, null))) return true;

            // 2) 영혼 추출 (0): 희생할 아군에게 (없으면 HP가 가장 낮은 아군)
            var soul = hand.FirstOrDefault(x => x.CardId == "NEC_SKL_001" && Affordable(c, x));
            var soulTarget = Fodder(c) ?? c.Allies.Where(a => !a.IsDead).OrderBy(a => a.CurrentHP).FirstOrDefault();
            if (soul != null && soulTarget != null && c.PlayCard(soul, soulTarget) == PlayResult.Ok) return true;

            // 3) 파편 소환 (0): Back부터
            var shard = hand.FirstOrDefault(x => x.CardId == Shard);
            if (shard != null)
            {
                var pos = new[] { SlotPosition.Back, SlotPosition.Middle, SlotPosition.Front }.Cast<SlotPosition?>().FirstOrDefault(p => c.Field.At(TeamType.Player, p.Value) == null);
                if (pos.HasValue && c.PlayCard(shard, null, pos) == PlayResult.Ok) return true;
            }

            // 4) 비싼 몬스터부터 앞 칸에
            var slot = FreeSlot(c);
            if (slot.HasValue)
            {
                var mon = hand.Where(x => Def(c, x).Type == CardType.Monster && x.CardId != Shard && Affordable(c, x)).OrderByDescending(x => x.Cost).ThenByDescending(x => Def(c, x).Hp).FirstOrDefault();
                if (mon != null && c.PlayCard(mon, null, slot) == PlayResult.Ok) return true;
            }

            // 5) 희생: Crystal이 모자라거나, 칸이 다 차서 손패 몬스터를 못 낼 때. 피의 의식(+1 보너스)을 먼저, 없으면 직접
            var fodder = Fodder(c);
            bool blockedMonster = !slot.HasValue && hand.Any(x => Def(c, x).Type == CardType.Monster && x.CardId != Shard);
            if (fodder != null && (WantsCrystal(c, true) || blockedMonster))
            {
                var rite = hand.FirstOrDefault(x => x.CardId == "NEC_ACT_001" && Affordable(c, x));
                if (rite != null && c.PlayCard(rite, fodder) == PlayResult.Ok) return true;
                if (c.Sacrifice(fodder) == SacrificeResult.Ok) return true;
            }

            // 6) 부패의 손길: HP가 가장 많은 적
            var decay = hand.FirstOrDefault(x => x.CardId == "NEC_SKL_003" && Affordable(c, x));
            var target = HighestHpEnemy(c);
            if (decay != null && target != null && Log("decay", c.PlayCard(decay, target))) return true;

            return false;
        }

        /// <summary>
        /// 버릴 카드: 이번 턴 못 쓰는 카드부터 — 칸이 없는 몬스터, 희생할 아군이 없는 피의 의식, Crystal이 모자란 가장 비싼 카드.
        /// 다 쓸 수 있으면 가장 싼 몬스터 (몬스터는 다음 턴에도 쓸 수 있고, 부패의 손길·영혼 추출은 이번 턴 가치가 크다).
        /// </summary>
        private static RuntimeCard DiscardChoice(BattleContext c)
        {
            var hand = Hand(c);
            bool slot = FreeSlot(c).HasValue;
            return hand.FirstOrDefault(x => Def(c, x).Type == CardType.Monster && x.CardId != Shard && !slot)
                ?? hand.FirstOrDefault(x => x.CardId == "NEC_ACT_001" && Fodder(c) == null)
                ?? hand.Where(x => !Affordable(c, x)).OrderByDescending(x => x.Cost).FirstOrDefault()
                ?? hand.Where(x => Def(c, x).Type == CardType.Monster).OrderBy(x => x.Cost).FirstOrDefault()
                ?? hand.OrderByDescending(x => x.Cost).First();
        }
    }
}
