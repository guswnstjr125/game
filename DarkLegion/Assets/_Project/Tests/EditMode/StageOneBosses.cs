using DarkLegion.Core;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 테스트용 보스 패턴. `14_보스_패턴.xlsx` Boss_TurnCycle 1~2층 행을 손으로 옮겼다 (임포터 #35가 생기면 JSON에서 만든다).
    /// </summary>
    public static class StageOneBosses
    {
        public static BossPatternCatalog Create()
        {
            var c = new BossPatternCatalog();
            c.Add("ENE_1F_BOS_001",
                new BossStep(1, 0, "SingleAttack", 8, null, "Front", false),
                new BossStep(1, 1, "ApplyWeak", 0, "Weak 2턴", "Back", false),
                new BossStep(1, 2, "SingleAttack", 8, null, "Front", false),
                new BossStep(2, 0, "Summon", 0, null, null, false, "GHO_001 ×1"),
                new BossStep(2, 1, "LifeSteal", 8, "HP 회복 50%", "LowestHp", false),
                new BossStep(2, 2, "AoE", 6, null, "All", false),
                new BossStep(3, 0, "SingleAttack", 8, null, "Front", true));
            c.Add("ENE_2F_BOS_001",
                new BossStep(1, 0, "ApplyBurn", 0, "Burn+2", "All", false),
                new BossStep(1, 1, "SingleAttack", 9, null, "Front", false),
                new BossStep(1, 2, "AoE", 4, null, "All", false),
                new BossStep(2, 0, "ApplyBurn", 0, "Burn+3", "All", false),
                new BossStep(2, 1, "SingleAttack", 9, null, "Front", true),
                new BossStep(2, 2, "AoE", 8, null, "All", false),
                new BossStep(3, 0, "ApplyBurn", 0, "Burn+1", "All", false),
                new BossStep(3, 0, "SingleAttack", 9, null, "Front", false));
            return c;
        }
    }
}
