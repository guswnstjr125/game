using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>1단계 빌드 범위 ID (`01` D-44 카드 32장, D-45 저주, D-69 유물, D-23·PROG-06 이벤트 7종).</summary>
    public static class Stage1Scope
    {
        /// <summary>1단계 카드 32장 (D-44). 카드 보상·이벤트 GrantCard 후보는 이것과의 교집합 (`Stage1CardPool`).</summary>
        public static readonly IReadOnlyList<string> Cards = new[]
        {
            "NEC_ACT_001", "NEC_ACT_002", "NEC_ACT_003", "NEC_ACT_004", "NEC_MON_001", "NEC_MON_002", "NEC_MON_003", "NEC_MON_004",
            "NEC_MON_005", "NEC_MON_006", "NEC_MON_007", "NEC_MON_008", "NEC_MON_009", "NEC_MON_010", "NEC_MON_013", "NEC_MON_014",
            "NEC_SKL_001", "NEC_SKL_002", "NEC_SKL_003", "NEC_SKL_004", "NEU_ACT_001", "NEU_ACT_002", "NEU_MON_001", "NEU_MON_002",
            "NEU_MON_003", "NEU_MON_004", "NEU_MON_005", "NEU_MON_006", "NEU_MON_007", "NEU_MON_008", "NEU_SKL_001", "NEU_SKL_002"
        };

        /// <summary>1단계 이벤트가 주는 저주 (D-45): 악마 계약 CUR_002·004, 어둠의 내기 CUR_002, 저주 상자 CUR_003.</summary>
        public static readonly IReadOnlyList<string> Curses = new[] { "CUR_002", "CUR_003", "CUR_004" };

        /// <summary>1단계 핸들러 검사 대상 유물 8종 (D-69): 시작·1층 보스·1~2층 엘리트 풀.</summary>
        public static readonly IReadOnlyList<string> Relics = new[]
        {
            "REL_BLOOD_CHALICE", "REL_BOSS_1F", "REL_COM_001", "REL_COM_002", "REL_COM_003", "REL_UNC_001", "REL_UNC_002", "REL_UNC_003"
        };

        /// <summary>1단계에서 지급·표시만 하는 유물 (D-69: 2층 보스 보상 직후 런이 끝난다). 핸들러 검사는 2단계.</summary>
        public static readonly IReadOnlyList<string> DisplayOnlyRelics = new[] { "REL_BOSS_2F" };

        /// <summary>1~2층 이벤트 풀 합집합 7종 (`06` Event_FloorPool, D-23, PROG-06).</summary>
        public static readonly IReadOnlyList<string> Events = new[]
        {
            "EVT_DEVIL_PACT", "EVT_BLOOD_ALTAR", "EVT_CURSED_CHEST", "EVT_SOUL_PRISON", "EVT_WANDER_MERCHANT", "EVT_FORGOTTEN_SHRINE", "EVT_DARK_WAGER"
        };
    }

    public enum HandlerKind
    {
        Card,
        Curse,
        Relic,
        Event
    }

    /// <summary>1단계 필수 핸들러 검사 결과. 임포터는 MissingCards가 있으면 실패, MissingPending은 구현 전이라 경고 (#76 2-1a).</summary>
    public sealed class Stage1HandlerCheck
    {
        /// <summary>핸들러가 없는 1단계 카드. 하나라도 있으면 임포트 실패 (`38` §5).</summary>
        public List<string> MissingCards { get; } = new List<string>();
        /// <summary>아직 구현하지 않은 저주·유물·이벤트 핸들러 ("Kind:ID"). 2-3·2-4에서 구현하면 비어야 한다.</summary>
        public List<string> MissingPending { get; } = new List<string>();
        public bool CardsOk => MissingCards.Count == 0;
    }

    /// <summary>
    /// 데이터 ID → 코드 핸들러가 있는지 (`38` §5 "동작 핸들러 없음", §6). 문장은 해석하지 않고 ID로 찾는다.
    /// 카드: Skill·Action은 <see cref="CardEffects"/>. Monster는 Monster_Effects 행이 없거나 데이터만으로 동작하면(SacrificeCrystal·FusionAsClass, D-71)
    /// 핸들러가 필요 없고, 그 밖의 효과는 <see cref="CardEffects"/> 또는 <see cref="MonsterEffects"/>에 있어야 한다.
    /// 저주·유물·이벤트는 구현할 때 <see cref="Register"/>로 등록한다.
    /// </summary>
    public static class HandlerRegistry
    {
        /// <summary>코드 없이 로더가 데이터로 처리하는 몬스터 효과 (D-71).</summary>
        private static readonly HashSet<string> DataOnlyMonsterEffects = new HashSet<string> { "SacrificeCrystal", "FusionAsClass" };

        private static readonly Dictionary<HandlerKind, HashSet<string>> Registered = new Dictionary<HandlerKind, HashSet<string>>
        {
            [HandlerKind.Curse] = new HashSet<string>(CurseEffects.Implemented),
            [HandlerKind.Relic] = new HashSet<string>(),
            [HandlerKind.Event] = new HashSet<string>(EventFlow.Implemented),
        };

        /// <summary>저주·유물·이벤트 핸들러를 구현한 코드가 자기 ID를 등록한다.</summary>
        internal static void Register(HandlerKind kind, string id) => Registered[kind].Add(id);

        public static bool Has(HandlerKind kind, string id, GameData data)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (kind != HandlerKind.Card) return Registered[kind].Contains(id);
            if (data == null) throw new System.ArgumentNullException(nameof(data), "card handler check needs Cards_Master·Monster_Effects");

            var row = data.Rows("Cards_Master").FirstOrDefault(r => r.Str("ID") == id);
            if (row == null) return false;
            if (CardEffects.For(id) != null) return true;
            if (row.Str("타입") != "Monster") return false;
            if (MonsterEffects.Handles(id)) return true;
            return data.Rows("Monster_Effects").Where(r => r.Str("Card_ID") == id).All(r => DataOnlyMonsterEffects.Contains(r.Str("Effect")));
        }

        public static List<string> Missing(HandlerKind kind, IEnumerable<string> ids, GameData data) => ids.Where(id => !Has(kind, id, data)).ToList();

        /// <summary>1단계 범위 전체 검사 (`38` §5). 카드는 오류, 저주·유물·이벤트는 구현 전이라 보류 목록.</summary>
        public static Stage1HandlerCheck CheckStage1(GameData data)
        {
            var r = new Stage1HandlerCheck();
            r.MissingCards.AddRange(Missing(HandlerKind.Card, Stage1Scope.Cards, data));
            foreach (var (kind, ids) in new[] { (HandlerKind.Curse, Stage1Scope.Curses), (HandlerKind.Relic, Stage1Scope.Relics), (HandlerKind.Event, Stage1Scope.Events) })
            {
                r.MissingPending.AddRange(Missing(kind, ids, data).Select(id => $"{kind}:{id}"));
            }
            return r;
        }
    }
}
