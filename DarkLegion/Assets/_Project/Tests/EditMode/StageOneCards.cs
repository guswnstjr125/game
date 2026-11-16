using System.Collections.Generic;
using DarkLegion.Core;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 테스트용 카드 정의. 값은 `08` Cards_Master·Cards_Effect·Cards_Upgrade(Up_Params)에서 손으로 옮겼다.
    /// Params·UpParams의 키 이름은 Cards_Upgrade Up_Params의 키와 같아야 한다 (`38` §4).
    /// 실제 게임은 임포터(#35)가 Excel JSON에서 만든다. 여기 값이 Excel과 다르면 Excel이 맞다.
    /// </summary>
    public static class StageOneCards
    {
        public static CardCatalog Create()
        {
            var c = new CardCatalog();
            void Mon(string id, int cost, int lv, int hp, int atk, int def, int upHp, int upAtk, int upDef, bool soulbound = false, int? sac = null)
                => c.Add(new CardDefinition { Id = id, Type = CardType.Monster, Cost = cost, Level = lv, Hp = hp, Atk = atk, Def = def, UpHp = upHp, UpAtk = upAtk, UpDef = upDef, Soulbound = soulbound, SacrificeCrystal = sac });
            void Card(string id, CardType type, int cost, bool exhaust, Dictionary<string, double> p, Dictionary<string, double> up = null, int? upCost = null, bool ethereal = false)
                => c.Add(new CardDefinition { Id = id, Type = type, Cost = cost, Exhaust = exhaust, Ethereal = ethereal, Params = p, UpParams = up ?? new Dictionary<string, double>(), UpCost = upCost });

            // 스탯: Cards_Master, 강화 스탯: Cards_Upgrade Up_Params (HP=..;ATK=..;DEF=..)
            Mon("NEC_MON_001", 1, 1, 10, 3, 1, 12, 3, 1);
            Mon("NEC_MON_002", 1, 1, 14, 2, 0, 16, 2, 0);
            Mon("NEC_MON_003", 1, 1, 6, 5, 0, 6, 6, 0);
            Mon("NEC_MON_005", 1, 1, 5, 2, 4, 7, 2, 4, soulbound: true);
            Mon("NEC_MON_007", 2, 2, 15, 4, 2, 17, 4, 2);
            Mon("NEU_MON_001", 0, 1, 5, 0, 0, 7, 0, 0, sac: 1);
            Mon("NEU_MON_004", 1, 1, 15, 0, 2, 17, 0, 2, soulbound: true);
            Mon("NEU_MON_008", 2, 2, 18, 6, 2, 20, 6, 2);

            Card("NEC_SKL_001", CardType.Skill, 0, false, new Dictionary<string, double> { ["DeathThisTurnDraw"] = 2 }, new Dictionary<string, double> { ["DeathThisTurnDraw"] = 3 });
            Card("NEC_SKL_002", CardType.Skill, 1, false, new Dictionary<string, double> { ["Shield"] = 3, ["FrontBonus"] = 2 }, upCost: 0);
            Card("NEC_SKL_003", CardType.Skill, 1, false, new Dictionary<string, double> { ["Poison"] = 3, ["Weak"] = 2 }, upCost: 0);
            Card("NEC_SKL_004", CardType.Skill, 1, false, new Dictionary<string, double>(), upCost: 0);
            Card("NEU_SKL_001", CardType.Skill, 1, false, new Dictionary<string, double> { ["ExtraAction"] = 1 }, upCost: 0);
            Card("NEU_SKL_002", CardType.Skill, 1, false, new Dictionary<string, double>(), upCost: 0);
            Card("NEC_ACT_001", CardType.Action, 1, true, new Dictionary<string, double> { ["CrystalBonus"] = 1 }, new Dictionary<string, double> { ["CrystalBonus"] = 2 });
            Card("NEC_ACT_002", CardType.Action, 2, true, new Dictionary<string, double> { ["Tokens"] = 3, ["TokenHp"] = 4, ["TokenAtk"] = 2 }, new Dictionary<string, double> { ["TokenHp"] = 6 });
            Card("NEC_ACT_003", CardType.Action, 2, true, new Dictionary<string, double> { ["DamageMult"] = 1.0 }, new Dictionary<string, double> { ["DamageMult"] = 1.2 });
            Card("NEC_ACT_004", CardType.Action, 3, true, new Dictionary<string, double> { ["Damage"] = 15, ["HealRatio"] = 0.5 }, new Dictionary<string, double> { ["Damage"] = 19 });
            Card("NEU_ACT_001", CardType.Action, 0, false, new Dictionary<string, double> { ["Draw"] = 2 }, new Dictionary<string, double> { ["Draw"] = 3 }, ethereal: true);
            Card("NEU_ACT_002", CardType.Action, 2, true, new Dictionary<string, double> { ["Multiplier"] = 2 }, upCost: 1);
            return c;
        }
    }
}
