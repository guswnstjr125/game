namespace DarkLegion.Core
{
    /// <summary>무한 루프 방지 한도 (`06` §9.5, `05` §4).</summary>
    public static class BattleLimits
    {
        public const int MaxQueueCount = 500;
        public const int MaxTriggerDepth = 32;
        public const int MaxTriggerIterationsPerFlush = 20;
        /// <summary>노드당 30턴 초과 시 패배 (`03` §2).</summary>
        public const int MaxTurnCount = 30;
    }
}
