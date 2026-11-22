using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>
    /// 이벤트 화면 문구 (`40_이벤트_화면_문구.md` §1~§7, D-52). 1~2층 풀에 나오는 7종만 담는다.
    /// 문구 정본은 `40`이다. 바꾸려면 `40`을 먼저 고친다.
    /// </summary>
    public static class EventScreenText
    {
        sealed class Entry
        {
            public string Title;
            public string Intro;
            public string[] Labels;
            public string[] Conditions;
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>
        {
            ["EVT_DEVIL_PACT"] = new Entry
            {
                Title = "악마 계약",
                Intro = "불꽃 속에서 뿔 달린 그림자가 몸을 숙인다. \"영웅이었다지. 그 이름값, 아직 남아 있나?\" 그림자가 손바닥을 펼치자 피로 쓴 계약서 두 장이 떠오른다.",
                Labels = new[] { "살을 내준다 (최대 HP -10, 카드 3택1)", "영혼에 낙인을 받는다 (저주 1장, 유물 최대 3택1)", "계약서를 찢는다" },
                Conditions = new[] { "최대 HP 21 이상", "미보유 유물 후보 있음", "" }
            },
            ["EVT_BLOOD_ALTAR"] = new Entry
            {
                Title = "피의 제단",
                Intro = "검게 굳은 피로 덮인 제단이 있다. 홈을 따라 흐르는 피는 아직 따뜻하다. 제단은 무언가를 바치면 되돌려 준다고 속삭인다.",
                Labels = new[] { "내 피를 바친다 (HP -20, 카드 1장 강화)", "군단의 피를 바친다 (몬스터 카드 1장 제거, 무작위 강화)", "손대지 않는다" },
                Conditions = new[] { "HP 21 이상", "제거 후 몬스터 3장 이상 및 강화 가능 카드 있음", "" }
            },
            ["EVT_CURSED_CHEST"] = new Entry
            {
                Title = "저주 상자",
                Intro = "쇠사슬로 묶인 상자가 바닥에 반쯤 묻혀 있다. 자물쇠는 이미 부서졌고, 틈새로 차가운 숨결 같은 것이 새어 나온다.",
                Labels = new[] { "상자를 연다 (유물 1개, 40% 확률로 저주 1장)", "", "그대로 둔다" },
                Conditions = new[] { "미보유 유물 후보 있음", "", "" }
            },
            ["EVT_SOUL_PRISON"] = new Entry
            {
                Title = "영혼 감옥",
                Intro = "뼈로 만든 우리 안에서 희미한 영혼이 몸을 웅크린다. \"꺼내 줘. 보답할게.\" 우리 뒤 어둠 속에서 무언가가 숨을 죽이고 있다.",
                Labels = new[] { "우리를 부순다 (50% 카드 3택1, 50% 전투)", "영혼의 온기를 받는다 (HP +12)", "지나친다" },
                Conditions = new[] { "", "", "" }
            },
            ["EVT_WANDER_MERCHANT"] = new Entry
            {
                Title = "방랑 상인",
                Intro = "얼굴 없는 상인이 등에 진 궤짝을 내려놓는다. \"산 자든 죽은 자든 손님이지. 금이 없으면 피도 받네.\"",
                Labels = new[] { "금으로 산다 (골드 -8, 카드 3택1)", "피로 산다 (HP -4, 카드 1장)", "사지 않는다" },
                Conditions = new[] { "골드 8 이상", "HP 5 이상", "" }
            },
            ["EVT_FORGOTTEN_SHRINE"] = new Entry
            {
                Title = "잊힌 제단",
                Intro = "이름이 지워진 신의 제단이다. 지옥 한가운데에서도 이곳만은 조용하다. 꺼진 촛대 하나가 아직 손님을 기다린다.",
                Labels = new[] { "무릎을 꿇는다 (HP +15)", "몬스터를 바친다 (몬스터 카드 1장 제거, 선택 강화)", "떠난다" },
                Conditions = new[] { "", "제거 후 몬스터 3장 이상 및 강화 가능 카드 있음", "" }
            },
            ["EVT_DARK_WAGER"] = new Entry
            {
                Title = "어둠의 내기",
                Intro = "해골 둘이 뼈 주사위를 굴리다 고개를 든다. \"한 판 하지. 지는 쪽이 내놓는 거야.\" 판 위에는 금화와 핏자국이 함께 흩어져 있다.",
                Labels = new[] { "첫 판을 건다 (50% 승리: 1단계 HP +18 / 2단계 골드 +25, 50% HP -12)", "영혼을 건다 (50% 카드 1장, 50% 저주 1장)", "판을 뒤엎는다" },
                Conditions = new[] { "", "", "" }
            }
        };

        public static bool Has(string eventId) => Entries.ContainsKey(eventId);

        public static IEnumerable<string> EventIds => Entries.Keys;

        public static string Title(string eventId) => Entries.TryGetValue(eventId, out var e) ? e.Title : eventId;

        public static string Intro(string eventId) => Entries.TryGetValue(eventId, out var e) ? e.Intro : "";

        /// <summary>선택지 한 줄. 저주 상자 2번은 1단계·2단계 문구가 다르다.</summary>
        public static string OptionLabel(string eventId, int option, bool stage1Build)
        {
            if (eventId == "EVT_CURSED_CHEST" && option == 2)
                return stage1Build ? "사슬을 풀고 숨을 고른다 (HP +6)" : "사슬만 풀어 판다 (골드 40)";
            return Entries.TryGetValue(eventId, out var e) && option >= 1 && option <= e.Labels.Length ? e.Labels[option - 1] : "";
        }

        public static string OptionCondition(string eventId, int option) =>
            Entries.TryGetValue(eventId, out var e) && option >= 1 && option <= e.Conditions.Length ? e.Conditions[option - 1] : "";

        /// <summary>결과 문구. 확률 결과는 실제로 나온 쪽의 문구다. curseName은 받은 저주 이름({저주 이름}).</summary>
        public static string ResultText(string eventId, int option, bool stage1Build, EventOutcome outcome, string curseName)
        {
            switch (eventId)
            {
                case "EVT_DEVIL_PACT":
                    if (option == 1) return "계약서가 살 속으로 녹아든다. 몸은 가벼워지고, 손에 새 힘이 쥐어진다.";
                    if (option == 2) return "그림자가 웃는다. '" + curseName + ". 오래 지니게 될 거다.' 대가로 내민 상자에서 빛이 샌다.";
                    return "찢긴 종이가 재가 된다. 그림자는 아무 말 없이 불 속으로 물러난다.";
                case "EVT_BLOOD_ALTAR":
                    if (option == 1) return "피가 홈을 채우자 덱의 카드 하나가 붉게 달아오른다.";
                    if (option == 2) return "제물이 쓰러지자 제단이 낮게 울린다. 군단 어딘가가 조금 더 강해졌다.";
                    return "제단의 속삭임이 등 뒤로 멀어진다.";
                case "EVT_CURSED_CHEST":
                    if (option == 1)
                        return outcome.CursesToAdd.Count > 0
                            ? "유물을 꺼내는 순간 무언가 손목을 감는다. 타락한 의지가 덱에 섞여 든다."
                            : "상자 안에는 오래된 유물 하나뿐이다.";
                    if (option == 2) return stage1Build ? "사슬이 풀리자 숨을 고를 틈이 생긴다." : "녹슨 사슬도 지옥에서는 값이 나간다.";
                    return "상자는 다시 흙 속으로 가라앉는다.";
                case "EVT_SOUL_PRISON":
                    if (option == 1)
                        return outcome.ForcedBattle
                            ? "우리가 부서지자 어둠 속의 간수가 몸을 일으킨다."
                            : "영혼이 빛이 되어 흩어지고, 빛 속에 카드 세 장이 떠오른다.";
                    if (option == 2) return "영혼이 마지막 온기를 건넨다. 상처가 조금 아문다.";
                    return "등 뒤에서 흐느끼는 소리가 오래 이어진다.";
                case "EVT_WANDER_MERCHANT":
                    if (option == 1) return "상인이 궤짝을 열어 보인다. 셋 중 하나를 고르라는 듯 손가락을 세운다.";
                    if (option == 2) return "상인이 피 한 방울을 병에 담고, 말없이 카드 한 장을 내민다.";
                    return "상인은 궤짝을 다시 지고 안개 속으로 걸어간다.";
                case "EVT_FORGOTTEN_SHRINE":
                    if (option == 1) return "오래전 버린 기도가 입에서 흘러나온다. 몸이 조금 가벼워진다.";
                    if (option == 2) return "촛대에 불이 붙는다. 바친 것만큼 카드 하나가 단단해진다.";
                    return "촛대는 다시 어둠에 묻힌다.";
                case "EVT_DARK_WAGER":
                    if (option == 1)
                    {
                        if (outcome.HpDelta < 0) return "주사위가 등을 돌린다. 해골이 뼈 칼로 값을 받아 간다.";
                        return stage1Build ? "해골이 내민 잔을 마시자 상처가 아문다." : "해골이 금화를 밀어 준다.";
                    }
                    if (option == 2)
                        return outcome.CursesToAdd.Count > 0
                            ? "해골이 낄낄거린다. 저주의 낙인이 덱에 새겨진다."
                            : "해골이 혀를 차며 카드 한 장을 던진다.";
                    return "주사위가 굴러 떨어진다. 해골들은 투덜대며 다시 판을 짠다.";
                default:
                    return "";
            }
        }
    }
}
