using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkLegion.Core
{
    /// <summary>export_game_data.py가 쓴 시트 모음 (`38` §2·§3). 없는 시트는 빈 시트로 본다.</summary>
    public sealed class GameData
    {
        private readonly Dictionary<string, DataSheet> _sheets = new Dictionary<string, DataSheet>();

        public GameData(params DataSheet[] sheets)
        {
            foreach (var s in sheets) _sheets[s.Name] = s;
        }

        public IEnumerable<DataSheet> Sheets => _sheets.Values;

        public IReadOnlyList<DataRow> Rows(string sheet) => _sheets.TryGetValue(sheet, out var s) ? s.Rows : (IReadOnlyList<DataRow>)new List<DataRow>();

        /// <summary>시트 이름 → JSON 문자열. 파일 읽기는 호출하는 쪽(에디터 임포터·테스트)이 한다.</summary>
        public static GameData FromJson(IEnumerable<KeyValuePair<string, string>> sheetJson)
        {
            return new GameData(sheetJson.Select(kv => DataSheet.FromJson(kv.Key, kv.Value)).ToArray());
        }
    }

    /// <summary>로더 결과. Errors가 하나라도 있으면 임포터는 SO를 쓰지 않는다 (`38` §5).</summary>
    public sealed class GameDataLoadResult
    {
        public CardCatalog Cards { get; } = new CardCatalog();
        public FusionCatalog Fusion { get; } = new FusionCatalog();
        public EnemyKitCatalog EnemyKits { get; } = new EnemyKitCatalog();
        public BossPatternCatalog BossPatterns { get; } = new BossPatternCatalog();
        public List<string> Errors { get; } = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// 시트 행 → Core 정의 (#35 Unity 비의존 부분). 열 규칙은 `38` §3·§4·§4.1, 읽는 칸은 `01` D-71.
    /// 대상 시트의 모든 행을 읽는다. 1단계 32장 제한은 보상·이벤트 후보에서 한다 (D-44).
    /// 검사(`38` §5)는 중복 ID·잘못된 참조·Up_Params 형식·카드 타입. 동작 핸들러 검사는 임포터가 한다.
    /// </summary>
    public static class GameDataLoader
    {
        /// <summary>Cards_Effect Value1~3의 이름 (`38` §4.1, D-71). null이면 그 칸을 읽지 않는다.</summary>
        public static readonly IReadOnlyDictionary<string, string[]> ValueNames = new Dictionary<string, string[]>
        {
            ["NEC_SKL_001"] = new[] { "DeathThisTurnDraw", null, null },
            ["NEC_SKL_002"] = new[] { "Shield", "FrontBonus", null },
            ["NEC_SKL_003"] = new[] { "Poison", "Weak", null },
            ["NEC_SKL_004"] = new string[] { null, null, null },
            ["NEU_SKL_001"] = new[] { "ExtraAction", null, null },
            ["NEU_SKL_002"] = new string[] { null, null, null },
            ["NEC_ACT_001"] = new[] { "CrystalBonus", null, null },
            ["NEC_ACT_002"] = new[] { "Tokens", "TokenHp", "TokenAtk" },
            ["NEC_ACT_003"] = new string[] { null, null, null },
            ["NEC_ACT_004"] = new[] { "Damage", "HealRatio", null },
            ["NEU_ACT_001"] = new[] { "Draw", "Discard", null },
            ["NEU_ACT_002"] = new[] { "Multiplier", null, null },
        };

        /// <summary>Excel에 백분율 정수로 적힌 수치 (÷100, D-71).</summary>
        private static readonly HashSet<string> PercentNames = new HashSet<string> { "HealRatio" };

        private static readonly Regex DrainNote = new Regex(@"(\d+)%\s*회복");

        public static GameDataLoadResult Load(GameData data)
        {
            var r = new GameDataLoadResult();
            var monToCard = MonsterIdToCard(data, r.Errors);
            LoadCards(data, r);
            LoadFusion(data, monToCard, r);
            LoadEnemies(data, r);
            LoadBosses(data, r);
            return r;
        }

        // ---------- 카드 (`08` Cards_*, `09` Monsters_Master·Monster_Effects) ----------

        private static void LoadCards(GameData data, GameDataLoadResult r)
        {
            var defs = new Dictionary<string, CardDefinition>();
            foreach (var row in data.Rows("Cards_Master"))
            {
                string id = row.Str("ID");
                if (defs.ContainsKey(id))
                {
                    r.Errors.Add($"Cards_Master: duplicate ID {id}");
                    continue;
                }
                if (!TryParseType(row.Str("타입"), out var type))
                {
                    r.Errors.Add($"Cards_Master/{id}: unknown 타입 {row.Str("타입")}");
                    continue;
                }
                var def = new CardDefinition { Id = id, Type = type, Cost = row.Int("Crystal"), Level = row.Int("레벨", 1), Hp = row.Int("HP"), Atk = row.Int("ATK"), Def = row.Int("DEF") };
                defs[id] = def;
                r.Cards.Add(def);
            }

            foreach (var row in Rows(data, "Cards_Effect", "Card_ID", defs, r.Errors))
            {
                var def = defs[row.Str("Card_ID")];
                def.Exhaust = row.Bool("Exhaust");
                if (def.Type == CardType.Monster || !ValueNames.TryGetValue(def.Id, out var names)) continue; // 몬스터 수치는 Monster_Effects (D-71)
                for (int i = 0; i < 3; i++)
                {
                    string name = names[i];
                    if (name == null) continue;
                    double v = row.Num("Value" + (i + 1));
                    def.Params[name] = PercentNames.Contains(name) ? v / 100.0 : v;
                }
            }

            foreach (var row in Rows(data, "Cards_Upgrade", "Card_ID", defs, r.Errors))
            {
                var def = defs[row.Str("Card_ID")];
                if (!TryParseUpParams(row.Str("Up_Params"), out var up))
                {
                    r.Errors.Add($"Cards_Upgrade/{def.Id}: Up_Params format '{row.Str("Up_Params")}'");
                    continue;
                }
                foreach (var kv in up)
                {
                    if (kv.Key == "Cost") def.UpCost = (int)kv.Value;
                    else if (def.Type == CardType.Monster && kv.Key == "HP") def.UpHp = (int)kv.Value;
                    else if (def.Type == CardType.Monster && kv.Key == "ATK") def.UpAtk = (int)kv.Value;
                    else if (def.Type == CardType.Monster && kv.Key == "DEF") def.UpDef = (int)kv.Value;
                    else def.UpParams[kv.Key] = kv.Value;
                }
            }

            foreach (var row in Rows(data, "Cards_Text", "Card_ID", defs, r.Errors))
            {
                if (row.Str("Keyword") == "Ethereal") defs[row.Str("Card_ID")].Ethereal = true;
            }

            foreach (var row in data.Rows("Monsters_Master"))
            {
                string cid = row.Str("Card_ID");
                if (cid.Length == 0) continue; // 카드 없는 융합 전용 몬스터
                if (!defs.TryGetValue(cid, out var def))
                {
                    r.Errors.Add($"Monsters_Master/{row.Str("MON_ID")}: Card_ID {cid} not in Cards_Master");
                    continue;
                }
                if (row.Str("Special").Contains("Soulbound")) def.Soulbound = true;
            }

            var seenValue = new HashSet<string>();
            foreach (var row in data.Rows("Monster_Effects"))
            {
                string cid = row.Str("Card_ID");
                if (!defs.TryGetValue(cid, out var def))
                {
                    r.Errors.Add($"Monster_Effects: Card_ID {cid} not in Cards_Master");
                    continue;
                }
                string effect = row.Str("Effect");
                if (effect == "SacrificeCrystal") def.SacrificeCrystal = row.Int("Value");
                else if (effect == "FusionAsClass") r.Fusion.AddClassMimic(cid);
                else if (row.IsNumber("Value") && seenValue.Add(cid)) def.Params["Value"] = row.Num("Value");
            }
        }

        private static bool TryParseType(string s, out CardType type)
        {
            switch (s)
            {
                case "Monster": type = CardType.Monster; return true;
                case "Skill": type = CardType.Skill; return true;
                case "Action": type = CardType.Action; return true;
                case "Curse": type = CardType.Curse; return true;
                default: type = CardType.Monster; return false;
            }
        }

        /// <summary>`Key=Value;Key=Value` (`38` §4). 빈 문자열은 강화값 없음.</summary>
        public static bool TryParseUpParams(string text, out Dictionary<string, double> values)
        {
            values = new Dictionary<string, double>();
            if (string.IsNullOrWhiteSpace(text)) return true;
            foreach (var part in text.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2 || kv[0].Trim().Length == 0
                    || !double.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return false;
                values[kv[0].Trim()] = v;
            }
            return true;
        }

        /// <summary>키 열이 Cards_Master에 있는 행만 돌려준다. 같은 시트에서 키가 겹치거나 없는 카드를 가리키면 오류.</summary>
        private static IEnumerable<DataRow> Rows(GameData data, string sheet, string keyColumn, Dictionary<string, CardDefinition> defs, List<string> errors)
        {
            var seen = new HashSet<string>();
            var list = new List<DataRow>();
            foreach (var row in data.Rows(sheet))
            {
                string id = row.Str(keyColumn);
                if (!seen.Add(id)) errors.Add($"{sheet}: duplicate {keyColumn} {id}");
                else if (!defs.ContainsKey(id)) errors.Add($"{sheet}: {keyColumn} {id} not in Cards_Master");
                else list.Add(row);
            }
            return list;
        }

        // ---------- 융합 (`09` Fusion_Recipes·Fusion_RandomPool, D-71) ----------

        private static Dictionary<string, string> MonsterIdToCard(GameData data, List<string> errors)
        {
            var map = new Dictionary<string, string>();
            foreach (var row in data.Rows("Monsters_Master"))
            {
                string mon = row.Str("MON_ID");
                if (map.ContainsKey(mon)) errors.Add($"Monsters_Master: duplicate MON_ID {mon}");
                else map[mon] = row.Str("Card_ID");
            }
            return map;
        }

        private static string CardOf(Dictionary<string, string> monToCard, string monId) =>
            monToCard.TryGetValue(monId, out var c) && c.Length > 0 ? c : null;

        private static void LoadFusion(GameData data, Dictionary<string, string> monToCard, GameDataLoadResult r)
        {
            var seen = new HashSet<string>();
            foreach (var row in data.Rows("Fusion_Recipes"))
            {
                string id = row.Str("Recipe_ID");
                if (!seen.Add(id))
                {
                    r.Errors.Add($"Fusion_Recipes: duplicate Recipe_ID {id}");
                    continue;
                }
                if (row.Str("Type") != "Fixed" || row.Int("Weight") <= 0) continue; // Random 행은 설명, Weight 0은 비활성 (D-03·D-71)
                string a = CardOf(monToCard, row.Str("MaterialA")), b = CardOf(monToCard, row.Str("MaterialB")), res = CardOf(monToCard, row.Str("Result_MON_ID"));
                if (a == null || b == null || res == null)
                {
                    r.Errors.Add($"Fusion_Recipes/{id}: material or result has no card ({row.Str("MaterialA")}, {row.Str("MaterialB")} → {row.Str("Result_MON_ID")})");
                    continue;
                }
                r.Fusion.AddFixed(id, a, b, res);
            }

            foreach (var row in data.Rows("Fusion_RandomPool"))
            {
                int weight = row.Int("Weight");
                if (weight <= 0) continue;
                string res = CardOf(monToCard, row.Str("ResultMON_ID"));
                if (res == null)
                {
                    r.Errors.Add($"Fusion_RandomPool/{row.Str("Pool_ID")}: result {row.Str("ResultMON_ID")} has no card");
                    continue;
                }
                r.Fusion.AddPool(row.Str("InputClass"), row.Int("InputLevel"), res, weight);
            }

            // Fallback: 희귀도 C, 같은 클래스, 그 레벨의 몬스터 카드 (`13` §3.4, D-71)
            var commons = data.Rows("Cards_Master")
                .Where(x => x.Str("타입") == "Monster" && x.Str("희귀도") == "C")
                .GroupBy(x => (x.Str("클래스"), x.Int("레벨", 1)));
            foreach (var g in commons) r.Fusion.SetCommonFallback(g.Key.Item1, g.Key.Item2, g.Select(x => x.Str("ID")).Distinct().ToArray());
        }

        // ---------- 적 (`10` Enemies_Master·Enemy_Actions) ----------

        private static void LoadEnemies(GameData data, GameDataLoadResult r)
        {
            var enemies = new HashSet<string>();
            foreach (var row in data.Rows("Enemies_Master"))
            {
                string id = row.Str("ENE_ID");
                if (!enemies.Add(id))
                {
                    r.Errors.Add($"Enemies_Master: duplicate ENE_ID {id}");
                    continue;
                }
                r.EnemyKits.SetSpawnStats(id, row.Int("HP"), row.Int("ATK"), row.Int("DEF"));
            }

            var groups = new Dictionary<string, List<(int order, EnemyActionDef def)>>();
            var order = new List<string>();
            foreach (var row in data.Rows("Enemy_Actions"))
            {
                string id = row.Str("ENE_ID");
                if (!enemies.Contains(id))
                {
                    r.Errors.Add($"Enemy_Actions: ENE_ID {id} not in Enemies_Master");
                    continue;
                }
                string type = row.Str("ActionType");
                var def = new EnemyActionDef
                {
                    ActionId = row.Str("Action_ID"),
                    Condition = row.Str("Condition"),
                    Chance = row.Has("Chance") ? row.Num("Chance") : 1,
                    ActionType = type,
                    Value = row.Num("Value"),
                    Status = row.Str("Status"),
                    Duration = row.Int("Duration"),
                    Target = row.Str("Target"),
                };
                if (type == "Summon")
                {
                    def.SpawnId = row.Str("Value");
                    if (!enemies.Contains(def.SpawnId)) r.Errors.Add($"Enemy_Actions/{id}/{def.ActionId}: summon {def.SpawnId} not in Enemies_Master");
                }
                var m = DrainNote.Match(row.Str("Note"));
                if (m.Success) def.DrainRatio = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) / 100.0;
                if (!groups.TryGetValue(id, out var list))
                {
                    groups[id] = list = new List<(int, EnemyActionDef)>();
                    order.Add(id);
                }
                list.Add((row.Int("Order"), def));
            }
            foreach (var id in order) r.EnemyKits.Add(id, groups[id].OrderBy(x => x.order).Select(x => x.def).ToArray());
        }

        // ---------- 보스 (`14` Boss_List·Boss_TurnCycle, D-65) ----------

        private static void LoadBosses(GameData data, GameDataLoadResult r)
        {
            var enemies = new HashSet<string>(data.Rows("Enemies_Master").Select(x => x.Str("ENE_ID")));
            var bossToEnemy = new Dictionary<string, string>();
            foreach (var row in data.Rows("Boss_List"))
            {
                string boss = row.Str("Boss_ID"), ene = row.Str("ENE_ID");
                if (bossToEnemy.ContainsKey(boss)) r.Errors.Add($"Boss_List: duplicate Boss_ID {boss}");
                else if (!enemies.Contains(ene)) r.Errors.Add($"Boss_List/{boss}: ENE_ID {ene} not in Enemies_Master");
                else bossToEnemy[boss] = ene;
            }

            var groups = new Dictionary<string, List<BossStep>>();
            var order = new List<string>();
            foreach (var row in data.Rows("Boss_TurnCycle"))
            {
                string boss = row.Str("Boss_ID");
                if (!bossToEnemy.TryGetValue(boss, out var ene))
                {
                    r.Errors.Add($"Boss_TurnCycle: Boss_ID {boss} not in Boss_List");
                    continue;
                }
                if (!groups.TryGetValue(ene, out var list))
                {
                    groups[ene] = list = new List<BossStep>();
                    order.Add(ene);
                }
                list.Add(new BossStep(row.Int("Phase"), row.Int("TurnIndex"), row.Str("Action"), row.Int("Damage"),
                    row.Str("StatusEffect"), row.Str("TargetRule"), row.Bool("EnqueueTwice"), row.Str("Note")));
            }
            foreach (var ene in order) r.BossPatterns.Add(ene, groups[ene].ToArray());
        }
    }
}
