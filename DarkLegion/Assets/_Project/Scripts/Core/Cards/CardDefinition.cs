using System.Collections.Generic;

namespace DarkLegion.Core
{
    public enum CardType
    {
        Monster,
        Skill,
        Action,
        Curse
    }

    /// <summary>
    /// 카드 한 종류의 정의 (`38` §3 CardData를 Core가 쓰는 형태). 임포터(#35)가 Excel JSON에서 만든다.
    /// 효과 수치는 Params, 강화 후 값은 UpParams (`08` Cards_Upgrade Up_Params, `38` §4).
    /// </summary>
    public sealed class CardDefinition
    {
        public string Id;
        public CardType Type;
        public int Cost;
        /// <summary>강화하면 바뀌는 비용 (Cost- 강화). 없으면 Cost 그대로.</summary>
        public int? UpCost;
        public bool Exhaust;
        public bool Ethereal;
        // 몬스터 카드
        public int Level = 1;
        public int Hp;
        public int Atk;
        public int Def;
        /// <summary>강화 스탯 (`08` Cards_Upgrade Up_Params HP·ATK·DEF). 없으면 기본 스탯.</summary>
        public int? UpHp;
        public int? UpAtk;
        public int? UpDef;
        public bool Soulbound;
        public int? SacrificeCrystal;
        /// <summary>희귀도 C/U/R/L (`08` Cards_Master). 보상 희귀도 굴림에 쓴다 (D-49·D-75).</summary>
        public string Rarity = "";
        /// <summary>클래스 NEC/ELE/BEA/HOL/NEU (`08` Cards_Master).</summary>
        public string Class = "";
        /// <summary>해금층 (`08` Cards_FloorUnlock). null이면 드롭 풀에 없다(융합 전용 등).</summary>
        public int? UnlockFloor;
        /// <summary>시작 클래스가 이 카드의 클래스일 때 해금층 (D-07·D-17).</summary>
        public int? UnlockFloorIfStartClass;
        public bool DropNormal;
        public bool DropElite;
        public bool DropBoss;
        public Dictionary<string, double> Params = new Dictionary<string, double>();
        public Dictionary<string, double> UpParams = new Dictionary<string, double>();

        public int CostFor(bool upgraded) => upgraded && UpCost.HasValue ? UpCost.Value : Cost;

        /// <summary>
        /// 이 정의로 카드 인스턴스를 만든다. 비용·Exhaust·Ethereal은 정의(데이터)를 따른다 (D-24).
        /// 덱을 만드는 쪽(런 시작·보상·임포터)은 이 함수만 쓴다.
        /// </summary>
        public RuntimeCard CreateCard(int instanceId, bool upgraded)
        {
            return new RuntimeCard(instanceId, Id, CostFor(upgraded), Ethereal, Exhaust) { Upgraded = upgraded };
        }

        /// <summary>강화 여부에 맞는 수치. 강화 값이 없으면 기본 값, 기본 값도 없으면 fallback.</summary>
        public double Param(string key, bool upgraded, double fallback = 0)
        {
            if (upgraded && UpParams.TryGetValue(key, out var up)) return up;
            return Params.TryGetValue(key, out var v) ? v : fallback;
        }
    }

    /// <summary>카드 정의 모음. 없는 CardId는 null (CARD-02: 크래시 없이 안전 실패).</summary>
    public sealed class CardCatalog
    {
        private readonly Dictionary<string, CardDefinition> _defs = new Dictionary<string, CardDefinition>();

        public void Add(CardDefinition def) => _defs[def.Id] = def;

        /// <summary>등록된 카드 ID (등록 순서).</summary>
        public IEnumerable<string> Ids => _defs.Keys;

        public CardDefinition Get(string id) => id != null && _defs.TryGetValue(id, out var d) ? d : null;
    }
}
