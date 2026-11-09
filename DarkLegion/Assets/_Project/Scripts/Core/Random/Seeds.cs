namespace DarkLegion.Core
{
    /// <summary>런 시드에서 노드 시드를 만든다 (`14` §3, `01` D-37).</summary>
    public static class Seeds
    {
        /// <summary>
        /// NodeSeed = unchecked((int)(RngSeed*31 + Floor)*31 + StageIndex).
        /// 저장하지 않고 매번 계산한다. 같은 노드를 다시 시작하면 같은 전투가 된다.
        /// </summary>
        public static int NodeSeed(long rngSeed, int floor, int stageIndex)
        {
            return unchecked((int)(rngSeed * 31 + floor) * 31 + stageIndex);
        }
    }
}
