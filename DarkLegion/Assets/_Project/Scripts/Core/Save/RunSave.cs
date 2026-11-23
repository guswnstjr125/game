using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DarkLegion.Core
{
    /// <summary>런 저장 단계 (`14` §2·§4, D-67). 전투 중 상태는 저장하지 않는다.</summary>
    public enum RunSavePhase
    {
        Map,
        BattleEntry,
        RewardPending,
        RestPending,
        SettlementPending
    }

    /// <summary>진행 중 런 (`14` §2, PR #89 1단계 변환). 메타 화폐는 meta.json이다.</summary>
    public sealed class SaveData
    {
        public int SaveVersion = 1;
        public string RunId;
        public bool TutorialEnabled;
        public bool TutorialCompleted;
        public RunSavePhase Phase;
        public RewardPendingDTO Reward;
        public RestPendingDTO Rest;
        public SettlementPendingDTO Settlement;
        public EventPendingDTO BattleEntryEvent;
        public PlayerSaveDTO Player;
        public List<CardSaveDTO> Deck;
        public int NextCardInstanceId;
        public MapSaveDTO Map;
        public List<RelicSaveDTO> Relics;
        public long RngSeed;
        public string StartClass;
        public int Floor;
        public int StageIndex;
        public int Gold;
        public int FusionCharge;
        public bool FreeFusionUsed;
        public bool FreeFusionPending;
        public List<string> Consumables = new List<string>();
        public int ShopRemovalsUsed;
        public ShopVisitDTO Shop;
    }

    /// <summary>보상 대기 선택 상태 (`14` §4, D-67).</summary>
    public sealed class RewardPendingDTO
    {
        public string NodeId;
        public RewardSource Source;
        public int Gold;
        public List<string> CardChoices = new List<string>();
        public List<string> RelicChoices = new List<string>();
        public string AutoRelic;
        public int SelectedCard = -1;
        public int SelectedRelic = -1;
        public EventPendingDTO Event;
    }

    /// <summary>D-67 이벤트에서 저장하는 선택 진행 또는 확정 결과.</summary>
    public sealed class EventPendingDTO
    {
        public string NodeId;
        public string EventId;
        public EventScreenPhase Phase;
        public EventOutcome Outcome;
        public EventChoice Choice;
        public EventScreenResult Result;
    }

    /// <summary>확정된 휴식 효과. 런 상태에는 효과 적용 후 값이 들어 있다 (D-84).</summary>
    public sealed class RestPendingDTO
    {
        public string NodeId;
        public bool Healed;
        public bool Upgraded;
        public bool ContinuedOnly;
        public int HpBefore;
        public int HpAfter;
        public int MaxHp;
        public int UpgradedInstanceId;
        public string UpgradedCardId;
    }

    /// <summary>정산 재개에 필요한 입력은 저장 시점에 고정한다 (`14` §2·§4).</summary>
    public sealed class SettlementPendingDTO
    {
        public string RunId;
        public RunOutcome Outcome;
        public int ReachedFloor;
        public int ReachedStageIndex;
        public List<int> ClearedFloors = new List<int>();
        public int MetaSoul;
        public int MetaSigil;
        public MetaStatsDTO Stats;
        public List<string> NewAchievements = new List<string>();
    }

    public sealed class PlayerSaveDTO
    {
        public int CurrentHp;
        public int MaxHp;
    }

    public sealed class CardSaveDTO
    {
        public int InstanceId;
        public string CardId;
        public int UpgradeLevel;
        public bool FromFusion;
    }

    public sealed class MapSaveDTO
    {
        public string CurrentNodeId;
        public List<string> VisitedNodeIds;
        public Dictionary<string, string> NodeEventIds;
    }

    public sealed class ShopVisitDTO
    {
        public List<int> PurchasedSlots = new List<int>();
        public int RefreshCount;
    }

    public sealed class RelicSaveDTO
    {
        public string RelicId;
        public bool UsedThisRun;
        public int Counter;
    }

    /// <summary>RunState와 SaveData를 맞춘다 (`14` §2 1단계 변환표, D-67).</summary>
    public static class RunSave
    {
        public static SaveData Capture(RunState run, RunSavePhase phase)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (phase != RunSavePhase.Map && phase != RunSavePhase.BattleEntry)
                throw new InvalidOperationException($"phase {phase} is not captured yet");
            return new SaveData
            {
                SaveVersion = 1,
                RunId = run.RunId,
                TutorialEnabled = run.TutorialEnabled,
                TutorialCompleted = run.TutorialCompleted,
                Phase = phase,
                Player = new PlayerSaveDTO { CurrentHp = run.CurrentHp, MaxHp = run.MaxHp },
                Deck = run.Deck.Select(c => new CardSaveDTO
                {
                    InstanceId = c.InstanceId,
                    CardId = c.CardId,
                    UpgradeLevel = c.Upgraded ? 1 : 0,
                    FromFusion = c.FromFusion
                }).ToList(),
                NextCardInstanceId = run.NextCardInstanceId,
                Map = new MapSaveDTO
                {
                    CurrentNodeId = run.CurrentNodeId,
                    VisitedNodeIds = run.VisitedNodeIds.ToList(),
                    NodeEventIds = new Dictionary<string, string>(run.NodeEventIds)
                },
                Relics = run.Relics.Select(id => new RelicSaveDTO { RelicId = id, UsedThisRun = run.RelicUsedThisRun.TryGetValue(id, out bool used) && used, Counter = run.RelicCounters.TryGetValue(id, out int counter) ? counter : 0 }).ToList(),
                RngSeed = run.RngSeed,
                StartClass = run.StartClass,
                Floor = run.Floor,
                StageIndex = run.StageIndex,
                Gold = run.Gold,
                FusionCharge = run.FusionCharge,
                FreeFusionUsed = run.FreeFusionUsed,
                FreeFusionPending = run.FreeFusionPending,
                Consumables = run.Consumables.ToList(),
                ShopRemovalsUsed = run.ShopRemovalsUsed,
                Shop = run.Shop == null ? null : new ShopVisitDTO { PurchasedSlots = run.Shop.PurchasedSlots.ToList(), RefreshCount = run.Shop.RefreshCount }
            };
        }

        public static SaveData CaptureRest(RunState run, RestScreenResult result)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (result == null || result.NodeId != run.CurrentNodeId || (!result.Healed && !result.Upgraded && !result.ContinuedOnly))
                throw new InvalidOperationException("invalid rest result");
            if (result.HpAfter != run.CurrentHp || result.MaxHp != run.MaxHp)
                throw new InvalidOperationException("rest result does not match run state");
            var save = Capture(run, RunSavePhase.Map);
            save.Phase = RunSavePhase.RestPending;
            save.Rest = new RestPendingDTO { NodeId = result.NodeId, Healed = result.Healed, Upgraded = result.Upgraded, ContinuedOnly = result.ContinuedOnly, HpBefore = result.HpBefore, HpAfter = result.HpAfter, MaxHp = result.MaxHp, UpgradedInstanceId = result.UpgradedInstanceId, UpgradedCardId = result.UpgradedCardId };
            return save;
        }

        public static SaveData CaptureRewardPending(RunState run, PendingReward reward, int selectedCard = -1, int selectedRelic = -1)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (reward == null || reward.Confirmed || reward.NodeId != run.CurrentNodeId) throw new InvalidOperationException("invalid pending reward");
            if (selectedCard < -1 || selectedCard >= reward.CardChoices.Count || selectedRelic < -1 || selectedRelic >= reward.RelicChoices.Count) throw new ArgumentOutOfRangeException("reward selection");
            var save = Capture(run, RunSavePhase.Map);
            save.Phase = RunSavePhase.RewardPending;
            save.Reward = new RewardPendingDTO { NodeId = reward.NodeId, Source = reward.Source, Gold = reward.Gold, CardChoices = reward.CardChoices.ToList(), RelicChoices = reward.RelicChoices.ToList(), AutoRelic = reward.AutoRelic, SelectedCard = selectedCard, SelectedRelic = selectedRelic };
            return save;
        }

        public static PendingReward RestoreReward(RewardPendingDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            return new PendingReward { NodeId = dto.NodeId, Source = dto.Source, Gold = dto.Gold, CardChoices = dto.CardChoices?.ToList() ?? new List<string>(), RelicChoices = dto.RelicChoices?.ToList() ?? new List<string>(), AutoRelic = dto.AutoRelic };
        }

        public static SaveData CaptureEventPending(RunState run, EventPendingDTO pending)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (pending == null || pending.NodeId != run.CurrentNodeId || string.IsNullOrEmpty(pending.EventId)) throw new InvalidOperationException("invalid event pending");
            if (pending.Phase == EventScreenPhase.Picking && pending.Outcome == null) throw new InvalidOperationException("event choice outcome is missing");
            if (pending.Phase == EventScreenPhase.Result && (pending.Outcome == null || !pending.Outcome.Confirmed || pending.Result == null)) throw new InvalidOperationException("event result is incomplete");
            if (pending.Phase != EventScreenPhase.Choosing && pending.Phase != EventScreenPhase.Picking && pending.Phase != EventScreenPhase.Result) throw new InvalidOperationException("unknown event phase");
            var save = Capture(run, RunSavePhase.Map);
            save.Phase = RunSavePhase.RewardPending;
            save.Reward = new RewardPendingDTO { NodeId = pending.NodeId, Event = CloneEventPending(pending) };
            return save;
        }

        public static SaveData CaptureForcedBattleEntry(RunState run, EventPendingDTO pending)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (pending == null || pending.NodeId != run.CurrentNodeId || pending.Phase != EventScreenPhase.Result || pending.Outcome == null || !pending.Outcome.ForcedBattle || pending.Result == null || !pending.Result.ForcedBattle)
                throw new InvalidOperationException("forced battle event is not confirmed");
            var save = Capture(run, RunSavePhase.BattleEntry);
            save.BattleEntryEvent = CloneEventPending(pending);
            return save;
        }

        internal static EventPendingDTO CloneEventPending(EventPendingDTO source)
        {
            if (source == null) return null;
            EventOutcome o = null;
            if (source.Outcome != null)
            {
                var a = source.Outcome;
                o = new EventOutcome { NodeId = a.NodeId, EventId = a.EventId, Option = a.Option, HpDelta = a.HpDelta, MaxHpDelta = a.MaxHpDelta, GoldDelta = a.GoldDelta, CursesToAdd = a.CursesToAdd.ToList(), CardChoices = a.CardChoices.ToList(), CardChooseOne = a.CardChooseOne, RelicChoices = a.RelicChoices.ToList(), AutoRelic = a.AutoRelic, NeedsUpgradeChoice = a.NeedsUpgradeChoice, NeedsSacrificeChoice = a.NeedsSacrificeChoice, RandomUpgradeAfterSacrifice = a.RandomUpgradeAfterSacrifice, ForcedBattle = a.ForcedBattle, Confirmed = a.Confirmed };
            }
            EventChoice c = source.Choice == null ? null : new EventChoice { CardIndex = source.Choice.CardIndex, RelicIndex = source.Choice.RelicIndex, UpgradeInstanceId = source.Choice.UpgradeInstanceId, SacrificeInstanceId = source.Choice.SacrificeInstanceId };
            EventScreenResult r = null;
            if (source.Result != null)
            {
                var x = source.Result;
                r = new EventScreenResult { NodeId = x.NodeId, EventId = x.EventId, Option = x.Option, Text = x.Text, HpBefore = x.HpBefore, HpAfter = x.HpAfter, MaxHpBefore = x.MaxHpBefore, MaxHpAfter = x.MaxHpAfter, GoldBefore = x.GoldBefore, GoldAfter = x.GoldAfter, CardsGained = x.CardsGained.ToArray(), CursesGained = x.CursesGained.ToArray(), RelicGained = x.RelicGained, SacrificedCardId = x.SacrificedCardId, UpgradedCardIds = x.UpgradedCardIds.ToArray(), ForcedBattle = x.ForcedBattle, RunOutcome = x.RunOutcome };
            }
            return new EventPendingDTO { NodeId = source.NodeId, EventId = source.EventId, Phase = source.Phase, Outcome = o, Choice = c, Result = r };
        }

        public static SaveData CaptureSettlement(RunState run, SettlementPendingDTO settlement)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (settlement == null || settlement.RunId != run.RunId || settlement.Outcome == RunOutcome.InProgress || run.Outcome != settlement.Outcome) throw new InvalidOperationException("invalid settlement pending");
            var save = Capture(run, RunSavePhase.Map);
            save.Phase = RunSavePhase.SettlementPending;
            save.Settlement = settlement;
            return save;
        }

        public static RunState Restore(SaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.SaveVersion != 1) throw new InvalidOperationException($"save version {save.SaveVersion}");
            if (save.Phase != RunSavePhase.Map && save.Phase != RunSavePhase.BattleEntry && save.Phase != RunSavePhase.RewardPending && save.Phase != RunSavePhase.RestPending && save.Phase != RunSavePhase.SettlementPending)
                throw new InvalidOperationException($"phase {save.Phase} is not restored yet");
            if (save.Phase == RunSavePhase.RewardPending && (save.Reward == null || save.Reward.NodeId != save.Map?.CurrentNodeId)) throw new InvalidOperationException("reward pending is invalid");
            if (save.Phase == RunSavePhase.RewardPending && save.Reward.Event == null && (!Enum.IsDefined(typeof(RewardSource), save.Reward.Source) || save.Reward.Gold < 0 || save.Reward.SelectedCard < -1 || save.Reward.SelectedCard >= save.Reward.CardChoices.Count || save.Reward.SelectedRelic < -1 || save.Reward.SelectedRelic >= save.Reward.RelicChoices.Count)) throw new InvalidOperationException("reward choices are invalid");
            if (save.Phase == RunSavePhase.RestPending && (save.Rest == null || save.Rest.NodeId == null || save.Rest.NodeId != (save.Map?.CurrentNodeId))) throw new InvalidOperationException("rest pending is invalid");
            if (save.Phase == RunSavePhase.RestPending && ((save.Rest.Healed ? 1 : 0) + (save.Rest.Upgraded ? 1 : 0) + (save.Rest.ContinuedOnly ? 1 : 0) != 1 || save.Rest.HpAfter != save.Player?.CurrentHp || save.Rest.MaxHp != save.Player?.MaxHp)) throw new InvalidOperationException("rest result does not match saved run");
            if (save.Phase == RunSavePhase.SettlementPending && save.Settlement == null) throw new InvalidOperationException("settlement pending is missing");
            if (save.Phase == RunSavePhase.SettlementPending && (save.Settlement.RunId != save.RunId || save.Settlement.Outcome == RunOutcome.InProgress)) throw new InvalidOperationException("settlement does not match run");
            if (save.Phase != RunSavePhase.RewardPending && save.Reward != null) throw new InvalidOperationException("unexpected reward pending body");
            if (save.Phase != RunSavePhase.RestPending && save.Rest != null) throw new InvalidOperationException("unexpected rest pending body");
            if (save.Phase != RunSavePhase.SettlementPending && save.Settlement != null) throw new InvalidOperationException("unexpected settlement pending body");
            if (save.BattleEntryEvent != null && (save.Phase != RunSavePhase.BattleEntry || save.BattleEntryEvent.Outcome == null || !save.BattleEntryEvent.Outcome.ForcedBattle || save.BattleEntryEvent.Result == null || !save.BattleEntryEvent.Result.ForcedBattle || save.BattleEntryEvent.NodeId != save.Map?.CurrentNodeId)) throw new InvalidOperationException("unexpected battle-entry event");
            if (save.Floor < 1 || save.Floor > 5 || save.StageIndex < 0 || save.StageIndex > 8)
                throw new InvalidOperationException($"bad position F{save.Floor} index {save.StageIndex}");
            if (save.Gold < 0 || save.FusionCharge < 0 || save.FusionCharge > FusionSystem.MaxCharge || save.ShopRemovalsUsed < 0 || save.ShopRemovalsUsed > 2)
                throw new InvalidOperationException("run economy state is invalid");
            if (save.Consumables == null || save.Consumables.Count > 3 || save.Consumables.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("consumables are invalid");
            if (save.Shop != null && (save.Shop.RefreshCount < 0 || save.Shop.PurchasedSlots == null || save.Shop.PurchasedSlots.Any(i => i < 0 || i > 6) || save.Shop.PurchasedSlots.Distinct().Count() != save.Shop.PurchasedSlots.Count)) throw new InvalidOperationException("shop state is invalid");
            string node = RunState.NodeId(save.Floor, save.StageIndex + 1);
            if (save.Map == null || save.Map.CurrentNodeId != node)
                throw new InvalidOperationException("map node does not match floor and stage");
            if (save.Player == null) throw new InvalidOperationException("player missing");
            if (save.Player.MaxHp < 1 || save.Player.CurrentHp < 0 || save.Player.CurrentHp > save.Player.MaxHp) throw new InvalidOperationException("player HP is invalid");
            if (save.Deck == null) throw new InvalidOperationException("deck missing");

            var seen = new HashSet<int>();
            int maxId = 0;
            foreach (var card in save.Deck)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId)) throw new InvalidOperationException("card id missing");
                if (card.UpgradeLevel != 0 && card.UpgradeLevel != 1) throw new InvalidOperationException($"upgrade {card.UpgradeLevel}");
                if (card.InstanceId < 1 || !seen.Add(card.InstanceId)) throw new InvalidOperationException($"instance id {card.InstanceId}");
                if (card.InstanceId > maxId) maxId = card.InstanceId;
            }
            if (save.NextCardInstanceId <= maxId) throw new InvalidOperationException("next instance id is not greater than existing ids");

            var run = new RunState
            {
                RunId = save.RunId,
                RngSeed = save.RngSeed,
                StartClass = save.StartClass,
                Floor = save.Floor,
                StageIndex = save.StageIndex,
                LastFloor = 2,
                Stage1Build = true,
                CurrentHp = save.Player.CurrentHp,
                MaxHp = save.Player.MaxHp,
                Gold = save.Gold,
                FusionCharge = save.FusionCharge,
                FreeFusionUsed = save.FreeFusionUsed,
                FreeFusionPending = save.FreeFusionPending,
                Consumables = save.Consumables == null ? new List<string>() : save.Consumables.ToList(),
                ShopRemovalsUsed = save.ShopRemovalsUsed,
                Shop = save.Shop == null ? null : new ShopVisitDTO { PurchasedSlots = save.Shop.PurchasedSlots.ToList(), RefreshCount = save.Shop.RefreshCount },
                NextCardInstanceId = save.NextCardInstanceId,
                TutorialEnabled = save.TutorialEnabled,
                TutorialCompleted = save.TutorialCompleted,
                Outcome = save.Phase == RunSavePhase.SettlementPending ? save.Settlement.Outcome : RunOutcome.InProgress,
                VisitedNodeIds = save.Map.VisitedNodeIds == null ? new List<string>() : save.Map.VisitedNodeIds.ToList(),
                NodeEventIds = save.Map.NodeEventIds == null ? new Dictionary<string, string>() : new Dictionary<string, string>(save.Map.NodeEventIds)
            };
            foreach (var card in save.Deck)
            {
                run.Deck.Add(new RunCard
                {
                    InstanceId = card.InstanceId,
                    CardId = card.CardId,
                    Upgraded = card.UpgradeLevel == 1,
                    FromFusion = card.FromFusion
                });
            }
            if (save.Relics != null)
            {
                foreach (var relic in save.Relics)
                {
                    if (relic == null || string.IsNullOrEmpty(relic.RelicId)) throw new InvalidOperationException("relic id missing");
                    if (relic.Counter < 0 || run.Relics.Contains(relic.RelicId)) throw new InvalidOperationException("relic save is invalid");
                    run.Relics.Add(relic.RelicId);
                    run.RelicUsedThisRun[relic.RelicId] = relic.UsedThisRun;
                    run.RelicCounters[relic.RelicId] = relic.Counter;
                }
            }
            return run;
        }
    }

    /// <summary>SaveData JSON. 읽기는 Core <see cref="Json"/> (D-74). 파일 교체는 하지 않는다.</summary>
    public static class SaveJson
    {
        public static int ReadVersion(string json)
        {
            var root = Json.ParseExactIntegers(json) as Dictionary<string, object> ?? throw new InvalidOperationException("save root is not an object");
            return Int(root, "SaveVersion");
        }

        public static string Write(SaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            var sb = new StringBuilder();
            sb.Append('{');
            Field(sb, "SaveVersion", save.SaveVersion, true);
            Field(sb, "RunId", save.RunId);
            Field(sb, "TutorialEnabled", save.TutorialEnabled);
            Field(sb, "TutorialCompleted", save.TutorialCompleted);
            Field(sb, "Phase", save.Phase.ToString());
            sb.Append(",\"Reward\":");
            if (save.Reward == null) sb.Append("null");
            else
            {
                var r = save.Reward;
                sb.Append("{\"NodeId\":").Append(Quote(r.NodeId)).Append(",\"Source\":").Append(Quote(r.Source.ToString())).Append(",\"Gold\":").Append(r.Gold);
                sb.Append(",\"CardChoices\":"); StringArray(sb, r.CardChoices);
                sb.Append(",\"RelicChoices\":"); StringArray(sb, r.RelicChoices);
                sb.Append(",\"AutoRelic\":").Append(QuoteNullable(r.AutoRelic)).Append(",\"SelectedCard\":").Append(r.SelectedCard).Append(",\"SelectedRelic\":").Append(r.SelectedRelic).Append(",\"Event\":");
                if (r.Event == null) sb.Append("null"); else WriteEventPending(sb, r.Event);
                sb.Append('}');
            }
            sb.Append(",\"Rest\":");
            if (save.Rest == null) sb.Append("null");
            else sb.Append("{\"NodeId\":").Append(Quote(save.Rest.NodeId)).Append(",\"Healed\":").Append(save.Rest.Healed ? "true" : "false").Append(",\"Upgraded\":").Append(save.Rest.Upgraded ? "true" : "false").Append(",\"ContinuedOnly\":").Append(save.Rest.ContinuedOnly ? "true" : "false").Append(",\"HpBefore\":").Append(save.Rest.HpBefore).Append(",\"HpAfter\":").Append(save.Rest.HpAfter).Append(",\"MaxHp\":").Append(save.Rest.MaxHp).Append(",\"UpgradedInstanceId\":").Append(save.Rest.UpgradedInstanceId).Append(",\"UpgradedCardId\":").Append(QuoteNullable(save.Rest.UpgradedCardId)).Append('}');
            sb.Append(",\"Settlement\":");
            if (save.Settlement == null) sb.Append("null");
            else
            {
                var s = save.Settlement;
                sb.Append("{\"RunId\":").Append(Quote(s.RunId)).Append(",\"Outcome\":").Append(Quote(s.Outcome.ToString())).Append(",\"ReachedFloor\":").Append(s.ReachedFloor).Append(",\"ReachedStageIndex\":").Append(s.ReachedStageIndex);
                sb.Append(",\"ClearedFloors\":[").Append(string.Join(",", s.ClearedFloors.Select(n => n.ToString(CultureInfo.InvariantCulture)))).Append(']');
                sb.Append(",\"MetaSoul\":").Append(s.MetaSoul).Append(",\"MetaSigil\":").Append(s.MetaSigil).Append(",\"NewAchievements\":"); StringArray(sb, s.NewAchievements);
                sb.Append(",\"Stats\":"); WriteStats(sb, s.Stats); sb.Append('}');
            }
            sb.Append(",\"BattleEntryEvent\":");
            if (save.BattleEntryEvent == null) sb.Append("null"); else WriteEventPending(sb, save.BattleEntryEvent);
            sb.Append(",\"Player\":{\"CurrentHp\":").Append(save.Player.CurrentHp.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"MaxHp\":").Append(save.Player.MaxHp.ToString(CultureInfo.InvariantCulture)).Append('}');
            sb.Append(",\"Deck\":[");
            for (int i = 0; i < save.Deck.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var c = save.Deck[i];
                sb.Append("{\"InstanceId\":").Append(c.InstanceId.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"CardId\":").Append(Quote(c.CardId));
                sb.Append(",\"UpgradeLevel\":").Append(c.UpgradeLevel.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"FromFusion\":").Append(c.FromFusion ? "true" : "false").Append('}');
            }
            sb.Append(']');
            Field(sb, "NextCardInstanceId", save.NextCardInstanceId);
            sb.Append(",\"Map\":{\"CurrentNodeId\":").Append(Quote(save.Map.CurrentNodeId));
            sb.Append(",\"VisitedNodeIds\":[");
            for (int i = 0; i < save.Map.VisitedNodeIds.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Quote(save.Map.VisitedNodeIds[i]));
            }
            sb.Append("],\"NodeEventIds\":{");
            bool firstEvent = true;
            foreach (var pair in save.Map.NodeEventIds)
            {
                if (!firstEvent) sb.Append(',');
                firstEvent = false;
                sb.Append(Quote(pair.Key)).Append(':').Append(Quote(pair.Value));
            }
            sb.Append("}}");
            sb.Append(",\"Relics\":[");
            for (int i = 0; i < save.Relics.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = save.Relics[i];
                sb.Append("{\"RelicId\":").Append(Quote(r.RelicId));
                sb.Append(",\"UsedThisRun\":").Append(r.UsedThisRun ? "true" : "false");
                sb.Append(",\"Counter\":").Append(r.Counter.ToString(CultureInfo.InvariantCulture)).Append('}');
            }
            sb.Append(']');
            Field(sb, "RngSeed", save.RngSeed);
            Field(sb, "StartClass", save.StartClass);
            Field(sb, "Floor", save.Floor);
            Field(sb, "StageIndex", save.StageIndex);
            Field(sb, "Gold", save.Gold);
            Field(sb, "FusionCharge", save.FusionCharge);
            Field(sb, "FreeFusionUsed", save.FreeFusionUsed);
            Field(sb, "FreeFusionPending", save.FreeFusionPending);
            sb.Append(",\"Consumables\":"); StringArray(sb, save.Consumables);
            Field(sb, "ShopRemovalsUsed", save.ShopRemovalsUsed);
            sb.Append(",\"Shop\":");
            if (save.Shop == null) sb.Append("null");
            else sb.Append("{\"PurchasedSlots\":[").Append(string.Join(",", save.Shop.PurchasedSlots.Select(n => n.ToString(CultureInfo.InvariantCulture)))).Append("],\"RefreshCount\":").Append(save.Shop.RefreshCount).Append('}');
            sb.Append('}');
            return sb.ToString();
        }

        public static SaveData Read(string json)
        {
            var root = Json.ParseExactIntegers(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("save root is not an object");
            var save = new SaveData
            {
                SaveVersion = Int(root, "SaveVersion"),
                RunId = Str(root, "RunId"),
                TutorialEnabled = Bool(root, "TutorialEnabled"),
                TutorialCompleted = Bool(root, "TutorialCompleted"),
                Phase = (RunSavePhase)Enum.Parse(typeof(RunSavePhase), Str(root, "Phase")),
                Player = Player(Obj(root, "Player")),
                Deck = Deck(Arr(root, "Deck")),
                NextCardInstanceId = Int(root, "NextCardInstanceId"),
                Map = Map(Obj(root, "Map")),
                Relics = Relics(Arr(root, "Relics")),
                RngSeed = Long(root, "RngSeed"),
                StartClass = Str(root, "StartClass"),
                Floor = Int(root, "Floor"),
                StageIndex = Int(root, "StageIndex"),
                Gold = Int(root, "Gold"),
                FusionCharge = Int(root, "FusionCharge")
            };
            save.FreeFusionUsed = OptionalBool(root, "FreeFusionUsed");
            save.FreeFusionPending = OptionalBool(root, "FreeFusionPending");
            if (root.TryGetValue("Consumables", out var consumables) && consumables != null) save.Consumables = StringList(consumables as List<object> ?? throw new InvalidOperationException("Consumables"));
            if (root.TryGetValue("ShopRemovalsUsed", out var removals) && removals != null) save.ShopRemovalsUsed = Int(root, "ShopRemovalsUsed");
            if (root.TryGetValue("Shop", out var shopValue) && shopValue != null)
            {
                var shop = shopValue as Dictionary<string, object> ?? throw new InvalidOperationException("Shop");
                save.Shop = new ShopVisitDTO { PurchasedSlots = IntList(Arr(shop, "PurchasedSlots")), RefreshCount = Int(shop, "RefreshCount") };
            }
            if (root.TryGetValue("Rest", out var restValue) && restValue != null)
            {
                var rest = restValue as Dictionary<string, object> ?? throw new InvalidOperationException("rest pending");
                save.Rest = new RestPendingDTO { NodeId = Str(rest, "NodeId"), Healed = Bool(rest, "Healed"), Upgraded = Bool(rest, "Upgraded"), ContinuedOnly = Bool(rest, "ContinuedOnly"), HpBefore = Int(rest, "HpBefore"), HpAfter = Int(rest, "HpAfter"), MaxHp = Int(rest, "MaxHp"), UpgradedInstanceId = Int(rest, "UpgradedInstanceId"), UpgradedCardId = NullableStr(rest, "UpgradedCardId") };
            }
            if (root.TryGetValue("Reward", out var rewardValue) && rewardValue != null)
            {
                var r = rewardValue as Dictionary<string, object> ?? throw new InvalidOperationException("reward pending");
                save.Reward = new RewardPendingDTO { NodeId = Str(r, "NodeId"), Source = (RewardSource)Enum.Parse(typeof(RewardSource), Str(r, "Source")), Gold = Int(r, "Gold"), CardChoices = StringList(Arr(r, "CardChoices")), RelicChoices = StringList(Arr(r, "RelicChoices")), AutoRelic = NullableStr(r, "AutoRelic"), SelectedCard = Int(r, "SelectedCard"), SelectedRelic = Int(r, "SelectedRelic") };
                if (r.TryGetValue("Event", out var eventValue) && eventValue != null) save.Reward.Event = ReadEventPending(eventValue as Dictionary<string, object> ?? throw new InvalidOperationException("event pending"));
            }
            if (root.TryGetValue("Settlement", out var settlementValue) && settlementValue != null)
            {
                var s = settlementValue as Dictionary<string, object> ?? throw new InvalidOperationException("settlement pending");
                save.Settlement = new SettlementPendingDTO { RunId = Str(s, "RunId"), Outcome = (RunOutcome)Enum.Parse(typeof(RunOutcome), Str(s, "Outcome")), ReachedFloor = Int(s, "ReachedFloor"), ReachedStageIndex = Int(s, "ReachedStageIndex"), ClearedFloors = IntList(Arr(s, "ClearedFloors")), MetaSoul = Int(s, "MetaSoul"), MetaSigil = Int(s, "MetaSigil"), NewAchievements = StringList(Arr(s, "NewAchievements")), Stats = ReadStats(Obj(s, "Stats")) };
            }
            if (root.TryGetValue("BattleEntryEvent", out var battleEventValue) && battleEventValue != null) save.BattleEntryEvent = ReadEventPending(battleEventValue as Dictionary<string, object> ?? throw new InvalidOperationException("battle-entry event"));
            return save;
        }

        private static void StringArray(StringBuilder sb, IEnumerable<string> values)
        {
            sb.Append('['); bool first = true;
            foreach (var value in values ?? Enumerable.Empty<string>()) { if (!first) sb.Append(','); first = false; sb.Append(Quote(value)); }
            sb.Append(']');
        }

        private static void WriteEventPending(StringBuilder b, EventPendingDTO e)
        {
            b.Append("{\"NodeId\":").Append(Quote(e.NodeId)).Append(",\"EventId\":").Append(Quote(e.EventId)).Append(",\"Phase\":").Append(Quote(e.Phase.ToString()));
            b.Append(",\"Outcome\":");
            if (e.Outcome == null) b.Append("null");
            else
            {
                var o = e.Outcome;
                b.Append("{\"NodeId\":").Append(Quote(o.NodeId)).Append(",\"EventId\":").Append(Quote(o.EventId)).Append(",\"Option\":").Append(o.Option).Append(",\"HpDelta\":").Append(o.HpDelta).Append(",\"MaxHpDelta\":").Append(o.MaxHpDelta).Append(",\"GoldDelta\":").Append(o.GoldDelta);
                b.Append(",\"CursesToAdd\":"); StringArray(b, o.CursesToAdd); b.Append(",\"CardChoices\":"); StringArray(b, o.CardChoices); b.Append(",\"CardChooseOne\":").Append(o.CardChooseOne ? "true" : "false");
                b.Append(",\"RelicChoices\":"); StringArray(b, o.RelicChoices); b.Append(",\"AutoRelic\":").Append(QuoteNullable(o.AutoRelic));
                b.Append(",\"NeedsUpgradeChoice\":").Append(o.NeedsUpgradeChoice ? "true" : "false").Append(",\"NeedsSacrificeChoice\":").Append(o.NeedsSacrificeChoice ? "true" : "false").Append(",\"RandomUpgradeAfterSacrifice\":").Append(o.RandomUpgradeAfterSacrifice ? "true" : "false").Append(",\"ForcedBattle\":").Append(o.ForcedBattle ? "true" : "false").Append(",\"Confirmed\":").Append(o.Confirmed ? "true" : "false").Append('}');
            }
            b.Append(",\"Choice\":");
            if (e.Choice == null) b.Append("null"); else b.Append("{\"CardIndex\":").Append(NullableInt(e.Choice.CardIndex)).Append(",\"RelicIndex\":").Append(NullableInt(e.Choice.RelicIndex)).Append(",\"UpgradeInstanceId\":").Append(NullableInt(e.Choice.UpgradeInstanceId)).Append(",\"SacrificeInstanceId\":").Append(NullableInt(e.Choice.SacrificeInstanceId)).Append('}');
            b.Append(",\"Result\":");
            if (e.Result == null) b.Append("null");
            else
            {
                var r = e.Result;
                b.Append("{\"NodeId\":").Append(Quote(r.NodeId)).Append(",\"EventId\":").Append(Quote(r.EventId)).Append(",\"Option\":").Append(r.Option).Append(",\"Text\":").Append(QuoteNullable(r.Text)).Append(",\"HpBefore\":").Append(r.HpBefore).Append(",\"HpAfter\":").Append(r.HpAfter).Append(",\"MaxHpBefore\":").Append(r.MaxHpBefore).Append(",\"MaxHpAfter\":").Append(r.MaxHpAfter).Append(",\"GoldBefore\":").Append(r.GoldBefore).Append(",\"GoldAfter\":").Append(r.GoldAfter);
                b.Append(",\"CardsGained\":"); StringArray(b, r.CardsGained); b.Append(",\"CursesGained\":"); StringArray(b, r.CursesGained); b.Append(",\"RelicGained\":").Append(QuoteNullable(r.RelicGained)).Append(",\"SacrificedCardId\":").Append(QuoteNullable(r.SacrificedCardId));
                b.Append(",\"UpgradedCardIds\":"); StringArray(b, r.UpgradedCardIds); b.Append(",\"ForcedBattle\":").Append(r.ForcedBattle ? "true" : "false").Append(",\"RunOutcome\":").Append(Quote(r.RunOutcome.ToString())).Append('}');
            }
            b.Append('}');
        }

        private static EventPendingDTO ReadEventPending(Dictionary<string, object> e)
        {
            var dto = new EventPendingDTO { NodeId = Str(e, "NodeId"), EventId = Str(e, "EventId"), Phase = (EventScreenPhase)Enum.Parse(typeof(EventScreenPhase), Str(e, "Phase")) };
            if (e.TryGetValue("Outcome", out var ov) && ov != null)
            {
                var o = ov as Dictionary<string, object> ?? throw new InvalidOperationException("event outcome");
                dto.Outcome = new EventOutcome { NodeId = Str(o, "NodeId"), EventId = Str(o, "EventId"), Option = Int(o, "Option"), HpDelta = Int(o, "HpDelta"), MaxHpDelta = Int(o, "MaxHpDelta"), GoldDelta = Int(o, "GoldDelta"), CursesToAdd = StringList(Arr(o, "CursesToAdd")), CardChoices = StringList(Arr(o, "CardChoices")), CardChooseOne = Bool(o, "CardChooseOne"), RelicChoices = StringList(Arr(o, "RelicChoices")), AutoRelic = NullableStr(o, "AutoRelic"), NeedsUpgradeChoice = Bool(o, "NeedsUpgradeChoice"), NeedsSacrificeChoice = Bool(o, "NeedsSacrificeChoice"), RandomUpgradeAfterSacrifice = Bool(o, "RandomUpgradeAfterSacrifice"), ForcedBattle = Bool(o, "ForcedBattle"), Confirmed = Bool(o, "Confirmed") };
            }
            if (e.TryGetValue("Choice", out var cv) && cv != null)
            {
                var c = cv as Dictionary<string, object> ?? throw new InvalidOperationException("event choice");
                dto.Choice = new EventChoice { CardIndex = OptionalInt(c, "CardIndex"), RelicIndex = OptionalInt(c, "RelicIndex"), UpgradeInstanceId = OptionalInt(c, "UpgradeInstanceId"), SacrificeInstanceId = OptionalInt(c, "SacrificeInstanceId") };
            }
            if (e.TryGetValue("Result", out var rv) && rv != null)
            {
                var r = rv as Dictionary<string, object> ?? throw new InvalidOperationException("event result");
                dto.Result = new EventScreenResult { NodeId = Str(r, "NodeId"), EventId = Str(r, "EventId"), Option = Int(r, "Option"), Text = NullableStr(r, "Text"), HpBefore = Int(r, "HpBefore"), HpAfter = Int(r, "HpAfter"), MaxHpBefore = Int(r, "MaxHpBefore"), MaxHpAfter = Int(r, "MaxHpAfter"), GoldBefore = Int(r, "GoldBefore"), GoldAfter = Int(r, "GoldAfter"), CardsGained = StringList(Arr(r, "CardsGained")).ToArray(), CursesGained = StringList(Arr(r, "CursesGained")).ToArray(), RelicGained = NullableStr(r, "RelicGained"), SacrificedCardId = NullableStr(r, "SacrificedCardId"), UpgradedCardIds = StringList(Arr(r, "UpgradedCardIds")).ToArray(), ForcedBattle = Bool(r, "ForcedBattle"), RunOutcome = (RunOutcome)Enum.Parse(typeof(RunOutcome), Str(r, "RunOutcome")) };
            }
            if ((dto.Phase == EventScreenPhase.Picking || dto.Phase == EventScreenPhase.Result) && dto.Outcome == null) throw new InvalidOperationException("event outcome missing");
            if (dto.Phase == EventScreenPhase.Result && (dto.Result == null || !dto.Outcome.Confirmed)) throw new InvalidOperationException("event result missing");
            return dto;
        }

        private static string NullableInt(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";
        private static int? OptionalInt(Dictionary<string, object> o, string key) { if (!o.TryGetValue(key, out var v) || v == null) return null; return IntValue(v); }

        private static void WriteStats(StringBuilder sb, MetaStatsDTO stats)
        {
            var s = stats ?? new MetaStatsDTO();
            sb.Append("{\"FloorClearCount\":[").Append(string.Join(",", s.FloorClearCount.Select(n => n.ToString(CultureInfo.InvariantCulture)))).Append(']');
            sb.Append(",\"TotalSacrifice\":").Append(s.TotalSacrifice).Append(",\"TotalFusion\":").Append(s.TotalFusion).Append(",\"TotalExecuteKill\":").Append(s.TotalExecuteKill).Append(",\"TotalBurnDamage\":").Append(s.TotalBurnDamage).Append(",\"TotalPoisonDamage\":").Append(s.TotalPoisonDamage).Append(",\"ClassWins\":{");
            bool first = true; foreach (var pair in s.ClassWins.OrderBy(p => p.Key, StringComparer.Ordinal)) { if (!first) sb.Append(','); first = false; sb.Append(Quote(pair.Key)).Append(':').Append(pair.Value); }
            sb.Append("},\"LastClass\":").Append(QuoteNullable(s.LastClass)).Append('}');
        }

        private static MetaStatsDTO ReadStats(Dictionary<string, object> o)
        {
            var floors = IntList(Arr(o, "FloorClearCount"));
            if (floors.Count != 5) throw new InvalidOperationException("FloorClearCount must have five entries");
            var wins = new Dictionary<string, int>(); foreach (var p in Obj(o, "ClassWins")) wins.Add(p.Key, IntValue(p.Value));
            return new MetaStatsDTO { FloorClearCount = floors.ToArray(), TotalSacrifice = Int(o, "TotalSacrifice"), TotalFusion = Int(o, "TotalFusion"), TotalExecuteKill = Int(o, "TotalExecuteKill"), TotalBurnDamage = Int(o, "TotalBurnDamage"), TotalPoisonDamage = Int(o, "TotalPoisonDamage"), ClassWins = wins, LastClass = NullableStr(o, "LastClass") };
        }

        private static void Field(StringBuilder sb, string name, int value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append(Quote(name)).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Field(StringBuilder sb, string name, long value)
        {
            sb.Append(',').Append(Quote(name)).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Field(StringBuilder sb, string name, string value)
        {
            sb.Append(',').Append(Quote(name)).Append(':').Append(Quote(value));
        }

        private static void Field(StringBuilder sb, string name, bool value)
        {
            sb.Append(',').Append(Quote(name)).Append(':').Append(value ? "true" : "false");
        }

        private static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string QuoteNullable(string value) => value == null ? "null" : Quote(value);

        private static Dictionary<string, object> Obj(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || !(v is Dictionary<string, object> d))
                throw new InvalidOperationException($"save field {key}");
            return d;
        }

        private static List<object> Arr(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || !(v is List<object> list))
                throw new InvalidOperationException($"save field {key}");
            return list;
        }

        private static List<string> StringList(List<object> list) => list.Select(v => v as string ?? throw new InvalidOperationException("string item expected")).ToList();

        private static List<int> IntList(List<object> list) => list.Select(IntValue).ToList();

        private static int IntValue(object value)
        {
            if (value is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
            throw new InvalidOperationException("integer item expected");
        }

        private static string Str(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || !(v is string s)) throw new InvalidOperationException($"save field {key}");
            return s;
        }

        private static string NullableStr(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v)) throw new InvalidOperationException($"save field {key}");
            if (v == null) return null;
            return v as string ?? throw new InvalidOperationException($"save field {key}");
        }

        private static bool Bool(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || !(v is bool b)) throw new InvalidOperationException($"save field {key}");
            return b;
        }

        private static bool OptionalBool(Dictionary<string, object> o, string key) => o.TryGetValue(key, out var v) && v != null ? v is bool b ? b : throw new InvalidOperationException($"save field {key}") : false;

        private static int Int(Dictionary<string, object> o, string key)
        {
            long n = Long(o, key);
            if (n < int.MinValue || n > int.MaxValue) throw new InvalidOperationException($"save field {key}");
            return (int)n;
        }

        private static long Long(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v)) throw new InvalidOperationException($"save field {key}");
            if (v is long exact) return exact;
            throw new InvalidOperationException($"save field {key}");
        }

        private static PlayerSaveDTO Player(Dictionary<string, object> o) =>
            new PlayerSaveDTO { CurrentHp = Int(o, "CurrentHp"), MaxHp = Int(o, "MaxHp") };

        private static List<CardSaveDTO> Deck(List<object> list)
        {
            var deck = new List<CardSaveDTO>();
            foreach (var item in list)
            {
                var o = item as Dictionary<string, object> ?? throw new InvalidOperationException("deck card");
                deck.Add(new CardSaveDTO
                {
                    InstanceId = Int(o, "InstanceId"),
                    CardId = Str(o, "CardId"),
                    UpgradeLevel = Int(o, "UpgradeLevel"),
                    FromFusion = Bool(o, "FromFusion")
                });
            }
            return deck;
        }

        private static MapSaveDTO Map(Dictionary<string, object> o)
        {
            var visited = new List<string>();
            foreach (var item in Arr(o, "VisitedNodeIds"))
            {
                if (!(item is string s)) throw new InvalidOperationException("visited node");
                visited.Add(s);
            }
            var events = new Dictionary<string, string>();
            foreach (var pair in Obj(o, "NodeEventIds"))
            {
                if (!(pair.Value is string s)) throw new InvalidOperationException("node event");
                events[pair.Key] = s;
            }
            return new MapSaveDTO { CurrentNodeId = Str(o, "CurrentNodeId"), VisitedNodeIds = visited, NodeEventIds = events };
        }

        private static List<RelicSaveDTO> Relics(List<object> list)
        {
            var relics = new List<RelicSaveDTO>();
            foreach (var item in list)
            {
                var o = item as Dictionary<string, object> ?? throw new InvalidOperationException("relic");
                relics.Add(new RelicSaveDTO
                {
                    RelicId = Str(o, "RelicId"),
                    UsedThisRun = Bool(o, "UsedThisRun"),
                    Counter = Int(o, "Counter")
                });
            }
            return relics;
        }
    }
}
