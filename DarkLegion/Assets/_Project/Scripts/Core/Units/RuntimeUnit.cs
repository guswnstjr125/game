using System.Collections.Generic;

namespace DarkLegion.Core
{
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

        public RuntimeMonster(string unitId, string dataId, int maxHp, int attack, int defense, RuntimeCard sourceCard, bool isToken = false)
            : base(unitId, dataId, TeamType.Player, maxHp, attack, defense)
        {
            SourceCard = sourceCard;
            IsToken = isToken;
        }
    }
}
