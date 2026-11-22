using System.Collections.Generic;
using System.Linq;

namespace DarkLegion.Core
{
    /// <summary>런 덱 규칙: 강화 가능, 맵 희생 후보 (D-68·D-75).</summary>
    public static class RunDeckRules
    {
        /// <summary>아직 강화하지 않았고 Cards_Upgrade 행이 있는 카드. 저주는 강화할 수 없다 (D-75).</summary>
        public static bool IsUpgradable(RunCard card, CardCatalog cards)
        {
            var def = cards.Get(card.CardId);
            return !card.Upgraded && def != null && def.Type != CardType.Curse && def.HasUpgrade;
        }

        /// <summary>
        /// 맵 희생으로 뺄 수 있는 몬스터 개체 (D-68): Soulbound가 아니고, 뺀 뒤 몬스터 카드가 3장 이상 남고 강화할 카드가 남는다.
        /// </summary>
        public static List<RunCard> MapSacrificeCandidates(RunState run, CardCatalog cards)
        {
            var monsters = run.Deck.Where(c => cards.Get(c.CardId)?.Type == CardType.Monster).ToList();
            if (monsters.Count - 1 < 3) return new List<RunCard>();
            return monsters.Where(m => !cards.Get(m.CardId).Soulbound
                && run.Deck.Any(o => o != m && IsUpgradable(o, cards))).ToList();
        }
    }

    public sealed class RestOptions
    {
        public bool CanHeal;
        public int HealAmount;
        public bool CanUpgrade;
        /// <summary>둘 다 안 되면 [계속]만 보이고 누르면 노드를 완료한다 (D-69).</summary>
        public bool ContinueOnly => !CanHeal && !CanUpgrade;
    }

    /// <summary>휴식 노드 (`03` §2, RestNode_Rules, D-69). 노드 완료(RunFlow.CompleteNode)는 호출하는 쪽.</summary>
    public static class RestFlow
    {
        public static RestOptions Options(RunState run, GameDataLoadResult data) => new RestOptions
        {
            CanHeal = run.CurrentHp < run.MaxHp,
            HealAmount = (int)System.Math.Floor(run.MaxHp * data.RestHealPercent + 1e-9),
            CanUpgrade = run.Deck.Any(c => RunDeckRules.IsUpgradable(c, data.Cards))
        };

        public static bool Heal(RunState run, GameDataLoadResult data)
        {
            var o = Options(run, data);
            if (!o.CanHeal) return false;
            run.CurrentHp = System.Math.Min(run.MaxHp, run.CurrentHp + o.HealAmount);
            return true;
        }

        public static bool Upgrade(RunState run, GameDataLoadResult data, int instanceId)
        {
            var card = run.Deck.FirstOrDefault(c => c.InstanceId == instanceId);
            if (card == null || !RunDeckRules.IsUpgradable(card, data.Cards)) return false;
            card.Upgraded = true;
            return true;
        }
    }

    /// <summary>층별 이벤트 가중치 (`06` Event_FloorPool, 가중치 0은 없음).</summary>
    public sealed class EventPool
    {
        private readonly Dictionary<int, List<(string id, double weight)>> _floors = new Dictionary<int, List<(string, double)>>();

        public void Add(int floor, string eventId, double weight)
        {
            if (!_floors.TryGetValue(floor, out var l)) _floors[floor] = l = new List<(string, double)>();
            l.Add((eventId, weight));
        }

        public IReadOnlyList<(string id, double weight)> Floor(int floor) => _floors.TryGetValue(floor, out var l) ? l : (IReadOnlyList<(string, double)>)new List<(string, double)>();
    }

    /// <summary>이벤트 선택 결과 (보상 대기, D-67). 마지막 확인에서 비용·획득을 한 번에 적용한다.</summary>
    public sealed class EventOutcome
    {
        public string NodeId;
        public string EventId;
        public int Option;
        /// <summary>양수 = 회복(최대 HP까지), 음수 = HP 손실(0 이하면 패배, D-51).</summary>
        public int HpDelta;
        public int MaxHpDelta;
        public int GoldDelta;
        public List<string> CursesToAdd = new List<string>();
        public List<string> CardChoices = new List<string>();
        /// <summary>true = 3택1(건너뛰기 가능), false = 카드가 있으면 그대로 받음 (D-75).</summary>
        public bool CardChooseOne;
        /// <summary>악마 계약: 미보유 유물 최대 3개 중 반드시 1개.</summary>
        public List<string> RelicChoices = new List<string>();
        /// <summary>저주 상자: 무작위로 정해진 유물 (D-68).</summary>
        public string AutoRelic;
        public bool NeedsUpgradeChoice;
        public bool NeedsSacrificeChoice;
        /// <summary>피의 제단 옵션 2: 희생 뒤 남은 강화 가능 카드 중 무작위 1장 (확인 때 이벤트 난수로).</summary>
        public bool RandomUpgradeAfterSacrifice;
        /// <summary>영혼 감옥: 확인 뒤 RunBattle.StartForced로 전투, 승리 시 일반 전투 보상 1회 (D-68).</summary>
        public bool ForcedBattle;
        public bool Confirmed;
    }

    /// <summary>이벤트 확인 때 플레이어가 고른 것.</summary>
    public sealed class EventChoice
    {
        public int? CardIndex;
        public int? RelicIndex;
        public int? UpgradeInstanceId;
        public int? SacrificeInstanceId;
    }

    /// <summary>
    /// 이벤트 노드 (`24` §2, D-45·D-51·D-68·D-69·D-75·D-77). 1단계 7종.
    /// 난수: 노드 시드 난수의 첫 값 = 이벤트 결정, 선택지 안의 추첨은 그다음 값부터 (D-69). 선택지마다 순서는 D-77.
    /// </summary>
    public static class EventFlow
    {
        public static readonly IReadOnlyList<string> Implemented = new[]
        {
            "EVT_DEVIL_PACT", "EVT_BLOOD_ALTAR", "EVT_CURSED_CHEST", "EVT_SOUL_PRISON", "EVT_WANDER_MERCHANT", "EVT_FORGOTTEN_SHRINE", "EVT_DARK_WAGER"
        };

        /// <summary>
        /// 이벤트 노드에 들어간다 (D-69): 저장된 이벤트가 있으면 그것, 없으면 그 층 풀에서 이번 런에 나온 이벤트를 빼고 재정규화해
        /// 노드 시드 첫 값으로 뽑아 NodeEventIds에 넣는다. 후보가 없으면 제외 없이 전체에서.
        /// </summary>
        public static string Enter(RunState run, GameDataLoadResult data)
        {
            if (run.NodeEventIds.TryGetValue(run.CurrentNodeId, out var saved)) return saved;
            var cands = Weighted(run, data);
            if (cands.Count == 0) throw new System.InvalidOperationException($"no events for floor {run.Floor}");
            double x = new BattleRandom(run.NodeSeed).NextFloat() * cands.Sum(e => e.weight);
            string pick = cands[cands.Count - 1].id;
            foreach (var (id, w) in cands)
            {
                if (x < w)
                {
                    pick = id;
                    break;
                }
                x -= w;
            }
            run.NodeEventIds[run.CurrentNodeId] = pick;
            return pick;
        }

        /// <summary>
        /// 이 노드에서 나올 수 있는 이벤트와 확률 (D-69): 그 층 풀에서 이번 런에 나온 이벤트(현재 노드 제외)를 빼고 재정규화. 후보가 없으면 풀 전체.
        /// 예: 1층 노드 3이 악마 계약이면 노드 6은 14/86 × 3, 22/86 × 2 (EVT-03).
        /// </summary>
        public static List<(string id, double probability)> Candidates(RunState run, GameDataLoadResult data)
        {
            var cands = Weighted(run, data);
            double total = cands.Sum(e => e.weight);
            return cands.Select(e => (e.id, e.weight / total)).ToList();
        }

        private static List<(string id, double weight)> Weighted(RunState run, GameDataLoadResult data)
        {
            var pool = data.Events.Floor(run.Floor);
            var seen = new HashSet<string>(run.NodeEventIds.Where(kv => kv.Key != run.CurrentNodeId).Select(kv => kv.Value));
            var cands = pool.Where(e => !seen.Contains(e.id)).ToList();
            return cands.Count > 0 ? cands : pool.ToList();
        }

        /// <summary>이벤트 안 추첨용 난수: 노드 시드 난수에서 첫 값(이벤트 결정)을 버린 것 (D-69).</summary>
        private static BattleRandom InnerRandom(RunState run)
        {
            var rng = new BattleRandom(run.NodeSeed);
            rng.NextFloat();
            return rng;
        }

        private static List<string> UnownedEliteRelics(RunState run, GameDataLoadResult data) =>
            data.Rewards.EliteRelicPool(run.Floor).Where(r => !run.Relics.Contains(r)).Distinct().Take(3).ToList();

        private static List<string> NormalCards(RunState run, GameDataLoadResult data, BattleRandom rng, int count)
        {
            var pool = CardPool.Candidates(data.Cards, run.Floor, run.StartClass, NodeKind.Battle, run.Stage1Build);
            return CardRewardRoller.Roll(rng, pool, data.Rewards.Row(NodeKind.Battle, run.Floor), count);
        }

        /// <summary>선택지 조건 (`24` §2, D-51·D-68·D-75). 옵션 3은 항상.</summary>
        public static bool IsEnabled(RunState run, GameDataLoadResult data, string eventId, int option)
        {
            if (option == 3) return true;
            switch (eventId)
            {
                case "EVT_DEVIL_PACT": return option == 1 ? run.MaxHp > 20 : UnownedEliteRelics(run, data).Count > 0;
                case "EVT_BLOOD_ALTAR":
                    return option == 1
                        ? run.CurrentHp > 20 && run.Deck.Any(c => RunDeckRules.IsUpgradable(c, data.Cards))
                        : RunDeckRules.MapSacrificeCandidates(run, data.Cards).Count > 0;
                case "EVT_CURSED_CHEST": return option == 1 ? UnownedEliteRelics(run, data).Count > 0 : true;
                case "EVT_SOUL_PRISON": return true;
                case "EVT_WANDER_MERCHANT": return option == 1 ? run.Gold >= 8 : run.CurrentHp > 4;
                case "EVT_FORGOTTEN_SHRINE": return option == 1 || RunDeckRules.MapSacrificeCandidates(run, data.Cards).Count > 0;
                case "EVT_DARK_WAGER": return true;
                default: return false;
            }
        }

        /// <summary>선택지를 고르면 추첨까지 해서 결과를 만든다. 런은 바꾸지 않는다 (확인은 <see cref="Confirm"/>).</summary>
        public static EventOutcome Choose(RunState run, GameDataLoadResult data, string eventId, int option)
        {
            if (!Implemented.Contains(eventId)) throw new System.ArgumentException($"event {eventId} not implemented");
            if (!IsEnabled(run, data, eventId, option)) throw new System.InvalidOperationException($"{eventId} option {option} is disabled");
            var o = new EventOutcome { NodeId = run.CurrentNodeId, EventId = eventId, Option = option };
            if (option == 3) return o;
            var rng = InnerRandom(run);
            switch (eventId)
            {
                case "EVT_DEVIL_PACT":
                    if (option == 1)
                    {
                        o.MaxHpDelta = -10;
                        o.CardChoices = NormalCards(run, data, rng, 3);
                        o.CardChooseOne = true;
                    }
                    else
                    {
                        o.CursesToAdd.Add(rng.Next(0, 2) == 0 ? "CUR_002" : "CUR_004"); // D-45, D-77: 저주 먼저
                        o.RelicChoices = UnownedEliteRelics(run, data);
                    }
                    break;
                case "EVT_BLOOD_ALTAR":
                    if (option == 1)
                    {
                        o.HpDelta = -20;
                        o.NeedsUpgradeChoice = true;
                    }
                    else
                    {
                        o.NeedsSacrificeChoice = true;
                        o.RandomUpgradeAfterSacrifice = true;
                    }
                    break;
                case "EVT_CURSED_CHEST":
                    if (option == 1)
                    {
                        var relics = UnownedEliteRelics(run, data);
                        o.AutoRelic = relics[rng.Next(0, relics.Count)]; // D-68 무작위 1개, D-77: 유물 먼저
                        if (rng.Chance(0.4)) o.CursesToAdd.Add("CUR_003");
                    }
                    else if (run.Stage1Build) o.HpDelta = 6;
                    else o.GoldDelta = 40;
                    break;
                case "EVT_SOUL_PRISON":
                    if (option == 1)
                    {
                        if (rng.Chance(0.5))
                        {
                            o.CardChoices = NormalCards(run, data, rng, 3);
                            o.CardChooseOne = true;
                        }
                        else o.ForcedBattle = true;
                    }
                    else o.HpDelta = 12;
                    break;
                case "EVT_WANDER_MERCHANT":
                    if (option == 1)
                    {
                        o.GoldDelta = -8;
                        o.CardChoices = NormalCards(run, data, rng, 3);
                        o.CardChooseOne = true;
                    }
                    else
                    {
                        o.HpDelta = -4;
                        o.CardChoices = NormalCards(run, data, rng, 1);
                    }
                    break;
                case "EVT_FORGOTTEN_SHRINE":
                    if (option == 1) o.HpDelta = 15;
                    else
                    {
                        o.NeedsSacrificeChoice = true;
                        o.NeedsUpgradeChoice = true;
                    }
                    break;
                case "EVT_DARK_WAGER":
                    if (option == 1)
                    {
                        if (rng.Chance(0.5))
                        {
                            if (run.Stage1Build) o.HpDelta = 18;
                            else o.GoldDelta = 25;
                        }
                        else o.HpDelta = -12;
                    }
                    else if (rng.Chance(0.5)) o.CardChoices = NormalCards(run, data, rng, 1);
                    else o.CursesToAdd.Add("CUR_002");
                    break;
            }
            return o;
        }

        /// <summary>
        /// 마지막 확인 (D-67). 입력을 모두 검사한 뒤에만 런을 바꾼다. 이미 확인했거나 입력이 잘못되면 false.
        /// 노드 완료는 호출하는 쪽(강제 전투면 전투·보상 뒤).
        /// </summary>
        public static bool Confirm(RunState run, GameDataLoadResult data, EventOutcome o, EventChoice choice)
        {
            if (o == null || o.Confirmed || run.Outcome != RunOutcome.InProgress) return false;
            choice = choice ?? new EventChoice();
            if (o.CardChooseOne && choice.CardIndex.HasValue && (choice.CardIndex < 0 || choice.CardIndex >= o.CardChoices.Count)) return false;
            if (o.RelicChoices.Count > 0 && (!choice.RelicIndex.HasValue || choice.RelicIndex < 0 || choice.RelicIndex >= o.RelicChoices.Count)) return false;

            RunCard sacrifice = null;
            if (o.NeedsSacrificeChoice)
            {
                sacrifice = RunDeckRules.MapSacrificeCandidates(run, data.Cards).FirstOrDefault(c => c.InstanceId == choice.SacrificeInstanceId);
                if (sacrifice == null) return false;
            }
            RunCard upgrade = null;
            if (o.NeedsUpgradeChoice)
            {
                upgrade = run.Deck.FirstOrDefault(c => c.InstanceId == choice.UpgradeInstanceId);
                if (upgrade == null || upgrade == sacrifice || !RunDeckRules.IsUpgradable(upgrade, data.Cards)) return false;
            }

            if (sacrifice != null) run.Deck.Remove(sacrifice); // 전투 희생 트리거 없음 (D-68)
            if (o.RandomUpgradeAfterSacrifice)
            {
                var ups = run.Deck.Where(c => RunDeckRules.IsUpgradable(c, data.Cards)).ToList();
                if (ups.Count > 0) ups[InnerRandom(run).Next(0, ups.Count)].Upgraded = true; // 이 선택지는 그 전에 난수를 쓰지 않는다
            }
            if (upgrade != null) upgrade.Upgraded = true;

            if (o.MaxHpDelta != 0)
            {
                run.MaxHp += o.MaxHpDelta;
                run.CurrentHp = System.Math.Min(run.CurrentHp, run.MaxHp);
            }
            if (o.HpDelta > 0) run.CurrentHp = System.Math.Min(run.MaxHp, run.CurrentHp + o.HpDelta);
            else if (o.HpDelta < 0) run.CurrentHp += o.HpDelta;
            run.Gold += o.GoldDelta;
            foreach (var curse in o.CursesToAdd) run.AddCard(curse);
            if (o.RelicChoices.Count > 0) run.Relics.Add(o.RelicChoices[choice.RelicIndex.Value]);
            if (!string.IsNullOrEmpty(o.AutoRelic) && !run.Relics.Contains(o.AutoRelic)) run.Relics.Add(o.AutoRelic);
            if (o.CardChoices.Count > 0)
            {
                if (!o.CardChooseOne) run.AddCard(o.CardChoices[0]);
                else if (choice.CardIndex.HasValue) run.AddCard(o.CardChoices[choice.CardIndex.Value]);
            }
            o.Confirmed = true;
            if (run.CurrentHp <= 0)
            {
                run.CurrentHp = 0;
                run.Outcome = RunOutcome.Defeat; // D-51
            }
            return true;
        }
    }
}
