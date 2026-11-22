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
    }

    /// <summary>보상 대기. 카드·유물 후보 복구는 다음 조각이다 (`14` §4).</summary>
    public sealed class RewardPendingDTO
    {
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
                Relics = run.Relics.Select(id => new RelicSaveDTO { RelicId = id, UsedThisRun = false, Counter = 0 }).ToList(),
                RngSeed = run.RngSeed,
                StartClass = run.StartClass,
                Floor = run.Floor,
                StageIndex = run.StageIndex,
                Gold = run.Gold,
                FusionCharge = run.FusionCharge
            };
        }

        public static RunState Restore(SaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.SaveVersion != 1) throw new InvalidOperationException($"save version {save.SaveVersion}");
            if (save.Phase != RunSavePhase.Map && save.Phase != RunSavePhase.BattleEntry)
                throw new InvalidOperationException($"phase {save.Phase} is not restored yet");
            if (save.Reward != null) throw new InvalidOperationException("reward pending is not stored in this phase");
            if (save.Floor < 1 || save.Floor > 5 || save.StageIndex < 0 || save.StageIndex > 8)
                throw new InvalidOperationException($"bad position F{save.Floor} index {save.StageIndex}");
            string node = RunState.NodeId(save.Floor, save.StageIndex + 1);
            if (save.Map == null || save.Map.CurrentNodeId != node)
                throw new InvalidOperationException("map node does not match floor and stage");
            if (save.Player == null) throw new InvalidOperationException("player missing");
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
                NextCardInstanceId = save.NextCardInstanceId,
                TutorialEnabled = save.TutorialEnabled,
                TutorialCompleted = save.TutorialCompleted,
                Outcome = RunOutcome.InProgress,
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
                    run.Relics.Add(relic.RelicId);
                }
            }
            return run;
        }
    }

    /// <summary>SaveData JSON. 읽기는 Core <see cref="Json"/> (D-74). 파일 교체는 하지 않는다.</summary>
    public static class SaveJson
    {
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
            sb.Append(",\"Reward\":null");
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
            sb.Append('}');
            return sb.ToString();
        }

        public static SaveData Read(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
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
            if (root.TryGetValue("Reward", out var reward) && reward != null)
                throw new InvalidOperationException("reward body is not read yet");
            return save;
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

        private static string Str(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || !(v is string s)) throw new InvalidOperationException($"save field {key}");
            return s;
        }

        private static bool Bool(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || !(v is bool b)) throw new InvalidOperationException($"save field {key}");
            return b;
        }

        private static int Int(Dictionary<string, object> o, string key)
        {
            long n = Long(o, key);
            if (n < int.MinValue || n > int.MaxValue) throw new InvalidOperationException($"save field {key}");
            return (int)n;
        }

        private static long Long(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || !(v is double d) || d != Math.Truncate(d) || d < long.MinValue || d > long.MaxValue)
                throw new InvalidOperationException($"save field {key}");
            return (long)d;
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
