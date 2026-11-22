using System.Collections.Generic;
using System.Linq;
using System.Text;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// #76 1-3: 시작 덱으로 1~2층 전투를 돌려 전투당 플레이어 HP 손실을 D-14 목표(일반 2~4 / 엘리트 6~10 / 보스 10~15)와 비교한다.
    /// 테스트가 확인하는 것은 "모든 전투가 끝난다·같은 시드는 같은 결과"뿐이다. 손실 수치는 측정값이라 통과 조건이 아니다 (BAL-04는 수동 10런).
    /// </summary>
    [Category("Stage1")]
    public class StageOneBattleSimTests
    {
        public const int Seeds = 40;

        private static IEnumerable<(Encounter enc, bool wave2)> Cases()
        {
            foreach (int floor in new[] { 1, 2 })
            {
                foreach (var enc in StageOneBattleSim.Encounters(floor))
                {
                    yield return (enc, false);
                    if (enc.Waves.Count > 1) yield return (enc, true);
                }
            }
        }

        [Test]
        public void Sim_EncountersExistForFloors1And2()
        {
            // `03` 노드 1·2·4·5(A·B)·7·9 → 1층 7편성, 2층은 엘리트 A만 → 6편성
            Assert.AreEqual(7, StageOneBattleSim.Encounters(1).Count);
            Assert.AreEqual(6, StageOneBattleSim.Encounters(2).Count);
            Assert.AreEqual(2, StageOneBattleSim.Encounters(1).Single(e => e.Node == 7).Waves[2].Single().count, "1층 노드 7 Wave 2 = 구울 ×2");
        }

        [Test]
        public void Sim_SameSeedSameResult()
        {
            var enc = StageOneBattleSim.Encounters(1).Single(e => e.Node == 9);
            var a = StageOneBattleSim.Run(enc, false, 7);
            var b = StageOneBattleSim.Run(enc, false, 7);
            Assert.AreEqual((a.Outcome, a.Turns, a.HpLoss), (b.Outcome, b.Turns, b.HpLoss));
        }

        [Test]
        public void Sim_AllBattlesEnd_ReportHpLossAgainstD14()
        {
            var results = new List<StageOneBattleSim.Result>();
            foreach (var (enc, wave2) in Cases())
            {
                for (int seed = 1; seed <= Seeds; seed++) results.Add(StageOneBattleSim.Run(enc, wave2, seed));
            }
            Assert.IsTrue(results.All(r => r.Outcome != BattleOutcome.None), "모든 전투가 승패로 끝난다");

            var sb = new StringBuilder();
            sb.AppendLine($"[SIM] NEC 시작 덱 10장 + 피의 잔, 전투마다 HP {StageOneBattleSim.StartHp}에서 시작, 시드 {Seeds}개");
            sb.AppendLine("편성 | Wave2 | 승률 | 평균 턴 | 평균 손실 | 최대 손실");
            foreach (var g in results.GroupBy(r => (r.Encounter.Label, r.Wave2)))
            {
                var l = g.ToList();
                sb.AppendLine($"{g.Key.Label} | {(g.Key.Wave2 ? "O" : "-")} | {100.0 * l.Count(r => r.Outcome == BattleOutcome.Victory) / l.Count:0}% | {l.Average(r => r.Turns):0.0} | {l.Average(r => r.HpLoss):0.0} | {l.Max(r => r.HpLoss)}");
            }

            // 노드 기대값: Wave 2는 50%, 엘리트 A·B는 50% (D-61). 층마다 노드 종류별 평균을 D-14와 비교
            sb.AppendLine("층 | 종류 | 기대 손실 | D-14 목표");
            foreach (int floor in new[] { 1, 2 })
            {
                foreach (var (type, target) in new[] { ("Battle", "2~4"), ("Elite", "6~10"), ("Boss", "10~15") })
                {
                    var nodes = results.Where(r => r.Encounter.Floor == floor && r.Encounter.NodeType == type).GroupBy(r => r.Encounter.Node);
                    double expected = nodes.Average(node =>
                        node.GroupBy(r => r.Encounter.Variant).Average(v =>
                        {
                            var withW2 = v.Where(r => r.Wave2).ToList();
                            double noW2 = v.Where(r => !r.Wave2).Average(r => r.HpLoss);
                            return withW2.Count == 0 ? noW2 : 0.5 * noW2 + 0.5 * withW2.Average(r => r.HpLoss);
                        }));
                    sb.AppendLine($"{floor}F | {type} | {expected:0.0} | {target}");
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
