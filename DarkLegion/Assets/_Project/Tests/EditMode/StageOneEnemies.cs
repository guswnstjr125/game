using DarkLegion.Core;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 테스트용 적 행동표. 1~2층 일반·엘리트 행은 `10_적_바이블.xlsx` Enemy_Actions(D-61)에서 손으로 옮겼다.
    /// ENE_TEST_* 는 테스트 전용 (고정 행동 1개).
    /// </summary>
    public static class StageOneEnemies
    {
        public static EnemyKitCatalog Create()
        {
            var k = new EnemyKitCatalog();
            EnemyActionDef A(string id, string cond, double chance, string type, double value, string status, int dur, string target)
                => new EnemyActionDef { ActionId = id, Condition = cond, Chance = chance, ActionType = type, Value = value, Status = status, Duration = dur, Target = target };

            k.Add("ENE_TEST_IDLE", A("WAIT", "", 1, "GainShield", 0, "", 0, "Self"));
            k.Add("ENE_TEST_HIT8", A("HIT", "", 1, "Attack", 1, "", 0, "FrontLine"));
            k.Add("ENE_TEST_AOE4", A("AOE", "", 1, "AoE", 4, "", 0, "All"));

            k.Add("ENE_1F_GHO_001", A("HEAVY", "Self.HpPct>50", 0.3, "Attack", 1.5, "", 0, "FrontLine"), A("BASIC", "", 1, "Attack", 1, "", 0, "FrontLine"));
            k.Add("ENE_1F_GHO_002", A("VENOM_BITE", "AnyTarget.NoStatus(Poison)", 1, "Attack+Status", 0.5, "Poison", 2, "LowestHp"), A("BASIC", "", 1, "Attack", 1, "", 0, "LowestHp"));
            k.Add("ENE_1F_GHO_003", A("SELF_DESTRUCT", "Self.Hp<=3", 1, "AoE+SelfDeath", 3, "", 0, "All"), A("WEAKEN", "AnyTarget.NoStatus(Weak)", 1, "ApplyStatus", 0, "Weak", 2, "Back"), A("BASIC", "", 1, "Attack", 1, "", 0, "Back"));
            k.Add("ENE_1F_ELT_001", A("SUMMON", "Self.SummonCount<2", 1, "Summon", 0, "ENE_1F_GHO_001", 0, "EnemySlot"), A("DRAIN", "Self.HpPct<60", 1, "LifeSteal", 1, "", 0, "Back"), A("HEAVY", "", 1, "Attack", 1.5, "", 0, "Back"));
            k.Add("ENE_1F_ELT_002", A("POISON", "", 1, "ApplyStatus", 0, "Poison", 2, "FrontLine"), A("WEAKEN", "", 1, "ApplyStatus", 0, "Weak", 2, "FrontLine"), A("BASIC", "", 1, "Attack", 1, "", 0, "FrontLine"));
            k.Add("ENE_2F_FIR_003", A("GUARD", "Self.NoShield", 1, "GainShield", 4, "", 0, "Self"), A("TAUNT", "Self.NoStatus(Taunt)", 1, "ApplyStatus", 0, "Taunt", 0, "Self"), A("BASIC", "", 1, "Attack", 1, "", 0, "FrontLine"));
            // 소환되는 굶주린 구울 기본 스탯 (`10` Enemies_Master)
            k.SetSpawnStats("ENE_1F_GHO_001", 14, 5, 1);
            return k;
        }
    }
}
