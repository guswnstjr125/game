using System.Collections.Generic;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    [Category("Stage1")]
    public class RandomAndSeedTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new BattleRandom(1234);
            var b = new BattleRandom(1234);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), "seed 1234, index " + i);
            }
        }

        [Test]
        public void DifferentSeedsDiffer()
        {
            var a = new BattleRandom(1);
            var b = new BattleRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt()) same++;
            }
            Assert.Less(same, 3);
        }

        [Test]
        public void KnownSequenceIsStable()
        {
            // 알고리즘이 바뀌면 저장된 런의 재현이 깨진다. 값이 바뀌면 이 테스트가 알린다.
            var r = new BattleRandom(42);
            var got = new List<uint> { r.NextUInt(), r.NextUInt(), r.NextUInt() };
            CollectionAssert.AreEqual(Expected42, got);
        }

        // 기대값은 C#과 별개로 Python 참조 구현(SplitMix64 + xoshiro128**)으로 계산했다.
        private static readonly uint[] Expected42 = { 1776835114u, 4165204688u, 17111135u };

        [Test]
        public void NextIsInRangeAndRoughlyUniform()
        {
            var r = new BattleRandom(7);
            var counts = new int[4];
            for (int i = 0; i < 40000; i++)
            {
                int v = r.Next(0, 4);
                Assert.That(v, Is.InRange(0, 3));
                counts[v]++;
            }
            foreach (int c in counts)
            {
                Assert.That(c / 40000.0, Is.EqualTo(0.25).Within(0.015), "seed 7");
            }
        }

        [Test]
        public void ChanceMatchesProbability()
        {
            var r = new BattleRandom(99);
            int hits = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (r.Chance(0.4)) hits++;
            }
            // 40% ± 1.5%p (`32` §11.3)
            Assert.That(hits / 10000.0, Is.EqualTo(0.4).Within(0.015), "seed 99");
        }

        [Test]
        public void NodeSeedFormula()
        {
            // (1000 × 31 + 2) × 31 + 5 = 961,067
            Assert.AreEqual(961067, Seeds.NodeSeed(1000, 2, 5));
            // 같은 위치 = 같은 시드, 다른 노드 = 다른 시드
            Assert.AreEqual(Seeds.NodeSeed(555, 1, 3), Seeds.NodeSeed(555, 1, 3));
            Assert.AreNotEqual(Seeds.NodeSeed(555, 1, 3), Seeds.NodeSeed(555, 1, 4));
            // 오버플로는 unchecked로 감싼다 (예외 없음)
            Assert.DoesNotThrow(() => Seeds.NodeSeed(long.MaxValue, 5, 9));
        }
    }
}
