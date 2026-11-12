namespace DarkLegion.Core
{
    /// <summary>
    /// 적 스탯 스케일 (`16` §2, D-09, `03` §1 "개별 적은 Enemies_Master + 노드 진행 시 스케일 추가").
    /// Enemies_Master 값은 그 층 스테이지인덱스 0 기준이라 층 계수는 이미 들어 있다. 여기서는 노드 계수만 곱한다.
    /// 보스는 Boss_List 값을 그대로 쓴다 (노드 9 고정).
    /// </summary>
    public static class EnemyScaler
    {
        public const double HpPerIndex = 0.023;
        public const double AtkPerIndex = 0.028;
        public const double DefPerIndex = 0.027;

        public static (int hp, int atk, int def) Scale(int baseHp, int baseAtk, int baseDef, int stageIndex, bool isBoss)
        {
            if (stageIndex < 0 || stageIndex > 8)
            {
                throw new System.ArgumentOutOfRangeException(nameof(stageIndex), "stage index is 0~8 (node 1 = 0)");
            }
            if (isBoss) return (baseHp, baseAtk, baseDef);
            return (
                Rounding.HalfUp(baseHp * (1 + HpPerIndex * stageIndex)),
                Rounding.HalfUp(baseAtk * (1 + AtkPerIndex * stageIndex)),
                Rounding.HalfUp(baseDef * (1 + DefPerIndex * stageIndex)));
        }

        /// <summary>`16` §2 기준 적(HP14·ATK5·DEF1) 공식. Floor_Scale 표 검증용.</summary>
        public static (int hp, int atk, int def) Reference(int floor, int stageIndex)
        {
            return (
                Rounding.HalfUp(14 * (1 + 0.29 * (floor - 1)) * (1 + HpPerIndex * stageIndex)),
                Rounding.HalfUp(5 * (1 + 0.18 * (floor - 1)) * (1 + AtkPerIndex * stageIndex)),
                Rounding.HalfUp(1 * (1 + 0.22 * (floor - 1)) * (1 + DefPerIndex * stageIndex)));
        }
    }
}
