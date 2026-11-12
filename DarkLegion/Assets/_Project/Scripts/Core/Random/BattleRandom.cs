namespace DarkLegion.Core
{
    /// <summary>
    /// 재현 가능한 난수 (`06` §15). 전투·보상의 모든 난수는 이것으로만 뽑는다.
    /// 알고리즘: SplitMix64로 시드를 펼친 xoshiro128**. System.Random·UnityEngine.Random은
    /// 런타임마다 구현이 다를 수 있어 쓰지 않는다 (같은 시드 = 어디서나 같은 수열).
    /// </summary>
    public sealed class BattleRandom
    {
        private uint _s0, _s1, _s2, _s3;

        public int Seed { get; }

        public BattleRandom(int seed)
        {
            Seed = seed;
            ulong x = unchecked((ulong)(long)seed);
            ulong a = SplitMix64(ref x);
            ulong b = SplitMix64(ref x);
            _s0 = (uint)a;
            _s1 = (uint)(a >> 32);
            _s2 = (uint)b;
            _s3 = (uint)(b >> 32);
            if ((_s0 | _s1 | _s2 | _s3) == 0)
            {
                _s0 = 1; // xoshiro 상태는 전부 0이면 안 된다
            }
        }

        /// <summary>32비트 부호 없는 난수.</summary>
        public uint NextUInt()
        {
            uint result = RotL(_s1 * 5, 7) * 9;
            uint t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotL(_s3, 11);
            return result;
        }

        /// <summary>[minInclusive, maxExclusive) 정수. 편향 없이 거부 표본으로 뽑는다.</summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maxExclusive), "max must be greater than min");
            }
            uint range = (uint)(maxExclusive - minInclusive);
            uint limit = uint.MaxValue - (uint.MaxValue % range);
            uint r;
            do
            {
                r = NextUInt();
            } while (r >= limit);
            return (int)(minInclusive + (r % range));
        }

        /// <summary>[0, 1) 실수 (24비트 정밀도).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1.0f / 16777216f);
        }

        /// <summary>확률 p(0~1)로 true. 이벤트 Chance(50%) 같은 판정에 쓴다.</summary>
        public bool Chance(double p)
        {
            if (p <= 0) return false;
            if (p >= 1) return true;
            return (NextUInt() >> 8) < p * 16777216.0;
        }

        private static uint RotL(uint x, int k)
        {
            return (x << k) | (x >> (32 - k));
        }

        private static ulong SplitMix64(ref ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                ulong z = x;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
