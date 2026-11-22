using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DarkLegion.Core
{
    /// <summary>
    /// 1단계에 남은 시트 → 기록 (`38` §3·§5). 전투 실행은 바꾸지 않는다.
    /// 시트 파일이 없으면 그 종류는 비어 있다. 있는 시트만 검사한다.
    /// </summary>
    public static class ContentImport
    {
        private static readonly Regex IdPattern = new Regex(@"\A[A-Z0-9_]+\z");
        private const double SumTolerance = 1e-6;

        public static ContentImportResult Read(GameData data)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            var result = new ContentImportResult();
            ReadMonsters(data, result);
            ReadBosses(data, result);
            ReadRelics(data, result);
            ReadEvents(data, result);
            ReadCurses(data, result);
            ReadKeywords(data, result);
            ReadFusion(data, result);
            ReadRewards(data, result);
            ReadStatuses(data, result);
            ReadClassStarts(data, result);
            return result;
        }

        private static void ReadMonsters(GameData data, ContentImportResult result)
        {
            if (!HasSheet(data, "Monsters_Master")) return;
            var cards = Ids(data, "Cards_Master", "ID");
            bool checkCards = HasSheet(data, "Cards_Master");
            var byId = new Dictionary<string, MonsterRecord>();
            var byCard = new Dictionary<string, List<MonsterRecord>>();
            foreach (var row in data.Rows("Monsters_Master"))
            {
                string id = row.Str("MON_ID");
                string at = "Monsters_Master/" + id;
                if (!ValidId(id))
                {
                    result.Errors.Add(at + ": 유효하지 않은 MON_ID");
                    continue;
                }
                if (byId.ContainsKey(id))
                {
                    result.Errors.Add(at + ": 중복 MON_ID");
                    continue;
                }
                bool hpOk = TryInt(row, "HP", 1, int.MaxValue, out int hp);
                bool atkOk = TryInt(row, "ATK", 0, int.MaxValue, out int atk);
                bool defOk = TryInt(row, "DEF", 0, int.MaxValue, out int def);
                bool crystalOk = TryInt(row, "Crystal", 0, int.MaxValue, out int crystal);
                bool levelOk = TryInt(row, "Level", 1, int.MaxValue, out int level);
                if (!hpOk) result.Errors.Add(at + ": HP 범위 오류");
                if (!atkOk || !defOk) result.Errors.Add(at + ": ATK 또는 DEF 범위 오류");
                if (!crystalOk) result.Errors.Add(at + ": Crystal 범위 오류");
                if (!levelOk) result.Errors.Add(at + ": Level 범위 오류");
                string name = row.Str("Name_KO");
                if (name.Length == 0) result.Errors.Add(at + ": Name_KO가 비어 있습니다.");
                string card = row.Str("Card_ID");
                if (card.Length > 0 && checkCards && !cards.Contains(card))
                    result.Errors.Add(at + ": Card_ID " + card + "가 Cards_Master에 없습니다.");
                var record = new MonsterRecord
                {
                    Id = id, Name = name, Class = row.Str("Class"), Level = level, CardId = card,
                    Hp = hp, Atk = atk, Def = def, Crystal = crystal,
                    OnSummon = row.Str("OnSummon"), Passive = row.Str("Passive"), OnDeath = row.Str("OnDeath"),
                    OnAttack = row.Str("OnAttack"), Special = row.Str("Special")
                };
                byId[id] = record;
                result.Monsters.Add(record);
                if (card.Length > 0)
                {
                    if (!byCard.TryGetValue(card, out var list)) byCard[card] = list = new List<MonsterRecord>();
                    list.Add(record);
                }
            }
            if (!HasSheet(data, "Monster_Effects")) return;
            foreach (var row in data.Rows("Monster_Effects"))
            {
                string card = row.Str("Card_ID");
                string at = "Monster_Effects/" + card;
                if (!byCard.TryGetValue(card, out var owners))
                {
                    result.Errors.Add(at + ": 몬스터 Card_ID가 없습니다.");
                    continue;
                }
                var effect = new MonsterEffectRecord
                {
                    CardId = card, Slot = row.Str("Slot"), Trigger = row.Str("Trigger"), Condition = row.Str("Condition"),
                    Effect = row.Str("Effect"), Target = row.Str("Target"), Limit = row.Str("Limit"),
                    ValueText = "", DurationText = ""
                };
                if (row.Has("Value"))
                {
                    if (row.IsNumber("Value"))
                    {
                        effect.HasValue = true;
                        effect.Value = row.Num("Value");
                    }
                    else if (IsBool(row, "Value")) result.Errors.Add(at + ": Value 형식 오류");
                    else effect.ValueText = row.Str("Value");
                }
                if (row.Has("Duration"))
                {
                    if (row.IsNumber("Duration"))
                    {
                        if (!TryInt(row, "Duration", 0, int.MaxValue, out int duration)) result.Errors.Add(at + ": Duration 범위 오류");
                        else
                        {
                            effect.HasDuration = true;
                            effect.Duration = duration;
                        }
                    }
                    else if (IsBool(row, "Duration")) result.Errors.Add(at + ": Duration 형식 오류");
                    else effect.DurationText = row.Str("Duration");
                }
                foreach (var owner in owners) owner.Effects.Add(effect);
            }
        }

        private static void ReadBosses(GameData data, ContentImportResult result)
        {
            if (!HasSheet(data, "Boss_List")) return;
            var enemies = Ids(data, "Enemies_Master", "ENE_ID");
            bool checkEnemies = HasSheet(data, "Enemies_Master");
            var relics = Ids(data, "Relics_MVP", "Relic_ID");
            bool checkRelics = HasSheet(data, "Relics_MVP");
            var curses = Ids(data, "Curse_Cards", "Curse_ID");
            bool checkCurses = HasSheet(data, "Curse_Cards");
            var byId = new Dictionary<string, BossPatternRecord>();
            foreach (var row in data.Rows("Boss_List"))
            {
                string id = row.Str("Boss_ID");
                string at = "Boss_List/" + id;
                if (!ValidId(id))
                {
                    result.Errors.Add(at + ": 유효하지 않은 Boss_ID");
                    continue;
                }
                if (byId.ContainsKey(id))
                {
                    result.Errors.Add(at + ": 중복 Boss_ID");
                    continue;
                }
                string enemy = row.Str("ENE_ID");
                if (!ValidId(enemy) || (checkEnemies && !enemies.Contains(enemy)))
                    result.Errors.Add(at + ": ENE_ID " + enemy + "가 Enemies_Master에 없습니다.");
                bool hpOk = TryInt(row, "HP", 1, int.MaxValue, out int hp);
                bool atkOk = TryInt(row, "ATK", 0, int.MaxValue, out int atk);
                bool defOk = TryInt(row, "DEF", 0, int.MaxValue, out int def);
                bool floorOk = TryInt(row, "Floor", 1, int.MaxValue, out int floor);
                bool phaseOk = TryInt(row, "Phase_Count", 1, int.MaxValue, out int phases);
                if (!hpOk) result.Errors.Add(at + ": HP 범위 오류");
                if (!atkOk || !defOk) result.Errors.Add(at + ": ATK 또는 DEF 범위 오류");
                if (!floorOk) result.Errors.Add(at + ": Floor 범위 오류");
                if (!phaseOk) result.Errors.Add(at + ": Phase_Count 범위 오류");
                string relic = row.Str("BossRelic_ID");
                if (relic.Length > 0 && checkRelics && !relics.Contains(relic))
                    result.Errors.Add(at + ": BossRelic_ID " + relic + "가 Relics_MVP에 없습니다.");
                var record = new BossPatternRecord
                {
                    BossId = id, EnemyId = enemy, Name = row.Str("Name_KO"), Floor = floor, Hp = hp, Atk = atk, Def = def,
                    PhaseCount = phases, BossRelicId = relic, CardRewardPool = row.Str("CardReward_Pool")
                };
                if (row.Has("GoldReward_Min") || row.Has("GoldReward_Max"))
                {
                    bool minOk = TryInt(row, "GoldReward_Min", 0, int.MaxValue, out int min);
                    bool maxOk = TryInt(row, "GoldReward_Max", 0, int.MaxValue, out int max);
                    if (!minOk || !maxOk || min > max)
                        result.Errors.Add(at + ": GoldReward 범위 오류");
                    else
                    {
                        record.HasGoldReward = true;
                        record.GoldRewardMin = min;
                        record.GoldRewardMax = max;
                    }
                }
                byId[id] = record;
                result.Bosses.Add(record);
            }
            if (HasSheet(data, "Boss_Phase"))
            {
                var seen = new HashSet<(string, int)>();
                foreach (var row in data.Rows("Boss_Phase"))
                {
                    string id = row.Str("Boss_ID");
                    string at = "Boss_Phase/" + id + "/" + row.Str("Phase");
                    if (!byId.TryGetValue(id, out var boss))
                    {
                        result.Errors.Add(at + ": Boss_List에 없습니다.");
                        continue;
                    }
                    if (!TryInt(row, "Phase", 1, int.MaxValue, out int phase) || !seen.Add((id, phase)))
                    {
                        result.Errors.Add(at + ": 잘못되었거나 중복된 Phase");
                        continue;
                    }
                    if (!TryInt(row, "HP_Pct_Min", 0, 100, out int min) || !TryInt(row, "HP_Pct_Max", 0, 100, out int max) || min > max)
                    {
                        result.Errors.Add(at + ": HP 비율 범위 오류");
                        continue;
                    }
                    boss.Phases.Add(new BossPhaseRange { Phase = phase, HpPctMin = min, HpPctMax = max });
                }
                foreach (var boss in result.Bosses)
                    if (boss.Phases.Count != boss.PhaseCount)
                        result.Errors.Add("Boss_Phase/" + boss.BossId + ": Phase_Count " + boss.PhaseCount + "와 행 수 " + boss.Phases.Count + "가 다릅니다.");
            }
            if (!HasSheet(data, "Boss_TurnCycle")) return;
            foreach (var row in data.Rows("Boss_TurnCycle"))
            {
                string id = row.Str("Boss_ID");
                string at = "Boss_TurnCycle/" + id + "/P" + row.Str("Phase") + "T" + row.Str("TurnIndex");
                if (!byId.TryGetValue(id, out var boss))
                {
                    result.Errors.Add(at + ": Boss_List에 없습니다.");
                    continue;
                }
                if (!TryInt(row, "Phase", 1, int.MaxValue, out int phase) || !TryInt(row, "TurnIndex", 0, int.MaxValue, out int turn))
                {
                    result.Errors.Add(at + ": Phase 또는 TurnIndex 범위 오류");
                    continue;
                }
                if (!TryInt(row, "Damage", 0, int.MaxValue, out int damage)) result.Errors.Add(at + ": Damage 범위 오류");
                string action = row.Str("Action");
                if (action.Length == 0) result.Errors.Add(at + ": Action이 비어 있습니다.");
                if (row.Has("OnceOnPhaseEnter") && !IsBool(row, "OnceOnPhaseEnter"))
                    result.Errors.Add(at + ": OnceOnPhaseEnter 형식 오류");
                if (row.Has("EnqueueTwice") && !IsBool(row, "EnqueueTwice"))
                    result.Errors.Add(at + ": EnqueueTwice 형식 오류");
                int threshold = 0;
                if (row.Has("ThresholdPct") && !TryInt(row, "ThresholdPct", 0, 100, out threshold))
                    result.Errors.Add(at + ": ThresholdPct 범위 오류");
                string curse = row.Str("CurseId");
                if (curse.Length > 0 && checkCurses && !curses.Contains(curse))
                    result.Errors.Add(at + ": CurseId " + curse + "가 Curse_Cards에 없습니다.");
                boss.Steps.Add(new BossStep(phase, turn, action, damage, row.Str("StatusEffect"), row.Str("TargetRule"),
                    row.Bool("EnqueueTwice"), row.Str("Note"), row.Bool("OnceOnPhaseEnter"), threshold, curse));
            }
            foreach (var boss in result.Bosses)
                if (boss.Steps.Count == 0) result.Errors.Add("Boss_TurnCycle/" + boss.BossId + ": 행동 행이 없습니다.");
        }

        private static void ReadRelics(GameData data, ContentImportResult result)
        {
            if (!HasSheet(data, "Relics_MVP")) return;
            var seen = new HashSet<string>();
            foreach (var row in data.Rows("Relics_MVP"))
            {
                string id = row.Str("Relic_ID");
                string at = "Relics_MVP/" + id;
                if (!ValidId(id) || !seen.Add(id))
                {
                    result.Errors.Add(at + ": 유효하지 않거나 중복된 Relic_ID");
                    continue;
                }
                if (row.Str("Name_KO").Length == 0 || row.Str("Text_KO").Length == 0)
                    result.Errors.Add(at + ": Name_KO 또는 Text_KO가 비어 있습니다.");
                result.Relics.Add(new RelicRecord
                {
                    Id = id, Name = row.Str("Name_KO"), Rarity = row.Str("Rarity"), Trigger = row.Str("Trigger"),
                    Effect = row.Str("Effect"), Text = row.Str("Text_KO")
                });
            }
            foreach (string id in Stage1Scope.Relics)
            {
                if (!seen.Contains(id)) result.Errors.Add("Relics_MVP: 1단계 유물 " + id + "가 없습니다.");
                else if (!HandlerRegistry.Has(HandlerKind.Relic, id, data))
                    result.Errors.Add("Relics_MVP/" + id + ": 1단계 핸들러가 없습니다.");
            }
        }

        private static void ReadEvents(GameData data, ContentImportResult result)
        {
            var byId = new Dictionary<string, EventRecord>();
            if (HasSheet(data, "Events_MVP"))
            {
                foreach (var row in data.Rows("Events_MVP"))
                {
                    string id = row.Str("Event_ID");
                    string at = "Events_MVP/" + id;
                    if (!ValidId(id))
                    {
                        result.Errors.Add(at + ": 유효하지 않은 Event_ID");
                        continue;
                    }
                    if (!byId.TryGetValue(id, out var ev))
                    {
                        if (row.Str("Name_KO").Length == 0) result.Errors.Add(at + ": Name_KO가 비어 있습니다.");
                        ev = new EventRecord { Id = id, Name = row.Str("Name_KO") };
                        byId[id] = ev;
                        result.Events.Add(ev);
                    }
                    if (!TryInt(row, "Option", 1, int.MaxValue, out int option))
                    {
                        result.Errors.Add(at + ": Option 범위 오류");
                        continue;
                    }
                    if (ev.Options.Any(item => item.Option == option))
                    {
                        result.Errors.Add(at + ": 중복 Option " + option);
                        continue;
                    }
                    ev.Options.Add(new EventOptionRecord { Option = option, Condition = row.Str("Condition"), Result = row.Str("Result") });
                }
                foreach (string id in Stage1Scope.Events)
                {
                    if (!byId.ContainsKey(id)) result.Errors.Add("Events_MVP: 1단계 이벤트 " + id + "가 없습니다.");
                    else if (!HandlerRegistry.Has(HandlerKind.Event, id, data))
                        result.Errors.Add("Events_MVP/" + id + ": 1단계 핸들러가 없습니다.");
                }
            }
            if (!HasSheet(data, "Event_FloorPool")) return;
            var sums = new double[6];
            foreach (var row in data.Rows("Event_FloorPool"))
            {
                string id = row.Str("Event_ID");
                string at = "Event_FloorPool/" + id;
                if (!byId.TryGetValue(id, out var ev))
                {
                    result.Errors.Add(at + ": Events_MVP에 없습니다.");
                    continue;
                }
                if (ev.InPool)
                {
                    result.Errors.Add(at + ": 중복 Event_ID");
                    continue;
                }
                ev.InPool = true;
                var weights = new double[5];
                bool ok = true;
                for (int floor = 1; floor <= 5; floor++)
                {
                    if (!TryUnit(row, "Floor_" + floor, out weights[floor - 1]))
                    {
                        result.Errors.Add(at + ": Floor_" + floor + " 범위 오류");
                        ok = false;
                    }
                }
                if (!ok) continue;
                ev.Floor1 = weights[0];
                ev.Floor2 = weights[1];
                ev.Floor3 = weights[2];
                ev.Floor4 = weights[3];
                ev.Floor5 = weights[4];
                for (int floor = 1; floor <= 5; floor++) sums[floor] += weights[floor - 1];
            }
            for (int floor = 1; floor <= 5; floor++)
                if (System.Math.Abs(sums[floor] - 1.0) > SumTolerance)
                    result.Errors.Add("Event_FloorPool/Floor_" + floor + ": 합 " + sums[floor] + " != 1");
        }

        private static void ReadCurses(GameData data, ContentImportResult result)
        {
            if (!HasSheet(data, "Curse_Cards")) return;
            var seen = new HashSet<string>();
            foreach (var row in data.Rows("Curse_Cards"))
            {
                string id = row.Str("Curse_ID");
                string at = "Curse_Cards/" + id;
                if (!ValidId(id) || !seen.Add(id))
                {
                    result.Errors.Add(at + ": 유효하지 않거나 중복된 Curse_ID");
                    continue;
                }
                if (!TryInt(row, "Cost", 0, int.MaxValue, out int cost)) result.Errors.Add(at + ": Cost 범위 오류");
                if (!IsBool(row, "Removable")) result.Errors.Add(at + ": Removable 형식 오류");
                if (!row.IsNumber("Value1")) result.Errors.Add(at + ": Value1은 숫자여야 합니다.");
                var curse = new CurseRecord
                {
                    Id = id, Name = row.Str("Name_KO"), Cost = cost, Effect = row.Str("Effect"),
                    Removable = row.Bool("Removable"), RemoveCost = row.Str("Remove_Cost"),
                    Value1 = row.Num("Value1"), Keyword = row.Str("Keyword")
                };
                if (row.Has("Duration"))
                {
                    if (!TryInt(row, "Duration", 0, int.MaxValue, out int duration)) result.Errors.Add(at + ": Duration 범위 오류");
                    else
                    {
                        curse.HasDuration = true;
                        curse.Duration = duration;
                    }
                }
                result.Curses.Add(curse);
            }
            foreach (string id in Stage1Scope.Curses)
            {
                if (!seen.Contains(id)) result.Errors.Add("Curse_Cards: 1단계 저주 " + id + "가 없습니다.");
                else if (!HandlerRegistry.Has(HandlerKind.Curse, id, data))
                    result.Errors.Add("Curse_Cards/" + id + ": 1단계 핸들러가 없습니다.");
            }
        }

        private static void ReadKeywords(GameData data, ContentImportResult result)
        {
            if (!HasSheet(data, "Keywords")) return;
            var seen = new HashSet<string>();
            foreach (var row in data.Rows("Keywords"))
            {
                string id = row.Str("Keyword_ID");
                string at = "Keywords/" + id;
                if (!ValidId(id) || !seen.Add(id))
                {
                    result.Errors.Add(at + ": 유효하지 않거나 중복된 Keyword_ID");
                    continue;
                }
                if (row.Str("Term").Length == 0 || row.Str("Name_KO").Length == 0 || row.Str("Tooltip_KO").Length == 0)
                    result.Errors.Add(at + ": Term·Name_KO·Tooltip_KO가 비어 있습니다.");
                result.Keywords.Add(new KeywordRecord
                {
                    Id = id, Term = row.Str("Term"), Name = row.Str("Name_KO"), Tooltip = row.Str("Tooltip_KO")
                });
            }
        }

        private static void ReadFusion(GameData data, ContentImportResult result)
        {
            var monsters = Ids(data, "Monsters_Master", "MON_ID");
            bool check = HasSheet(data, "Monsters_Master");
            if (HasSheet(data, "Fusion_Recipes"))
            {
                var seen = new HashSet<string>();
                foreach (var row in data.Rows("Fusion_Recipes"))
                {
                    string id = row.Str("Recipe_ID");
                    string at = "Fusion_Recipes/" + id;
                    if (!ValidId(id) || !seen.Add(id))
                    {
                        result.Errors.Add(at + ": 유효하지 않거나 중복된 Recipe_ID");
                        continue;
                    }
                    string type = row.Str("Type");
                    if (type != "Fixed" && type != "Random") result.Errors.Add(at + ": Type은 Fixed 또는 Random이어야 합니다.");
                    if (!TryInt(row, "Weight", 0, int.MaxValue, out int weight)) result.Errors.Add(at + ": Weight 범위 오류");
                    string a = row.Str("MaterialA"), b = row.Str("MaterialB"), res = row.Str("Result_MON_ID");
                    if (check && (!monsters.Contains(a) || !monsters.Contains(b) || !monsters.Contains(res)))
                        result.Errors.Add(at + ": 재료 또는 결과가 Monsters_Master에 없습니다.");
                    result.FusionRecipes.Add(new FusionRecipeRecord
                    {
                        Id = id, MaterialA = a, MaterialB = b, ResultMonsterId = res, Type = type, Weight = weight
                    });
                }
            }
            if (!HasSheet(data, "Fusion_RandomPool")) return;
            var pools = new HashSet<string>();
            foreach (var row in data.Rows("Fusion_RandomPool"))
            {
                string id = row.Str("Pool_ID");
                string at = "Fusion_RandomPool/" + id;
                if (!ValidId(id) || !pools.Add(id))
                {
                    result.Errors.Add(at + ": 유효하지 않거나 중복된 Pool_ID");
                    continue;
                }
                bool inputOk = TryInt(row, "InputLevel", 1, int.MaxValue, out int input);
                bool outputOk = TryInt(row, "OutputLevel", 1, int.MaxValue, out int output);
                if (!inputOk || !outputOk) result.Errors.Add(at + ": Level 범위 오류");
                if (!TryInt(row, "Weight", 0, int.MaxValue, out int weight)) result.Errors.Add(at + ": Weight 범위 오류");
                string res = row.Str("ResultMON_ID");
                if (check && !monsters.Contains(res)) result.Errors.Add(at + ": 결과 " + res + "가 Monsters_Master에 없습니다.");
                result.FusionPools.Add(new FusionPoolRecord
                {
                    Id = id, InputClass = row.Str("InputClass"), InputLevel = input, OutputClass = row.Str("OutputClass"),
                    OutputLevel = output, ResultMonsterId = res, Weight = weight
                });
            }
        }

        private static void ReadRewards(GameData data, ContentImportResult result)
        {
            var relics = Ids(data, "Relics_MVP", "Relic_ID");
            bool check = HasSheet(data, "Relics_MVP");
            if (HasSheet(data, "Reward_BattleTable"))
            {
                var seen = new HashSet<(string, int)>();
                foreach (var row in data.Rows("Reward_BattleTable"))
                {
                    string type = row.Str("BattleType");
                    string at = "Reward_BattleTable/" + type + "/" + row.Str("Floor");
                    if (type != "Normal" && type != "Elite" && type != "Boss")
                    {
                        result.Errors.Add(at + ": BattleType 범위 오류");
                        continue;
                    }
                    if (!TryInt(row, "Floor", 1, int.MaxValue, out int floor) || !seen.Add((type, floor)))
                    {
                        result.Errors.Add(at + ": 잘못되었거나 중복된 Floor");
                        continue;
                    }
                    bool minOk = TryInt(row, "GoldMin", 0, int.MaxValue, out int min);
                    bool maxOk = TryInt(row, "GoldMax", 0, int.MaxValue, out int max);
                    if (!minOk || !maxOk || min > max) result.Errors.Add(at + ": Gold 범위 오류");
                    bool commonOk = TryUnit(row, "Drop_Common", out double common);
                    bool uncommonOk = TryUnit(row, "Drop_Uncommon", out double uncommon);
                    bool rareOk = TryUnit(row, "Drop_Rare", out double rare);
                    bool legendaryOk = TryUnit(row, "Drop_Legendary", out double legendary);
                    if (!commonOk || !uncommonOk || !rareOk || !legendaryOk
                        || System.Math.Abs(common + uncommon + rare + legendary - 1.0) > SumTolerance)
                        result.Errors.Add(at + ": 희귀도 합 오류");
                    result.Rewards.Add(new RewardBattleRecord
                    {
                        BattleType = type, Floor = floor, GoldMin = min, GoldMax = max,
                        Common = common, Uncommon = uncommon, Rare = rare, Legendary = legendary
                    });
                }
            }
            if (!HasSheet(data, "Reward_EliteRelic")) return;
            var floors = new HashSet<int>();
            foreach (var row in data.Rows("Reward_EliteRelic"))
            {
                string at = "Reward_EliteRelic/" + row.Str("Floor");
                if (!TryInt(row, "Floor", 1, int.MaxValue, out int floor) || !floors.Add(floor))
                {
                    result.Errors.Add(at + ": 잘못되었거나 중복된 Floor");
                    continue;
                }
                string a = row.Str("Relic_Pool_A"), b = row.Str("Relic_Pool_B"), c = row.Str("Relic_Pool_C");
                foreach (string id in new[] { a, b, c })
                    if (id.Length > 0 && check && !relics.Contains(id))
                        result.Errors.Add(at + ": " + id + "가 Relics_MVP에 없습니다.");
                result.EliteRelics.Add(new EliteRelicRecord { Floor = floor, PoolA = a, PoolB = b, PoolC = c, RarityRule = row.Str("Rarity_Rule") });
            }
        }

        private static void ReadStatuses(GameData data, ContentImportResult result)
        {
            if (!HasSheet(data, "Status_Master")) return;
            var seen = new HashSet<string>();
            foreach (var row in data.Rows("Status_Master"))
            {
                string id = row.Str("Status_ID");
                string at = "Status_Master/" + id;
                if (!ValidId(id) || !seen.Add(id))
                {
                    result.Errors.Add(at + ": 유효하지 않거나 중복된 Status_ID");
                    continue;
                }
                if (row.Str("Name_KO").Length == 0 || row.Str("StackType").Length == 0)
                    result.Errors.Add(at + ": Name_KO 또는 StackType이 비어 있습니다.");
                var status = new StatusRecord
                {
                    Id = id, Name = row.Str("Name_KO"), StackType = row.Str("StackType"),
                    TickTiming = row.Str("TickTiming"), Effect = row.Str("Effect")
                };
                if (row.Has("MaxStack"))
                {
                    if (!TryInt(row, "MaxStack", 1, int.MaxValue, out int max)) result.Errors.Add(at + ": MaxStack 범위 오류");
                    else
                    {
                        status.HasMaxStack = true;
                        status.MaxStack = max;
                    }
                }
                if (!row.Has("DurationTurns"))
                {
                    status.HasDuration = false;
                }
                else if (IsBool(row, "DurationTurns") && !row.Bool("DurationTurns")) status.DurationUnspecified = true;
                else if (TryInt(row, "DurationTurns", 0, int.MaxValue, out int duration))
                {
                    status.HasDuration = true;
                    status.DurationTurns = duration;
                }
                else result.Errors.Add(at + ": DurationTurns 형식 오류");
                result.Statuses.Add(status);
            }
        }

        private static void ReadClassStarts(GameData data, ContentImportResult result)
        {
            if (!HasSheet(data, "Player_Stats")) return;
            var seen = new HashSet<string>();
            foreach (var row in data.Rows("Player_Stats"))
            {
                string id = row.Str("Class");
                string at = "Player_Stats/" + id;
                if (!ValidId(id) || !seen.Add(id))
                {
                    result.Errors.Add(at + ": 유효하지 않거나 중복된 Class");
                    continue;
                }
                bool hpOk = TryInt(row, "StartHP", 1, int.MaxValue, out int hp);
                bool maxOk = TryInt(row, "MaxHP", 1, int.MaxValue, out int max);
                if (!hpOk || !maxOk || max < hp) result.Errors.Add(at + ": HP 범위 오류");
                bool goldOk = TryInt(row, "StartGold", 0, int.MaxValue, out int gold);
                bool crystalOk = TryInt(row, "StartCrystal", 0, int.MaxValue, out int crystal);
                if (!goldOk || !crystalOk) result.Errors.Add(at + ": Gold 또는 Crystal 범위 오류");
                result.ClassStarts.Add(new ClassStartRecord { ClassName = id, StartHp = hp, MaxHp = max, StartGold = gold, StartCrystal = crystal });
            }
        }

        private static bool HasSheet(GameData data, string name)
        {
            foreach (var sheet in data.Sheets)
                if (sheet.Name == name) return true;
            return false;
        }

        private static HashSet<string> Ids(GameData data, string sheet, string column)
        {
            var ids = new HashSet<string>();
            foreach (var row in data.Rows(sheet)) ids.Add(row.Str(column));
            return ids;
        }

        private static bool ValidId(string id) => id.Length > 0 && IdPattern.IsMatch(id);

        private static bool TryInt(DataRow row, string column, int minimum, int maximum, out int value)
        {
            value = 0;
            if (!row.IsNumber(column)) return false;
            double number = row.Num(column);
            if (number < minimum || number > maximum || number != System.Math.Truncate(number)) return false;
            value = (int)number;
            return true;
        }

        private static bool TryUnit(DataRow row, string column, out double value)
        {
            value = 0;
            if (!row.IsNumber(column)) return false;
            value = row.Num(column);
            return value >= 0 && value <= 1 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsBool(DataRow row, string column)
        {
            if (!row.Has(column) || row.IsNumber(column)) return false;
            string text = row.Str(column);
            return text == "true" || text == "false";
        }
    }
}
