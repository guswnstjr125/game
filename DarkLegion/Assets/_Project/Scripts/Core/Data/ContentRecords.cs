using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>몬스터 효과 한 행 (`09` Monster_Effects, D-61). 문장은 해석하지 않는다 (`38` §6).</summary>
    public sealed class MonsterEffectRecord
    {
        public string CardId;
        public string Slot;
        public string Trigger;
        public string Condition;
        public string Effect;
        public string Target;
        public bool HasValue;
        public double Value;
        /// <summary>칸이 문자열이면 그 문구. 숫자는 빈 문자열.</summary>
        public string ValueText;
        public bool HasDuration;
        public int Duration;
        /// <summary>칸이 문자열이면 그 문구(`Battle` 등). 숫자·빈 칸은 빈 문자열.</summary>
        public string DurationText;
        public string Limit;
    }

    /// <summary>`09` Monsters_Master 한 마리 (`38` §3 MonsterData).</summary>
    public sealed class MonsterRecord
    {
        public string Id;
        public string Name;
        public string Class;
        public int Level;
        public string CardId;
        public int Hp;
        public int Atk;
        public int Def;
        public int Crystal;
        public string OnSummon;
        public string Passive;
        public string OnDeath;
        public string OnAttack;
        public string Special;
        public List<MonsterEffectRecord> Effects = new List<MonsterEffectRecord>();
    }

    /// <summary>`14` Boss_Phase의 HP 비율. 전투 페이즈 판정은 BossBrain이 50·20으로 한다.</summary>
    public sealed class BossPhaseRange
    {
        public int Phase;
        public int HpPctMin;
        public int HpPctMax;
    }

    /// <summary>`14` Boss_List·Boss_Phase·Boss_TurnCycle. 골드는 참고 열이고 지급은 Reward_BattleTable (`38` §3).</summary>
    public sealed class BossPatternRecord
    {
        public string BossId;
        public string EnemyId;
        public string Name;
        public int Floor;
        public int Hp;
        public int Atk;
        public int Def;
        public int PhaseCount;
        public string BossRelicId;
        public string CardRewardPool;
        public bool HasGoldReward;
        public int GoldRewardMin;
        public int GoldRewardMax;
        public List<BossPhaseRange> Phases = new List<BossPhaseRange>();
        public List<BossStep> Steps = new List<BossStep>();
    }

    /// <summary>`06` Relics_MVP. Text가 화면 문구이고 Effect는 설계 요약 (`38` §3, D-58).</summary>
    public sealed class RelicRecord
    {
        public string Id;
        public string Name;
        public string Rarity;
        public string Trigger;
        public string Effect;
        public string Text;
    }

    public sealed class EventOptionRecord
    {
        public int Option;
        public string Condition;
        public string Result;
    }

    /// <summary>`06` Events_MVP 선택지와 Event_FloorPool 가중치. 가중치는 내보내기 뒤 소수 (`38` §2).</summary>
    public sealed class EventRecord
    {
        public string Id;
        public string Name;
        public bool InPool;
        public double Floor1;
        public double Floor2;
        public double Floor3;
        public double Floor4;
        public double Floor5;
        public List<EventOptionRecord> Options = new List<EventOptionRecord>();

        public double Weight(int floor)
        {
            switch (floor)
            {
                case 1: return Floor1;
                case 2: return Floor2;
                case 3: return Floor3;
                case 4: return Floor4;
                case 5: return Floor5;
                default: return 0;
            }
        }
    }

    /// <summary>`08` Curse_Cards. RemoveCost는 시트 문구 그대로 (`80G`). 빈 문자열은 칸이 비어 있음.</summary>
    public sealed class CurseRecord
    {
        public string Id;
        public string Name;
        public int Cost;
        public string Effect;
        public bool Removable;
        public string RemoveCost;
        public double Value1;
        public bool HasDuration;
        public int Duration;
        public string Keyword;
    }

    public sealed class KeywordRecord
    {
        public string Id;
        public string Term;
        public string Name;
        public string Tooltip;
    }

    /// <summary>`09` Fusion_Recipes. 고정 레시피는 Type Fixed이고 Weight &gt; 0 (D-03·D-71).</summary>
    public sealed class FusionRecipeRecord
    {
        public string Id;
        public string MaterialA;
        public string MaterialB;
        public string ResultMonsterId;
        public string Type;
        public int Weight;
        public bool IsFixedRecipe => Type == "Fixed" && Weight > 0;
    }

    public sealed class FusionPoolRecord
    {
        public string Id;
        public string InputClass;
        public int InputLevel;
        public string OutputClass;
        public int OutputLevel;
        public string ResultMonsterId;
        public int Weight;
    }

    public sealed class RewardBattleRecord
    {
        public string BattleType;
        public int Floor;
        public int GoldMin;
        public int GoldMax;
        public double Common;
        public double Uncommon;
        public double Rare;
        public double Legendary;
    }

    public sealed class EliteRelicRecord
    {
        public int Floor;
        public string PoolA;
        public string PoolB;
        public string PoolC;
        public string RarityRule;
    }

    /// <summary>
    /// `12` Status_Master. DurationUnspecified는 DurationTurns 칸이 N인 경우(JSON false)다.
    /// 그 칸을 0턴으로 바꾸지 않는다.
    /// </summary>
    public sealed class StatusRecord
    {
        public string Id;
        public string Name;
        public string StackType;
        public string TickTiming;
        public string Effect;
        public bool HasMaxStack;
        public int MaxStack;
        public bool HasDuration;
        public int DurationTurns;
        public bool DurationUnspecified;
    }

    /// <summary>`04` Player_Stats 시작 능력치.</summary>
    public sealed class ClassStartRecord
    {
        public string ClassName;
        public int StartHp;
        public int MaxHp;
        public int StartGold;
        public int StartCrystal;
    }

    public sealed class ContentImportResult
    {
        public List<MonsterRecord> Monsters = new List<MonsterRecord>();
        public List<BossPatternRecord> Bosses = new List<BossPatternRecord>();
        public List<RelicRecord> Relics = new List<RelicRecord>();
        public List<EventRecord> Events = new List<EventRecord>();
        public List<CurseRecord> Curses = new List<CurseRecord>();
        public List<KeywordRecord> Keywords = new List<KeywordRecord>();
        public List<FusionRecipeRecord> FusionRecipes = new List<FusionRecipeRecord>();
        public List<FusionPoolRecord> FusionPools = new List<FusionPoolRecord>();
        public List<RewardBattleRecord> Rewards = new List<RewardBattleRecord>();
        public List<EliteRelicRecord> EliteRelics = new List<EliteRelicRecord>();
        public List<StatusRecord> Statuses = new List<StatusRecord>();
        public List<ClassStartRecord> ClassStarts = new List<ClassStartRecord>();
        public List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }
}
