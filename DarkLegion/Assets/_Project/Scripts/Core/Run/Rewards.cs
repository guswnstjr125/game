using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>`06` Reward_BattleTable 한 행: 전투 종류 × 층의 골드 범위와 희귀도 확률(합 1, D-35).</summary>
    public sealed class RewardRow
    {
        public string BattleType;
        public int Floor;
        public int GoldMin;
        public int GoldMax;
        /// <summary>희귀도 C/U/R/L → 확률.</summary>
        public IReadOnlyDictionary<string, double> Rarity;
    }

    /// <summary>보상 표 (`06` Reward_BattleTable·Reward_EliteRelic, `14` Boss_List).</summary>
    public sealed class RewardTables
    {
        private readonly Dictionary<(string, int), RewardRow> _rows = new Dictionary<(string, int), RewardRow>();
        private readonly Dictionary<int, IReadOnlyList<string>> _elite = new Dictionary<int, IReadOnlyList<string>>();
        private readonly Dictionary<int, string> _boss = new Dictionary<int, string>();

        public void AddRow(RewardRow row) => _rows[(row.BattleType, row.Floor)] = row;
        public void SetEliteRelicPool(int floor, IReadOnlyList<string> pool) => _elite[floor] = pool;
        public void SetBossRelic(int floor, string relicId) => _boss[floor] = relicId;

        /// <summary>노드 종류의 행. 이벤트 카드 보상은 Normal 행 (D-49).</summary>
        public RewardRow Row(NodeKind kind, int floor)
        {
            string type = kind == NodeKind.Elite ? "Elite" : kind == NodeKind.Boss ? "Boss" : "Normal";
            return _rows.TryGetValue((type, floor), out var r) ? r : throw new System.InvalidOperationException($"no Reward_BattleTable row {type}/{floor}");
        }

        /// <summary>그 층 엘리트 유물 풀 A → B → C (`06` Reward_EliteRelic).</summary>
        public IReadOnlyList<string> EliteRelicPool(int floor) => _elite.TryGetValue(floor, out var p) ? p : (IReadOnlyList<string>)new string[0];

        /// <summary>그 층 보스 유물 (D-16).</summary>
        public string BossRelic(int floor) => _boss.TryGetValue(floor, out var r) ? r : null;
    }

    /// <summary>카드 보상 후보 풀 (D-75): 해금층 ≤ 현재 층(시작 클래스면 IfStartClass) ∧ 그 전투 종류의 DropPool ∧ (1단계면 Stage1CardPool).</summary>
    public static class CardPool
    {
        public static List<CardDefinition> Candidates(CardCatalog cards, int floor, string startClass, NodeKind kind, bool stage1)
        {
            var stage1Set = new HashSet<string>(Stage1Scope.Cards);
            var list = new List<CardDefinition>();
            foreach (var id in cards.Ids)
            {
                var c = cards.Get(id);
                if (c.Type == CardType.Curse) continue;
                int? unlock = c.Class == startClass && c.UnlockFloorIfStartClass.HasValue ? c.UnlockFloorIfStartClass : c.UnlockFloor;
                if (!unlock.HasValue || unlock.Value > floor) continue;
                bool drop = kind == NodeKind.Elite ? c.DropElite : kind == NodeKind.Boss ? c.DropBoss : c.DropNormal;
                if (!drop) continue;
                if (stage1 && !stage1Set.Contains(id)) continue;
                list.Add(c);
            }
            return list;
        }
    }

    /// <summary>
    /// 카드 n장 고르기 (D-49·D-75): 희귀도 1회(빈 희귀도 제외 재정규화) → 그 희귀도에서 겹치지 않게 min(남은 칸, 후보 수)장
    /// → 칸이 남으면 희귀도를 다시 굴린다. 다시 굴린 칸끼리는 겹쳐도 된다. 전투 3택1과 이벤트 GrantCard가 같이 쓴다.
    /// </summary>
    public static class CardRewardRoller
    {
        private static readonly string[] Order = { "C", "U", "R", "L" };

        public static List<string> Roll(BattleRandom rng, IReadOnlyList<CardDefinition> candidates, RewardRow row, int count, List<string> rarityLog = null)
        {
            var byRarity = Order.ToDictionary(r => r, r => candidates.Where(c => c.Rarity == r).ToList());
            var weights = Order.Where(r => byRarity[r].Count > 0 && row.Rarity.TryGetValue(r, out var p) && p > 0).Select(r => (r, row.Rarity[r])).ToList();
            var result = new List<string>();
            if (weights.Count == 0) return result;
            double total = weights.Sum(w => w.Item2);
            while (result.Count < count)
            {
                double x = rng.NextFloat() * total;
                string rarity = weights[weights.Count - 1].r;
                foreach (var (r, p) in weights)
                {
                    if (x < p)
                    {
                        rarity = r;
                        break;
                    }
                    x -= p;
                }
                rarityLog?.Add(rarity);
                var pool = new List<CardDefinition>(byRarity[rarity]);
                int take = System.Math.Min(count - result.Count, pool.Count);
                for (int i = 0; i < take; i++)
                {
                    int j = rng.Next(i, pool.Count);
                    var t = pool[i];
                    pool[i] = pool[j];
                    pool[j] = t;
                    result.Add(pool[i].Id);
                }
            }
            return result;
        }
    }

    public enum RewardSource
    {
        Battle,
        Elite,
        Boss
    }

    /// <summary>
    /// 확정 전 보상 (`14` §4 보상 대기, D-67). 후보는 만든 순서 그대로 저장하고, 마지막 확인에서 한 번에 적용한다.
    /// </summary>
    public sealed class PendingReward
    {
        public string NodeId;
        public RewardSource Source;
        public int Gold;
        public List<string> CardChoices = new List<string>();
        /// <summary>엘리트: 미보유 유물 후보. 1개 이상이면 반드시 하나 고른다 (D-75).</summary>
        public List<string> RelicChoices = new List<string>();
        /// <summary>보스: 고르지 않고 받는 유물 (D-16).</summary>
        public string AutoRelic;
        public bool Confirmed;
        /// <summary>희귀도 굴림 기록 (검사·디버그용, 저장하지 않는다).</summary>
        public List<string> RarityRolls = new List<string>();
    }

    /// <summary>전투 보상 만들기·확정 (`03` §8, `37` §2.3, D-16·D-37·D-66·D-67·D-75).</summary>
    public static class RewardFlow
    {
        /// <summary>현재 노드의 전투 보상. 보상 난수 = BattleRandom(RewardSeed(NodeSeed)), 골드 → 카드 순 (D-75).</summary>
        public static PendingReward ForBattle(RunState run, NodeKind kind, GameDataLoadResult data)
        {
            if (kind != NodeKind.Battle && kind != NodeKind.Elite && kind != NodeKind.Boss) throw new System.ArgumentException($"{kind} has no battle reward", nameof(kind));
            var rng = new BattleRandom(Seeds.RewardSeed(run.NodeSeed));
            var row = data.Rewards.Row(kind, run.Floor);
            var reward = new PendingReward
            {
                NodeId = run.CurrentNodeId,
                Source = kind == NodeKind.Elite ? RewardSource.Elite : kind == NodeKind.Boss ? RewardSource.Boss : RewardSource.Battle,
                Gold = rng.Next(row.GoldMin, row.GoldMax + 1)
            };
            var pool = CardPool.Candidates(data.Cards, run.Floor, run.StartClass, kind, run.Stage1Build);
            reward.CardChoices = CardRewardRoller.Roll(rng, pool, row, 3, reward.RarityRolls);
            if (kind == NodeKind.Elite) reward.RelicChoices = data.Rewards.EliteRelicPool(run.Floor).Where(r => !run.Relics.Contains(r)).ToList();
            if (kind == NodeKind.Boss) reward.AutoRelic = data.Rewards.BossRelic(run.Floor);
            return reward;
        }

        /// <summary>
        /// 마지막 확인 (D-67). cardIndex null = 카드 건너뛰기(D-37). 유물 후보가 있으면 relicIndex가 필요하다.
        /// 이미 확정했거나 입력이 잘못되면 아무것도 바꾸지 않고 false. 노드 완료(RunFlow.CompleteNode)는 호출하는 쪽이 이어서 한다.
        /// </summary>
        public static bool Confirm(RunState run, PendingReward reward, int? cardIndex, int? relicIndex)
        {
            if (reward == null || reward.Confirmed) return false;
            if (cardIndex.HasValue && (cardIndex < 0 || cardIndex >= reward.CardChoices.Count)) return false;
            if (reward.RelicChoices.Count > 0)
            {
                if (!relicIndex.HasValue || relicIndex < 0 || relicIndex >= reward.RelicChoices.Count) return false;
            }
            else if (relicIndex.HasValue) return false;

            run.Gold += reward.Gold;
            if (relicIndex.HasValue) run.Relics.Add(reward.RelicChoices[relicIndex.Value]);
            if (!string.IsNullOrEmpty(reward.AutoRelic) && !run.Relics.Contains(reward.AutoRelic)) run.Relics.Add(reward.AutoRelic);
            if (cardIndex.HasValue) run.AddCard(reward.CardChoices[cardIndex.Value]);
            reward.Confirmed = true;
            return true;
        }
    }
}
