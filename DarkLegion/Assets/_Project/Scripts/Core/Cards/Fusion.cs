using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>융합 실패 이유 (`13` §3.4). UI는 이 값으로 토스트 문구를 고른다 (`43`).</summary>
    public enum FusionError
    {
        None,
        InvalidPhase,
        NotInHand,
        SameCard,
        UnknownCard,
        NotMonster,
        LevelMismatch,
        /// <summary>Lv3 + Lv3: MVP 불가 (D-03 `LEVEL_CAP`).</summary>
        LevelCap,
        Soulbound,
        NoCharge,
        NotEnoughCrystal,
        /// <summary>고정 레시피·랜덤 풀·대체 풀이 모두 비어 결과가 없다 (데이터 오류).</summary>
        NoResult
    }

    public sealed class FusionOutcome
    {
        public FusionError Error;
        public RuntimeCard Result;
        /// <summary>고정 레시피로 나왔으면 그 ID (FUS_001 등), 랜덤이면 null.</summary>
        public string RecipeId;

        public bool Ok => Error == FusionError.None;
    }

    /// <summary>융합 한 번의 기록. 런 계층이 전투 뒤 런 덱에 반영한다 (재료 제거, 결과 편입, `14` CardSaveDTO.FromFusion).</summary>
    public sealed class FusionRecord
    {
        public string MaterialA;
        public string MaterialB;
        public string Result;
        public string RecipeId;
    }

    /// <summary>
    /// 융합 데이터 (`09_몬스터_바이블.xlsx` Fusion_Recipes·Fusion_RandomPool). 임포터가 MON_ID를 Card_ID로 바꿔 채운다.
    /// 가중치 0 행(MVP 제외, D-21·D-22)은 넣지 않는다.
    /// 풀 키: 같은 클래스 "NEC", NEU + 클래스 "NEC+NEU", NEU + NEU "NEU".
    /// </summary>
    public sealed class FusionCatalog
    {
        private readonly Dictionary<(string, string), (string recipe, string result)> _fixed = new Dictionary<(string, string), (string, string)>();
        private readonly Dictionary<(string, int), List<(string card, int weight)>> _pools = new Dictionary<(string, int), List<(string, int)>>();
        private readonly Dictionary<(string, int), List<string>> _commons = new Dictionary<(string, int), List<string>>();
        private readonly HashSet<string> _classMimics = new HashSet<string>();

        /// <summary>융합 때 상대 재료의 클래스로 취급하는 카드 (`09` Monster_Effects FusionAsClass, 모방 슬라임, D-70).</summary>
        public void AddClassMimic(string cardId) => _classMimics.Add(cardId);

        public bool IsClassMimic(string cardId) => cardId != null && _classMimics.Contains(cardId);

        public void AddFixed(string recipeId, string cardA, string cardB, string resultCard)
        {
            _fixed[Key(cardA, cardB)] = (recipeId, resultCard);
        }

        public void AddPool(string poolKey, int inputLevel, string resultCard, int weight)
        {
            if (weight <= 0) return;
            if (!_pools.TryGetValue((poolKey, inputLevel), out var list)) _pools[(poolKey, inputLevel)] = list = new List<(string, int)>();
            list.Add((resultCard, weight));
        }

        /// <summary>풀이 비었을 때 대체 결과: 재료 A 클래스 Lv+1 Common 카드 (`13` §3.4).</summary>
        public void SetCommonFallback(string cardClass, int resultLevel, params string[] cards) => _commons[(cardClass, resultLevel)] = cards.ToList();

        public bool TryGetFixed(string cardA, string cardB, out string recipeId, out string result)
        {
            if (_fixed.TryGetValue(Key(cardA, cardB), out var r))
            {
                recipeId = r.recipe;
                result = r.result;
                return true;
            }
            recipeId = result = null;
            return false;
        }

        public IReadOnlyList<(string card, int weight)> Pool(string poolKey, int inputLevel) =>
            _pools.TryGetValue((poolKey, inputLevel), out var l) ? l : (IReadOnlyList<(string, int)>)new List<(string, int)>();

        public IReadOnlyList<string> Commons(string cardClass, int resultLevel) =>
            _commons.TryGetValue((cardClass, resultLevel), out var l) ? l : (IReadOnlyList<string>)new List<string>();

        private static (string, string) Key(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
    }

    /// <summary>
    /// 융합 규칙 (`01` D-03, `13` §3). 전투 중 Main Phase, 손패의 같은 레벨 몬스터 카드 2장, 충전 1 + Crystal(Lv1 2·Lv2 3).
    /// 재료는 영구 소멸, 결과 카드는 손패로 (같은 턴에 소환 가능).
    /// </summary>
    public static class FusionSystem
    {
        public const int MaxCharge = 3;
        public const int MaxMaterialLevel = 2;

        /// <summary>노드 3·6·9 클리어 시 충전 +1 (D-03). 그 밖의 노드는 0.</summary>
        public static int ChargeForNodeClear(int nodeNumber) => nodeNumber > 0 && nodeNumber % 3 == 0 ? 1 : 0;

        /// <summary>재료 레벨별 Crystal 비용: Lv1 2, Lv2 3 (D-03).</summary>
        public static int CostFor(int materialLevel) => materialLevel == 1 ? 2 : 3;

        /// <summary>카드 ID의 클래스 (NEC_MON_001 → NEC).</summary>
        public static string ClassOf(string cardId)
        {
            int i = cardId.IndexOf('_');
            return i > 0 ? cardId.Substring(0, i) : cardId;
        }

        public static FusionError Validate(BattleContext ctx, RuntimeCard a, RuntimeCard b)
        {
            if (ctx.State != BattleState.PlayerPhase) return FusionError.InvalidPhase;
            if (a == null || b == null || a.Zone != CardZone.Hand || b.Zone != CardZone.Hand) return FusionError.NotInHand;
            if (a == b) return FusionError.SameCard;
            var da = ctx.Cards?.Get(a.CardId);
            var db = ctx.Cards?.Get(b.CardId);
            if (da == null || db == null) return FusionError.UnknownCard;
            if (da.Type != CardType.Monster || db.Type != CardType.Monster) return FusionError.NotMonster;
            if (da.Level != db.Level) return FusionError.LevelMismatch;
            if (da.Level > MaxMaterialLevel) return FusionError.LevelCap;
            if (da.Soulbound || db.Soulbound) return FusionError.Soulbound;
            if (ctx.Player.FusionCharge < 1) return FusionError.NoCharge;
            if (ctx.Player.CurrentCrystal < CostFor(da.Level)) return FusionError.NotEnoughCrystal;
            if (!HasAnyResult(ctx.Fusion, a.CardId, b.CardId, da.Level)) return FusionError.NoResult;
            return FusionError.None;
        }

        public static FusionOutcome Fuse(BattleContext ctx, RuntimeCard a, RuntimeCard b)
        {
            var error = Validate(ctx, a, b);
            if (error != FusionError.None) return new FusionOutcome { Error = error };

            int level = ctx.Cards.Get(a.CardId).Level;
            ctx.Player.TrySpendCrystal(CostFor(level));
            ctx.Player.FusionCharge--;
            var deck = ctx.Player.Deck;
            deck.DestroyFromHand(a);
            deck.DestroyFromHand(b);

            string resultId = Resolve(ctx.Fusion, a.CardId, b.CardId, level, ctx.Random, out var recipeId);
            var card = ctx.Cards.Get(resultId).CreateCard(deck.NextInstanceId(), false);
            card.FromFusion = true;
            deck.AddToHand(card);
            ctx.FusionHistory.Add(new FusionRecord { MaterialA = a.CardId, MaterialB = b.CardId, Result = resultId, RecipeId = recipeId });
            ctx.Log($"[FUSION] {a.CardId} + {b.CardId} → {resultId}{(recipeId != null ? " (" + recipeId + ")" : "")}");
            return new FusionOutcome { Result = card, RecipeId = recipeId };
        }

        /// <summary>
        /// 결과 판정 (D-03): 1) 고정 레시피 100%  2) 랜덤 — 같은 클래스·NEU 섞임은 그 풀의 Weight 비례,
        /// 서로 다른 두 클래스는 50%로 한쪽 클래스를 고른 뒤 그 클래스 풀의 같은 클래스 결과만  3) 풀이 비면 재료 A 클래스 Lv+1 Common 균등.
        /// </summary>
        public static string Resolve(FusionCatalog catalog, string cardA, string cardB, int level, BattleRandom random, out string recipeId)
        {
            if (catalog.TryGetFixed(cardA, cardB, out recipeId, out var fixedResult)) return fixedResult;
            recipeId = null;

            var (ca, cb) = MaterialClasses(catalog, cardA, cardB);
            IReadOnlyList<(string card, int weight)> pool;
            if (ca == cb) pool = catalog.Pool(ca, level);
            else if (ca == "NEU" || cb == "NEU") pool = catalog.Pool((ca == "NEU" ? cb : ca) + "+NEU", level);
            else
            {
                string side = random.Chance(0.5) ? ca : cb;
                pool = catalog.Pool(side, level).Where(p => ClassOf(p.card) == side).ToList();
            }

            if (pool.Count > 0)
            {
                int total = pool.Sum(p => p.weight);
                int roll = random.Next(0, total);
                foreach (var p in pool)
                {
                    if (roll < p.weight) return p.card;
                    roll -= p.weight;
                }
            }
            var commons = catalog.Commons(ca, level + 1);
            return commons.Count > 0 ? commons[random.Next(0, commons.Count)] : null;
        }

        /// <summary>
        /// 랜덤 판정에 쓰는 재료 클래스. 모방 슬라임처럼 클래스를 따라가는 카드는 상대 재료의 클래스로 본다 (D-70).
        /// 둘 다 그런 카드면 원래 클래스(NEU) 그대로. 고정 레시피는 이 함수 전에 카드 ID로 찾는다.
        /// </summary>
        public static (string a, string b) MaterialClasses(FusionCatalog catalog, string cardA, string cardB)
        {
            string ca = ClassOf(cardA), cb = ClassOf(cardB);
            bool ma = catalog.IsClassMimic(cardA), mb = catalog.IsClassMimic(cardB);
            if (ma && !mb) ca = cb;
            else if (mb && !ma) cb = ca;
            return (ca, cb);
        }

        private static bool HasAnyResult(FusionCatalog catalog, string a, string b, int level)
        {
            if (catalog == null) return false;
            if (catalog.TryGetFixed(a, b, out _, out _)) return true;
            var (ca, cb) = MaterialClasses(catalog, a, b);
            if (catalog.Commons(ca, level + 1).Count > 0) return true;
            if (ca == cb) return catalog.Pool(ca, level).Count > 0;
            if (ca == "NEU" || cb == "NEU") return catalog.Pool((ca == "NEU" ? cb : ca) + "+NEU", level).Count > 0;
            // 서로 다른 클래스: 어느 쪽이 나와도 결과가 있어야 한다
            return catalog.Pool(ca, level).Any(p => ClassOf(p.card) == ca) && catalog.Pool(cb, level).Any(p => ClassOf(p.card) == cb);
        }
    }
}
