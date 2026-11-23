using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLegion.Core
{
    public sealed class UnsupportedRunSaveVersionException : InvalidOperationException
    {
        public int Version { get; }
        public UnsupportedRunSaveVersionException(int version) : base("unsupported run save version " + version) { Version = version; }
    }

    /// <summary>저장 동작을 테스트에서 교체할 수 있게 하는 파일 시스템 표면.</summary>
    public interface ISaveFileSystem
    {
        void CreateDirectory(string path);
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Move(string source, string destination);
        void Replace(string source, string destination);
        void Delete(string path);
        string[] GetFiles(string path, string pattern);
    }

    public sealed class PhysicalSaveFileSystem : ISaveFileSystem
    {
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public bool Exists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);
        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents, new UTF8Encoding(false));
        public void Move(string source, string destination) => File.Move(source, destination);
        public void Replace(string source, string destination) => File.Replace(source, destination, null);
        public void Delete(string path) => File.Delete(path);
        public string[] GetFiles(string path, string pattern) => Directory.Exists(path) ? Directory.GetFiles(path, pattern) : Array.Empty<string>();
    }

    /// <summary>
    /// 단일 런 슬롯 및 메타 파일. 파일을 같은 디렉터리의 임시 파일에 완전히 쓴 뒤 Move/Replace 한 번으로 확정한다.
    /// Unity 경로는 호출자가 persistentDataPath/SaveData를 전달한다 (UI 연결은 별도 작업).
    /// </summary>
    public sealed class SaveFileStore
    {
        public const string RunFileName = "save_slot_0.json";
        public const string MetaFileName = "meta.json";
        private readonly string _directory;
        private readonly ISaveFileSystem _files;

        public SaveFileStore(string directory, ISaveFileSystem files = null)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("directory is required", nameof(directory));
            _directory = directory;
            _files = files ?? new PhysicalSaveFileSystem();
        }

        public bool SaveRun(SaveData save) => AtomicWrite(Path.Combine(_directory, RunFileName), SaveJson.Write(save));
        public SaveData LoadRun()
        {
            string path = Path.Combine(_directory, RunFileName);
            if (!_files.Exists(path)) return null;
            string json = _files.ReadAllText(path);
            int version = SaveJson.ReadVersion(json);
            if (version != 1)
            {
                _files.Delete(path);
                throw new UnsupportedRunSaveVersionException(version);
            }
            var save = SaveJson.Read(json);
            if (save.TutorialCompleted && !ReconcileTutorialCompletion(save)) throw new InvalidOperationException("tutorial meta reconciliation failed");
            return save;
        }
        public bool SaveMeta(MetaSaveDTO meta) => AtomicWrite(Path.Combine(_directory, MetaFileName), MetaSaveJson.Write(meta));
        public MetaSaveDTO LoadMeta() => _files.Exists(Path.Combine(_directory, MetaFileName))
            ? MetaSaveJson.Read(_files.ReadAllText(Path.Combine(_directory, MetaFileName))) : MetaSaveDTO.New();

        /// <summary>`14` §3: 복구한 런에서 안내 전투 완료가 확인되면 전역 플래그를 멱등 반영한다.</summary>
        public bool ReconcileTutorialCompletion(SaveData runSave)
        {
            if (runSave == null) return false;
            if (!runSave.TutorialCompleted) return true;
            MetaSaveDTO meta;
            try { meta = LoadMeta(); }
            catch (InvalidOperationException) { return false; }
            catch (FormatException) { return false; }
            if (meta.TutorialDone) return true;
            meta.TutorialDone = true;
            return SaveMeta(meta);
        }

        /// <summary>
        /// D-67: 확정 전 런을 별도 복원본에서 변경해 다음 상태를 만든 뒤 슬롯을 한 번 교체한다.
        /// 쓰기 실패 시 기존 RewardPending 슬롯을 유지하므로 재시작해도 지급이 중복되지 않는다.
        /// </summary>
        public bool ConfirmRewardAndPersist(SaveData pendingSave, int? cardIndex, int? relicIndex, SettlementPendingDTO settlement = null)
        {
            if (pendingSave == null || pendingSave.Phase != RunSavePhase.RewardPending || pendingSave.Reward?.Event != null) return false;
            RunState run;
            try { run = RunSave.Restore(pendingSave); }
            catch (InvalidOperationException) { return false; }
            var reward = RunSave.RestoreReward(pendingSave.Reward);
            if (!RewardFlow.Confirm(run, reward, cardIndex, relicIndex)) return false;
            RunFlow.CompleteNode(run);
            SaveData next;
            if (run.Outcome == RunOutcome.InProgress) next = RunSave.Capture(run, RunSavePhase.Map);
            else
            {
                if (settlement == null) return false;
                try { next = RunSave.CaptureSettlement(run, settlement); }
                catch (InvalidOperationException) { return false; }
            }
            return SaveRun(next);
        }

        public bool SaveEventResult(RunState run, EventPendingDTO result, SettlementPendingDTO settlement = null)
        {
            if (run == null || result == null) return false;
            if (run.Outcome != RunOutcome.InProgress)
            {
                if (settlement == null) return false;
                try { return SaveRun(RunSave.CaptureSettlement(run, settlement)); }
                catch (InvalidOperationException) { return false; }
            }
            try { return SaveRun(RunSave.CaptureEventPending(run, result)); }
            catch (InvalidOperationException) { return false; }
        }

        /// <summary>이벤트 [계속] 또는 강제 전투 진입을 이벤트 결과 저장에서 다음 단계로 원자 전환한다.</summary>
        public bool ContinueEventAndPersist(SaveData eventSave, SettlementPendingDTO settlement = null)
        {
            if (eventSave == null || eventSave.Phase != RunSavePhase.RewardPending || eventSave.Reward?.Event == null || eventSave.Reward.Event.Phase != EventScreenPhase.Result) return false;
            RunState run;
            try { run = RunSave.Restore(eventSave); }
            catch (InvalidOperationException) { return false; }
            var eventResult = eventSave.Reward.Event;
            SaveData next;
            if (eventResult.Result.ForcedBattle)
            {
                next = RunSave.CaptureForcedBattleEntry(run, eventResult);
            }
            else
            {
                RunFlow.CompleteNode(run);
                if (run.Outcome == RunOutcome.InProgress) next = RunSave.Capture(run, RunSavePhase.Map);
                else
                {
                    if (settlement == null) return false;
                    try { next = RunSave.CaptureSettlement(run, settlement); }
                    catch (InvalidOperationException) { return false; }
                }
            }
            return SaveRun(next);
        }

        public bool SaveRestResult(RunState run, RestScreenResult result)
        {
            try { return SaveRun(RunSave.CaptureRest(run, result)); }
            catch (InvalidOperationException) { return false; }
        }

        /// <summary>D-84: 결과 저장 뒤 한 번만 노드를 넘기고 다음 위치를 교체 저장한다.</summary>
        public bool ContinueRestAndPersist(SaveData restSave, SettlementPendingDTO settlement = null)
        {
            if (restSave == null || restSave.Phase != RunSavePhase.RestPending) return false;
            RunState run;
            try { run = RunSave.Restore(restSave); }
            catch (InvalidOperationException) { return false; }
            RunFlow.CompleteNode(run);
            SaveData next;
            if (run.Outcome == RunOutcome.InProgress) next = RunSave.Capture(run, RunSavePhase.Map);
            else
            {
                if (settlement == null) return false;
                try { next = RunSave.CaptureSettlement(run, settlement); }
                catch (InvalidOperationException) { return false; }
            }
            return SaveRun(next);
        }

        /// <summary>D-67: 지급과 LastSettledRunId를 같은 meta.json 교체로 확정한다.</summary>
        public bool ApplySettlement(SettlementPendingDTO settlement)
        {
            if (settlement == null || string.IsNullOrWhiteSpace(settlement.RunId) || settlement.Outcome == RunOutcome.InProgress) return false;
            MetaSaveDTO meta;
            try { meta = LoadMeta(); }
            catch (InvalidOperationException) { return false; }
            catch (FormatException) { return false; }
            if (meta.LastSettledRunId == settlement.RunId) return true;
            try
            {
                checked { meta.MetaSoul += settlement.MetaSoul; meta.MetaSigil += settlement.MetaSigil; }
                var current = meta.Stats ?? (meta.Stats = new MetaStatsDTO());
                var delta = settlement.Stats ?? new MetaStatsDTO();
                for (int i = 0; i < 5; i++) current.FloorClearCount[i] = checked(current.FloorClearCount[i] + delta.FloorClearCount[i]);
                current.TotalSacrifice = checked(current.TotalSacrifice + delta.TotalSacrifice);
                current.TotalFusion = checked(current.TotalFusion + delta.TotalFusion);
                current.TotalExecuteKill = checked(current.TotalExecuteKill + delta.TotalExecuteKill);
                current.TotalBurnDamage = checked(current.TotalBurnDamage + delta.TotalBurnDamage);
                current.TotalPoisonDamage = checked(current.TotalPoisonDamage + delta.TotalPoisonDamage);
                foreach (var pair in delta.ClassWins) current.ClassWins[pair.Key] = checked((current.ClassWins.TryGetValue(pair.Key, out int count) ? count : 0) + pair.Value);
                if (delta.LastClass != null) current.LastClass = delta.LastClass;
                foreach (string id in settlement.NewAchievements ?? Enumerable.Empty<string>()) meta.AchievementFlags[id] = true;
                meta.LastSettledRunId = settlement.RunId;
                meta.LastSettlement = new SettlementReceiptDTO { RunId = settlement.RunId, Outcome = settlement.Outcome, MetaSoul = settlement.MetaSoul, MetaSigil = settlement.MetaSigil, NewAchievements = (settlement.NewAchievements ?? new System.Collections.Generic.List<string>()).ToList() };
                return SaveMeta(meta);
            }
            catch (OverflowException) { return false; }
        }

        /// <summary>앱 시작 시 미확정 임시 파일만 정리한다. 확정 슬롯은 건드리지 않는다.</summary>
        public void CleanupTemporaryFiles()
        {
            foreach (string path in _files.GetFiles(_directory, "*.tmp")) _files.Delete(path);
        }

        private bool AtomicWrite(string path, string contents)
        {
            string temp = path + ".tmp";
            try
            {
                _files.CreateDirectory(_directory);
                _files.WriteAllText(temp, contents);
                if (_files.Exists(path)) _files.Replace(temp, path);
                else _files.Move(temp, path);
                return true;
            }
            catch (IOException) { TryDelete(temp); return false; }
            catch (UnauthorizedAccessException) { TryDelete(temp); return false; }
            catch (NotSupportedException) { TryDelete(temp); return false; }
        }

        private void TryDelete(string path)
        {
            try { if (_files.Exists(path)) _files.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
