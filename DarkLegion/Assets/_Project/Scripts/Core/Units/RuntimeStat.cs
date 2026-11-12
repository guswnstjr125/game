namespace DarkLegion.Core
{
    /// <summary>
    /// 스탯 = (Base + Additive) × Multiplicative, 반올림 후 0 이상 (`06` §5).
    /// 반올림은 0.5 올림이다 (`16` §2와 같은 규칙. C# 기본 은행가 반올림을 쓰지 않는다).
    /// </summary>
    public sealed class RuntimeStat
    {
        public int BaseValue;
        public int AdditiveModifier;
        public float MultiplicativeModifier = 1f;

        public RuntimeStat(int baseValue)
        {
            BaseValue = baseValue;
        }

        public int FinalValue
        {
            get
            {
                int v = Rounding.HalfUp((BaseValue + AdditiveModifier) * (double)MultiplicativeModifier);
                return v < 0 ? 0 : v;
            }
        }
    }

    public static class Rounding
    {
        /// <summary>0.5 올림 반올림 (양수 기준, 음수는 0 쪽이 아닌 +무한대 쪽으로 0.5 올림).</summary>
        public static int HalfUp(double x)
        {
            return (int)System.Math.Floor(x + 0.5 + 1e-9);
        }
    }
}
