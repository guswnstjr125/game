using System.Collections.Generic;

namespace DarkLegion.Core
{
    /// <summary>몬스터 커맨드 (D-27). 스킬 커맨드는 Post-MVP.</summary>
    public enum UnitCommand
    {
        Attack,
        Defend
    }

    public enum TeamType
    {
        Player,
        Enemy,
        Neutral
    }

    /// <summary>유닛별 특수 플래그 (`06` §7). Bind는 상태이상이고 Frozen은 플래그다.</summary>
    public sealed class RuntimeBattleFlags
    {
        public bool Invincible;
        public bool Untargetable;
        public bool Silenced;
        public bool Frozen;
        public bool CannotHeal;
        public bool CannotDraw;
    }

    /// <summary>플레이어·적·소환수 공통 부모 (`06` §4). 데이터(SO)와 분리된 전투 중 상태만 가진다.</summary>
    public abstract class RuntimeUnit
    {
        public string UnitId { get; }
        public string DataId { get; }
        public TeamType Team { get; }

        public RuntimeStat MaxHP { get; }
        public RuntimeStat Attack { get; }
        public RuntimeStat Defense { get; }

        public int CurrentHP { get; set; }
        /// <summary>턴 종료 시 전량 소멸 (`06` §11.2).</summary>
        public int Shield { get; set; }

        public bool IsDead { get; private set; }
        public bool IsTargetable => !IsDead && !Flags.Untargetable;
        public RuntimeBattleFlags Flags { get; } = new RuntimeBattleFlags();
        public StatusContainer Statuses { get; }
        public List<string> Tags { get; } = new List<string>();

        /// <summary>위치 DEF 보너스 (Front +1, `04` Position_Bonus). 슬롯 배치(#42)가 넣는다.</summary>
        public int PositionDefBonus { get; set; }

        /// <summary>피해 계산에 쓰는 DEF (D-01: FinalDef + Front 보너스).</summary>
        public virtual int EffectiveDefense => Defense.FinalValue + PositionDefBonus;

        protected RuntimeUnit(string unitId, string dataId, TeamType team, int maxHp, int attack, int defense)
        {
            UnitId = unitId;
            DataId = dataId;
            Team = team;
            MaxHP = new RuntimeStat(maxHp);
            Attack = new RuntimeStat(attack);
            Defense = new RuntimeStat(defense);
            CurrentHP = maxHp;
            Statuses = new StatusContainer(this);
        }

        /// <summary>HP ≤ 0이 된 순간 한 번만 true. 슬롯은 DeathAction이 실행될 때까지 유지한다 (`06` §12).</summary>
        internal bool MarkDead()
        {
            if (IsDead) return false;
            IsDead = true;
            return true;
        }

        public override string ToString() => $"{UnitId}({DataId}) HP{CurrentHP}/{MaxHP.FinalValue}";
    }

    public sealed class RuntimePlayer : RuntimeUnit
    {
        public const int DefaultMaxCrystal = 3;

        public int CurrentCrystal { get; set; }
        public int MaxCrystal { get; set; } = DefaultMaxCrystal;
        public RuntimeDeck Deck { get; }

        /// <summary>플레이어 DEF는 0 (D-01). Shield만 적용된다.</summary>
        public override int EffectiveDefense => 0;

        public RuntimePlayer(string unitId, int maxHp, RuntimeDeck deck)
            : base(unitId, "PLAYER", TeamType.Player, maxHp, 0, 0)
        {
            Deck = deck;
        }

        /// <summary>턴 시작: MaxCrystal로 리셋, 이월 없음. 턴 중 상한은 없다 (D-30).</summary>
        public void ResetCrystalForTurn()
        {
            CurrentCrystal = MaxCrystal;
        }

        public bool TrySpendCrystal(int cost)
        {
            if (cost < 0 || CurrentCrystal < cost) return false;
            CurrentCrystal -= cost;
            return true;
        }
    }

    public sealed class RuntimeEnemy : RuntimeUnit
    {
        public bool IsBoss { get; }
        /// <summary>이 적을 소환한 적 (Self.SummonCount 조건용).</summary>
        public RuntimeEnemy SummonedBy { get; set; }

        public RuntimeEnemy(string unitId, string dataId, int maxHp, int attack, int defense, bool isBoss)
            : base(unitId, dataId, TeamType.Enemy, maxHp, attack, defense)
        {
            IsBoss = isBoss;
            if (isBoss) Tags.Add("Boss");
        }
    }

    /// <summary>아군 소환수. 소환한 카드 인스턴스를 기억해 사망 시 버린 더미로 보낸다 (`04` §4).</summary>
    public sealed class RuntimeMonster : RuntimeUnit
    {
        public RuntimeCard SourceCard { get; }
        public bool IsToken { get; }
        /// <summary>몬스터 레벨. 희생 Crystal의 기본값이다 (`13` §2.3). 해골 토큰은 Lv1 (`17` NEC_ACT_002).</summary>
        public int Level { get; set; } = 1;
        /// <summary>레벨과 다른 희생 Crystal (파편 +1, 수정 골렘 Lv2 +3).</summary>
        public int? SacrificeCrystalOverride { get; set; }
        /// <summary>희생이 예약되어 SacrificeAction을 기다리는 중. 같은 유닛을 두 번 희생하지 못하게 한다. 유닛은 그동안 필드에 있다.</summary>
        public bool SacrificeReserved { get; internal set; }
        public bool IsSoulbound => Tags.Contains("Soulbound");
        /// <summary>커맨드 (D-27). 턴 시작에 공격으로 돌아간다.</summary>
        public UnitCommand Command { get; set; } = UnitCommand.Attack;
        /// <summary>플레이어가 고른 공격 대상. 없거나 죽었으면 기본 대상 (Taunt 적 → 적 앞줄).</summary>
        public RuntimeEnemy AttackTarget { get; set; }
        /// <summary>이번 턴 추가 행동 횟수 (신속한 지휘). 턴 시작에 0.</summary>
        public int ExtraActionsThisTurn { get; set; }
        /// <summary>누적형 효과의 쌓인 횟수 (망령 전사 등, 상한 판정용). 전투 동안 유지.</summary>
        public int GrowthStacks { get; set; }

        public RuntimeMonster(string unitId, string dataId, int maxHp, int attack, int defense, RuntimeCard sourceCard, bool isToken = false)
            : base(unitId, dataId, TeamType.Player, maxHp, attack, defense)
        {
            SourceCard = sourceCard;
            IsToken = isToken;
        }
    }
}
