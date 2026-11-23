using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DarkLegion.Core
{
    public sealed class MetaSaveDTO
    {
        public int SaveVersion = 1;
        public List<string> UnlockedClasses = new List<string>();
        public List<string> PermanentUnlocks = new List<string>();
        public int MetaSoul;
        public int MetaSigil;
        public List<string> UnlockedCards = new List<string>();
        public List<string> UnlockedRelics = new List<string>();
        public Dictionary<string, bool> AchievementFlags = new Dictionary<string, bool>();
        public string LastSettledRunId;
        public SettlementReceiptDTO LastSettlement;
        public bool TutorialDone;
        public MetaStatsDTO Stats = new MetaStatsDTO();
        public static MetaSaveDTO New() => new MetaSaveDTO();
    }

    public sealed class MetaStatsDTO
    {
        public int[] FloorClearCount = new int[5];
        public int TotalSacrifice;
        public int TotalFusion;
        public int TotalExecuteKill;
        public int TotalBurnDamage;
        public int TotalPoisonDamage;
        public Dictionary<string, int> ClassWins = new Dictionary<string, int>();
        public string LastClass;
    }

    public sealed class SettlementReceiptDTO
    {
        public string RunId;
        public RunOutcome Outcome;
        public int MetaSoul;
        public int MetaSigil;
        public List<string> NewAchievements = new List<string>();
    }

    public static class MetaSaveJson
    {
        public static string Write(MetaSaveDTO m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            var b = new StringBuilder();
            b.Append("{\"SaveVersion\":").Append(m.SaveVersion).Append(",\"UnlockedClasses\":"); Strings(b, m.UnlockedClasses);
            b.Append(",\"PermanentUnlocks\":"); Strings(b, m.PermanentUnlocks);
            b.Append(",\"MetaSoul\":").Append(m.MetaSoul).Append(",\"MetaSigil\":").Append(m.MetaSigil);
            b.Append(",\"UnlockedCards\":"); Strings(b, m.UnlockedCards);
            b.Append(",\"UnlockedRelics\":"); Strings(b, m.UnlockedRelics);
            b.Append(",\"AchievementFlags\":{");
            bool first = true;
            foreach (var p in m.AchievementFlags.OrderBy(x => x.Key, StringComparer.Ordinal)) { if (!first) b.Append(','); first = false; b.Append(Quote(p.Key)).Append(':').Append(p.Value ? "true" : "false"); }
            b.Append("},\"LastSettledRunId\":").Append(Quote(m.LastSettledRunId)).Append(",\"LastSettlement\":");
            if (m.LastSettlement == null) b.Append("null");
            else { var r = m.LastSettlement; b.Append("{\"RunId\":").Append(Quote(r.RunId)).Append(",\"Outcome\":").Append(Quote(r.Outcome.ToString())).Append(",\"MetaSoul\":").Append(r.MetaSoul).Append(",\"MetaSigil\":").Append(r.MetaSigil).Append(",\"NewAchievements\":"); Strings(b, r.NewAchievements); b.Append('}'); }
            b.Append(",\"TutorialDone\":").Append(m.TutorialDone ? "true" : "false").Append(",\"Stats\":{");
            var s = m.Stats ?? new MetaStatsDTO();
            b.Append("\"FloorClearCount\":[").Append(string.Join(",", (s.FloorClearCount ?? new int[5]).Select(n => n.ToString(CultureInfo.InvariantCulture)))).Append(']');
            b.Append(",\"TotalSacrifice\":").Append(s.TotalSacrifice).Append(",\"TotalFusion\":").Append(s.TotalFusion).Append(",\"TotalExecuteKill\":").Append(s.TotalExecuteKill).Append(",\"TotalBurnDamage\":").Append(s.TotalBurnDamage).Append(",\"TotalPoisonDamage\":").Append(s.TotalPoisonDamage).Append(",\"ClassWins\":{");
            first = true;
            foreach (var p in s.ClassWins.OrderBy(x => x.Key, StringComparer.Ordinal)) { if (!first) b.Append(','); first = false; b.Append(Quote(p.Key)).Append(':').Append(p.Value); }
            b.Append("},\"LastClass\":").Append(Quote(s.LastClass)).Append("}}");
            return b.ToString();
        }

        public static MetaSaveDTO Read(string json)
        {
            var root = Json.ParseExactIntegers(json) as Dictionary<string, object> ?? throw new InvalidOperationException("meta root is not an object");
            int version = Int(root, "SaveVersion");
            if (version > 1 || version < 0) throw new InvalidOperationException("unsupported meta version " + version);
            if (version == 0) return MigrateVersionZero(root);
            var m = new MetaSaveDTO { SaveVersion = version, UnlockedClasses = Strings(Arr(root, "UnlockedClasses")), PermanentUnlocks = Strings(Arr(root, "PermanentUnlocks")), MetaSoul = Int(root, "MetaSoul"), MetaSigil = Int(root, "MetaSigil"), UnlockedCards = Strings(Arr(root, "UnlockedCards")), UnlockedRelics = Strings(Arr(root, "UnlockedRelics")), AchievementFlags = BoolMap(Obj(root, "AchievementFlags")), LastSettledRunId = NullableString(root, "LastSettledRunId"), TutorialDone = Bool(root, "TutorialDone"), Stats = ReadStats(Obj(root, "Stats")) };
            if (!root.TryGetValue("LastSettlement", out var receipt)) throw new InvalidOperationException("LastSettlement missing");
            if (receipt != null)
            {
                var r = receipt as Dictionary<string, object> ?? throw new InvalidOperationException("LastSettlement");
                m.LastSettlement = new SettlementReceiptDTO { RunId = Str(r, "RunId"), Outcome = (RunOutcome)Enum.Parse(typeof(RunOutcome), Str(r, "Outcome")), MetaSoul = Int(r, "MetaSoul"), MetaSigil = Int(r, "MetaSigil"), NewAchievements = Strings(Arr(r, "NewAchievements")) };
            }
            return m;
        }

        private static MetaSaveDTO MigrateVersionZero(Dictionary<string, object> root)
        {
            var m = new MetaSaveDTO();
            m.UnlockedClasses = OptionalStrings(root, "UnlockedClasses");
            m.PermanentUnlocks = OptionalStrings(root, "PermanentUnlocks");
            m.MetaSoul = OptionalInt(root, "MetaSoul");
            m.MetaSigil = OptionalInt(root, "MetaSigil");
            m.UnlockedCards = OptionalStrings(root, "UnlockedCards");
            m.UnlockedRelics = OptionalStrings(root, "UnlockedRelics");
            if (root.TryGetValue("AchievementFlags", out var flags) && flags != null) m.AchievementFlags = BoolMap(flags as Dictionary<string, object> ?? throw new InvalidOperationException("AchievementFlags"));
            if (root.TryGetValue("LastSettledRunId", out var id)) m.LastSettledRunId = id == null ? null : id as string ?? throw new InvalidOperationException("LastSettledRunId");
            m.TutorialDone = root.TryGetValue("TutorialDone", out var tutorial) && tutorial is bool done && done;
            if (root.TryGetValue("Stats", out var stats) && stats != null) m.Stats = ReadStats(stats as Dictionary<string, object> ?? throw new InvalidOperationException("Stats"));
            if (root.TryGetValue("LastSettlement", out var receipt) && receipt != null)
            {
                var r = receipt as Dictionary<string, object> ?? throw new InvalidOperationException("LastSettlement");
                m.LastSettlement = new SettlementReceiptDTO { RunId = Str(r, "RunId"), Outcome = (RunOutcome)Enum.Parse(typeof(RunOutcome), Str(r, "Outcome")), MetaSoul = OptionalInt(r, "MetaSoul"), MetaSigil = OptionalInt(r, "MetaSigil"), NewAchievements = OptionalStrings(r, "NewAchievements") };
            }
            return m;
        }

        private static MetaStatsDTO ReadStats(Dictionary<string, object> o)
        {
            int[] floors = Arr(o, "FloorClearCount").Select(Number).ToArray();
            if (floors.Length != 5) throw new InvalidOperationException("FloorClearCount must have five entries");
            return new MetaStatsDTO { FloorClearCount = floors, TotalSacrifice = Int(o, "TotalSacrifice"), TotalFusion = Int(o, "TotalFusion"), TotalExecuteKill = Int(o, "TotalExecuteKill"), TotalBurnDamage = Int(o, "TotalBurnDamage"), TotalPoisonDamage = Int(o, "TotalPoisonDamage"), ClassWins = IntMap(Obj(o, "ClassWins")), LastClass = NullableString(o, "LastClass") };
        }
        private static void Strings(StringBuilder b, IEnumerable<string> a) { b.Append('['); bool first = true; foreach (var s in a ?? Enumerable.Empty<string>()) { if (!first) b.Append(','); first = false; b.Append(Quote(s)); } b.Append(']'); }
        private static string Quote(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
        private static Dictionary<string, object> Obj(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? v as Dictionary<string, object> ?? throw new InvalidOperationException(k) : throw new InvalidOperationException(k);
        private static List<object> Arr(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? v as List<object> ?? throw new InvalidOperationException(k) : throw new InvalidOperationException(k);
        private static string Str(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v is string s ? s : throw new InvalidOperationException(k);
        private static string NullableString(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v == null ? null : Str(o, k);
        private static bool Bool(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v is bool b ? b : throw new InvalidOperationException(k);
        private static int Int(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? Number(v) : throw new InvalidOperationException(k);
        private static int Number(object v) => v is long l && l >= int.MinValue && l <= int.MaxValue ? (int)l : throw new InvalidOperationException("integer expected");
        private static int OptionalInt(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? Number(v) : 0;
        private static List<string> OptionalStrings(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && v != null ? Strings(v as List<object> ?? throw new InvalidOperationException(k)) : new List<string>();
        private static List<string> Strings(List<object> a) => a.Select(v => v as string ?? throw new InvalidOperationException("string expected")).ToList();
        private static Dictionary<string, bool> BoolMap(Dictionary<string, object> o) => o.ToDictionary(p => p.Key, p => p.Value is bool b ? b : throw new InvalidOperationException("boolean expected"));
        private static Dictionary<string, int> IntMap(Dictionary<string, object> o) => o.ToDictionary(p => p.Key, p => Number(p.Value));
    }
}
