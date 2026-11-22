using DarkLegion.Core;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// 테스트용 융합 데이터. `09_몬스터_바이블.xlsx` Fusion_Recipes·Fusion_RandomPool의 1단계 행(D-21·D-22·D-26)을
    /// Monsters_Master Card_ID로 바꿔 손으로 옮겼다. 가중치 0 행(MVP 제외)은 넣지 않는다.
    /// </summary>
    public static class StageOneFusion
    {
        public static FusionCatalog Create()
        {
            var f = new FusionCatalog();
            f.AddFixed("FUS_001", "NEC_MON_002", "NEC_MON_002", "NEC_MON_013");   // 구울 + 구울 → 식인 구울
            f.AddFixed("FUS_014", "NEC_MON_001", "NEC_MON_006", "NEC_MON_014");   // 뼈 병사 + 뼈 창병 → 저주 기사
            // POOL_NEC_L1: 클래스 80 / NEU 20
            f.AddPool("NEC", 1, "NEC_MON_013", 40);
            f.AddPool("NEC", 1, "NEC_MON_014", 40);
            f.AddPool("NEC", 1, "NEU_MON_007", 15);
            f.AddPool("NEC", 1, "NEU_MON_008", 5);
            // POOL_NEU_X_NEC: 50 / 50
            f.AddPool("NEC+NEU", 1, "NEC_MON_007", 50);
            f.AddPool("NEC+NEU", 1, "NEU_MON_007", 50);
            // POOL_NEU_L1
            f.AddPool("NEU", 1, "NEU_MON_007", 50);
            f.AddPool("NEU", 1, "NEU_MON_008", 50);
            // `09` Monster_Effects NEU_MON_002 Passive FusionRule: 상대 재료의 클래스로 취급 (D-70)
            f.AddClassMimic("NEU_MON_002");
            return f;
        }
    }
}
