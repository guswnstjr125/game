using System;
using System.Linq;
using DarkLegion.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DarkLegion.UI
{
    /// <summary>
    /// 18노드 맵과 전투 결과 화면 (`37` §1·§2). 규칙은 <see cref="RunMapSession"/>만 부르고,
    /// 전투는 <see cref="BattleScreenView"/>가 같은 런 상태로 진행한다. 이벤트·휴식은 기존 화면을 같은 런 세션에 연결한다.
    /// </summary>
    public sealed class RunMapView : MonoBehaviour
    {
        const float NodeWidth = 190f;
        const float NodeSpacing = 200f;

        RunMapSession _map;
        GameData _data;
        Font _font;
        Sprite _white;
        GameObject _canvasGo;
        GameObject _eventsGo;
        GameObject _battleGo;
        GameObject _nodeGo;
        RectTransform _mapRoot;
        RectTransform _nodesRoot;
        RectTransform _resultRoot;
        Text _top;
        Text _hint;
        Text _resultText;
        Button _confirm;
        Text _confirmLabel;
        Button _restart;
        RectTransform _choicesRoot;
        BattleResultView _result;
        int? _pickCard;
        int? _pickRelic;
        readonly System.Collections.Generic.Dictionary<string, string> _names = new System.Collections.Generic.Dictionary<string, string>();

        GameDataLoadResult _loaded;
        SaveFileStore _store;
        SaveData _checkpoint;
        RectTransform _title;
        Text _status;
        Button _continue;
        RectTransform _replaceDialog;
        Button _replaceConfirm;
        SaveData _replaceExpected;
        bool _startingNew;
        Action _retry;
        public string SaveDirectory { get; set; }

        RectTransform _pauseRoot;
        RectTransform _settingsRoot;
        RectTransform _creditsRoot;
        RectTransform _forfeitRoot;
        Text _displayOptions;
        Button _pauseButton;
        int _pauseFrame;
        bool _settingsFromPause;
        int _resolutionIndex = 1;
        int _modeIndex;
        static readonly int[] ResolutionWidths = { 1280, 1920, 2560, 3840 };
        static readonly string[] DisplayModes = { "전체 화면", "창", "테두리 없는 창" };

        bool InputBlocked() => _pauseRoot.gameObject.activeSelf || _settingsRoot.gameObject.activeSelf ||
            _forfeitRoot.gameObject.activeSelf || _errorRoot.gameObject.activeSelf || _pauseFrame == Time.frameCount;

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true || _pauseFrame == Time.frameCount ||
                _errorRoot == null || _errorRoot.gameObject.activeSelf) return;
            if (_settingsRoot.gameObject.activeSelf) CloseSettings();
            else if (_creditsRoot.gameObject.activeSelf) CloseCredits();
            else if (_forfeitRoot.gameObject.activeSelf) CancelForfeit();
            else if (_pauseRoot.gameObject.activeSelf) ResumePlay();
            else if (_battleGo == null && _mapRoot.gameObject.activeSelf) OpenPause();
        }

        void OpenPause()
        {
            if (_map == null || _map.Run.Outcome != RunOutcome.InProgress || InputBlocked()) return;
            if (_battleGo == null && !_mapRoot.gameObject.activeSelf) return;
            if (_battleGo != null && _checkpoint.Phase != RunSavePhase.BattleEntry) return;
            _pauseFrame = Time.frameCount;
            _pauseRoot.gameObject.SetActive(true);
        }

        void ResumePlay()
        {
            _pauseFrame = Time.frameCount;
            _pauseRoot.gameObject.SetActive(false);
        }

        void OpenSettings(bool fromPause)
        {
            _settingsFromPause = fromPause;
            _pauseRoot.gameObject.SetActive(false);
            _title.gameObject.SetActive(false);
            _settingsRoot.gameObject.SetActive(true);
            UpdateDisplayOptions();
        }

        void CloseSettings()
        {
            _pauseFrame = Time.frameCount;
            _settingsRoot.gameObject.SetActive(false);
            (_settingsFromPause ? _pauseRoot : _title).gameObject.SetActive(true);
        }

        void UpdateDisplayOptions()
        {
            int width = ResolutionWidths[_resolutionIndex];
            _displayOptions.text = DisplayModes[_modeIndex] + " / " + width + "×" + (width * 9 / 16) +
                " / 수직 동기화 " + (QualitySettings.vSyncCount > 0 ? "켜짐" : "꺼짐");
        }

        void ApplyDisplayOptions()
        {
            var mode = _modeIndex == 0 ? FullScreenMode.ExclusiveFullScreen :
                _modeIndex == 1 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            int width = ResolutionWidths[_resolutionIndex];
            Screen.SetResolution(width, width * 9 / 16, mode);
            UpdateDisplayOptions();
        }

        void CloseCredits()
        {
            _creditsRoot.gameObject.SetActive(false);
            _title.gameObject.SetActive(true);
        }

        void OpenForfeit()
        {
            _pauseRoot.gameObject.SetActive(false);
            _forfeitRoot.gameObject.SetActive(true);
        }

        void CancelForfeit()
        {
            _forfeitRoot.gameObject.SetActive(false);
            _pauseRoot.gameObject.SetActive(true);
        }

        void ConfirmForfeit()
        {
            if (!_forfeitRoot.gameObject.activeSelf || _errorRoot.gameObject.activeSelf) return;
            Attempt(() =>
            {
                var run = RunSave.Restore(_checkpoint);
                RunFlow.Forfeit(run);
                WriteCheckpoint(RunSave.CaptureSettlement(run, Settlement(run)));
                _forfeitRoot.gameObject.SetActive(false);
                CloseBattle();
                Attempt(() => Resume(_checkpoint));
            });
        }

        void ReturnToTitle()
        {
            if (!_pauseRoot.gameObject.activeSelf || _errorRoot.gameObject.activeSelf) return;
            Attempt(() =>
            {
                // 전투 도중에는 이미 확정된 진입 저장을 그대로 사용한다 (D-37).
                if (_battleGo == null) WriteCheckpoint(RunSave.Capture(_map.Run, RunSavePhase.Map));
                ResumePlay();
                CloseBattle();
                ShowTitle();
            });
        }

        void CloseBattle()
        {
            if (_battleGo == null) return;
            _battleGo.SetActive(false);
            Destroy(_battleGo);
            _battleGo = null;
        }

        void ShowTitle()
        {
            _title.gameObject.SetActive(true);
            _pauseButton.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            _mapRoot.gameObject.SetActive(false);
            _status.text = "";
            _continue.interactable = false;
            _checkpoint = null;
            try
            {
                _checkpoint = _store.LoadRun();
                if (_checkpoint != null) RunSave.Restore(_checkpoint);
                _continue.interactable = _checkpoint != null;
            }
            catch (Exception ex) { _status.text = "저장을 읽을 수 없습니다. " + ex.Message; }
        }

        void StartNew()
        {
            if (!_title.gameObject.activeSelf || _startingNew) return;
            if (!_store.HasRunSlot)
            {
                BeginNewRun(null);
                return;
            }

            if (!_continue.interactable || _checkpoint.Phase == RunSavePhase.SettlementPending)
            {
                _status.text = "기존 저장을 확인해 주세요. 정산 대기 중이면 이어하기로 결과를 마쳐 주세요.";
                return;
            }

            _replaceExpected = _checkpoint;
            _replaceConfirm.interactable = true;
            _title.gameObject.SetActive(false);
            _replaceDialog.gameObject.SetActive(true);
        }

        void CancelReplacement()
        {
            if (_startingNew || !_replaceDialog.gameObject.activeSelf) return;
            _replaceExpected = null;
            _replaceDialog.gameObject.SetActive(false);
            _title.gameObject.SetActive(true);
        }

        void ConfirmReplacement()
        {
            if (_startingNew || !_replaceDialog.gameObject.activeSelf || _replaceExpected == null) return;
            _replaceConfirm.interactable = false;
            BeginNewRun(_replaceExpected);
        }

        void BeginNewRun(SaveData expected)
        {
            _startingNew = true;
            SaveData next = null;
            Attempt(() =>
            {
                if (next == null)
                {
                    var run = RunMapSession.NewNecRun(_loaded, DateTime.UtcNow.Ticks, !_store.LoadMeta().TutorialDone).Run;
                    run.RunId = Guid.NewGuid().ToString("N");
                    next = RunSave.Capture(run, RunSavePhase.Map);
                }

                bool saved = expected == null
                    ? !_store.HasRunSlot && _store.SaveRun(next)
                    : _store.ReplaceActiveRun(expected, next);
                if (!saved)
                    throw new InvalidOperationException("새 게임을 저장하지 못했습니다. 기존 저장을 유지했습니다.");

                _checkpoint = next;
                _replaceExpected = null;
                _replaceDialog.gameObject.SetActive(false);
                _startingNew = false;
                Attempt(() => Resume(next));
            });
        }

        void Resume(SaveData save)
        {
            if (save == null) throw new InvalidOperationException("이어할 저장이 없습니다.");
            _map = new RunMapSession(RunSave.Restore(save), _loaded);
            _title.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            switch (save.Phase)
            {
                case RunSavePhase.Map: RefreshMap(); break;
                case RunSavePhase.BattleEntry:
                    ShowBattle(save.BattleEntryEvent == null ? _map.EnterBattle() : _map.ResumeForcedBattle());
                    break;
                case RunSavePhase.RewardPending:
                    if (save.Reward.Event != null) OpenEvent(save.Reward.Event);
                    else
                    {
                        var r = save.Reward;
                        ShowResult(new BattleResultView
                        {
                            Battle = BattleOutcome.Victory, Run = _map.Run.Outcome,
                            NodeId = r.NodeId, Kind = r.Source == RewardSource.Boss ? NodeKind.Boss : r.Source == RewardSource.Elite ? NodeKind.Elite : NodeKind.Battle,
                            HpBefore = _map.Run.CurrentHp, HpAfter = _map.Run.CurrentHp, MaxHp = _map.Run.MaxHp,
                            RewardGold = r.Gold, CardChoices = r.CardChoices.ToArray(), RelicChoices = r.RelicChoices.ToArray(),
                            AutoRelic = r.AutoRelic, CanConfirm = true
                        });
                        _pickCard = r.SelectedCard < 0 ? (int?)null : r.SelectedCard;
                        _pickRelic = r.SelectedRelic < 0 ? (int?)null : r.SelectedRelic;
                        RenderChoices();
                    }
                    break;
                case RunSavePhase.RestPending: OpenRest(save.Rest); break;
                case RunSavePhase.SettlementPending:
                    if (!_store.ApplySettlement(save.Settlement)) throw new InvalidOperationException("정산을 저장할 수 없습니다.");
                    ShowRunEnd();
                    _resultText.text += "\n영혼 +" + save.Settlement.MetaSoul + "  인장 +" + save.Settlement.MetaSigil;
                    break;
            }
        }

        void WriteCheckpoint(SaveData save)
        {
            if (!_store.SaveRun(save)) throw new InvalidOperationException("기존 저장을 유지했습니다.");
            _checkpoint = save;
        }

        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                _retry = action;
                _errorText.text = "진행 상태를 저장하거나 불러올 수 없습니다.\n" + ex.Message;
                _errorRoot.gameObject.SetActive(true);
            }
        }

        RectTransform _errorRoot;
        Text _errorText;

        void Start()
        {
            _font = BattleScreenView.LoadFont();
            _white = BattleScreenView.White();
            if (_canvasGo == null) Build();
            try
            {
                _data = BattleScreenView.LoadData();
                foreach (var row in _data.Rows("Cards_Master")) _names[row.Str("ID")] = row.Str("카드명");
                foreach (var row in _data.Rows("Cards_Text")) _names[row.Str("Card_ID")] = row.Str("Name_KO");
                foreach (var row in _data.Rows("Curse_Cards")) _names[row.Str("Curse_ID")] = row.Str("Name_KO");
                foreach (var row in _data.Rows("Relics_MVP")) _names[row.Str("Relic_ID")] = row.Str("Name_KO");
                _loaded = GameDataLoader.Load(_data);
                string directory = SaveDirectory ?? System.IO.Path.Combine(Application.persistentDataPath, "SaveData");
                _store = new SaveFileStore(directory);
                _store.CleanupTemporaryFiles();
                ShowTitle();
            }
            catch (Exception ex)
            {
                _retry = Start;
                _errorText.text = "초기화할 수 없습니다. " + ex.Message;
                _errorRoot.gameObject.SetActive(true);
            }
        }

        void OnDestroy()
        {
            if (_battleGo != null) Destroy(_battleGo);
            if (_nodeGo != null) Destroy(_nodeGo);
            if (_canvasGo != null) Destroy(_canvasGo);
            if (_eventsGo != null) Destroy(_eventsGo);
        }

        void RefreshMap()
        {
            _pauseButton.gameObject.SetActive(true);
            _resultRoot.gameObject.SetActive(false);
            _mapRoot.gameObject.SetActive(true);
            var snap = _map.Capture();
            _top.text = "층 " + snap.Floor + " · 노드 " + snap.Node + "    HP " + snap.Hp + "/" + snap.MaxHp
                + "    골드 " + snap.Gold + "    융합 충전 " + snap.FusionCharge + "/3    덱 " + snap.DeckCount + "장"
                + "    유물 " + (snap.Relics.Length == 0 ? "없음" : string.Join(", ", snap.Relics));

            for (int i = _nodesRoot.childCount - 1; i >= 0; i--) Destroy(_nodesRoot.GetChild(i).gameObject);
            foreach (var node in snap.Nodes)
            {
                float x = (node.Node - 5) * NodeSpacing;
                float y = node.Floor == 1 ? 170f : -90f;
                var box = Box(_nodesRoot, NodeColor(node), NodeWidth, 130f);
                Place(box, x, y);
                string state = node.State == MapNodeState.Visited ? "완료" : node.State == MapNodeState.Current ? "현재" : "";
                Label(box, node.Id + "\n" + node.KindLabel + "\n" + state, 22);
                var button = box.gameObject.AddComponent<Button>();
                button.targetGraphic = box.GetComponent<Image>();
                var captured = node;
                button.onClick.AddListener(() => ClickNode(captured));
            }

            if (snap.Outcome == RunOutcome.Victory) _hint.text = "런 승리";
            else if (snap.Outcome == RunOutcome.Defeat) _hint.text = "런 패배";
            else
            {
                var current = snap.Nodes.First(n => n.State == MapNodeState.Current);
                _hint.text = current.Enterable
                    ? current.Id + " " + current.KindLabel + ": 노드를 눌러 " + current.KindLabel + (current.Kind == NodeKind.Event || current.Kind == NodeKind.Rest ? " 화면을 엽니다." : "에 들어갑니다.")
                    : current.Id + " " + current.KindLabel + ": 이 노드의 화면은 아직 없습니다.";
            }
        }

        void ClickNode(MapNodeView node)
        {
            if (InputBlocked() || !node.Enterable || !_map.CanEnter(node.Floor, node.Node)) return;
            Attempt(() =>
            {
                switch (node.Kind)
                {
                    case NodeKind.Event:
                        string id = EventFlow.Enter(_map.Run, _loaded);
                        WriteCheckpoint(RunSave.CaptureEventPending(_map.Run, new EventPendingDTO
                        {
                            NodeId = node.Id, EventId = id, Phase = EventScreenPhase.Choosing
                        }));
                        OpenEvent(_checkpoint.Reward.Event);
                        break;
                    case NodeKind.Rest:
                        OpenRest(null);
                        break;
                    default:
                        WriteCheckpoint(RunSave.Capture(_map.Run, RunSavePhase.BattleEntry));
                        ShowBattle(_map.EnterBattle());
                        break;
                }
            });
        }

        void OpenEvent(EventPendingDTO pending)
        {
            _pauseButton.gameObject.SetActive(false);
            _mapRoot.gameObject.SetActive(false);
            var session = EventScreenSession.RestorePending(_map.Run, _loaded, pending, nameOf: NameOf);
            _nodeGo = new GameObject("EventScreen");
            var view = _nodeGo.AddComponent<EventScreenView>();
            view.Attach(session, NameOf);
            view.HandleInput = (name, action) =>
            {
                if (_nodeGo != view.gameObject || _errorRoot.gameObject.activeSelf) return;
                if (name == "Continue")
                {
                    Attempt(() =>
                    {
                        if (!_store.ContinueEventAndPersist(_checkpoint))
                            throw new InvalidOperationException("이벤트 노드 완료를 저장하지 못했습니다.");
                        Attempt(LoadAfterNode);
                    });
                    return;
                }

                action();
                Attempt(() =>
                {
                    var run = _map.Run;
                    if (run.Outcome == RunOutcome.InProgress)
                        WriteCheckpoint(RunSave.CaptureEventPending(run, session.CapturePending()));
                    else
                    {
                        WriteCheckpoint(RunSave.CaptureSettlement(run, Settlement(run)));
                        CloseNodeScreen();
                        Attempt(() => Resume(_checkpoint));
                    }
                });
            };
        }

        void OpenRest(RestPendingDTO pending)
        {
            _pauseButton.gameObject.SetActive(false);
            _mapRoot.gameObject.SetActive(false);
            var session = pending == null ? new RestScreenSession(_map.Run, _loaded, nameOf: NameOf)
                : RestScreenSession.RestorePending(_map.Run, _loaded, pending, nameOf: NameOf);
            _nodeGo = new GameObject("RestScreen");
            var view = _nodeGo.AddComponent<RestScreenView>();
            view.Attach(session, NameOf);
            view.HandleInput = (name, action) =>
            {
                if (_nodeGo != view.gameObject || _errorRoot.gameObject.activeSelf) return;
                if (name == "Continue")
                {
                    if (session.Result == null) action();
                    if (session.Result == null) return;
                    Attempt(() =>
                    {
                        if (_checkpoint.Phase != RunSavePhase.RestPending)
                            WriteCheckpoint(RunSave.CaptureRest(_map.Run, session.Result));
                        if (!_store.ContinueRestAndPersist(_checkpoint))
                            throw new InvalidOperationException("휴식 노드 완료를 저장하지 못했습니다.");
                        Attempt(LoadAfterNode);
                    });
                    return;
                }

                action();
                if (session.Result != null)
                    Attempt(() => WriteCheckpoint(RunSave.CaptureRest(_map.Run, session.Result)));
            };
        }

        void LoadAfterNode()
        {
            _checkpoint = _store.LoadRun();
            CloseNodeScreen();
            Resume(_checkpoint);
        }

        void ShowBattle(BattleScreenSession session)
        {
            _pauseButton.gameObject.SetActive(true);
            _mapRoot.gameObject.SetActive(false);
            _battleGo = new GameObject("BattleScreen");
            var view = _battleGo.AddComponent<BattleScreenView>();
            view.Attach(session, _data, OnBattleDone);
            view.OnBattleFinished = () => Attempt(SaveBattleOutcome);
            view.OpenPause = OpenPause;
            view.InputBlocked = InputBlocked;
        }

        void CloseNodeScreen()
        {
            if (_nodeGo != null) _nodeGo.SetActive(false);
            Destroy(_nodeGo);
            _nodeGo = null;
        }

        void SaveBattleOutcome()
        {
            if (_checkpoint.Phase == RunSavePhase.BattleEntry)
            {
                var result = _map.Result();
                var save = result.Battle == BattleOutcome.Victory
                    ? _map.CaptureBattleReward()
                    : RunSave.CaptureSettlement(_map.Run, Settlement(_map.Run));
                WriteCheckpoint(save);
                _pauseButton.gameObject.SetActive(false);
            }

            if (!_store.ReconcileTutorialCompletion(_checkpoint))
                throw new InvalidOperationException("안내 전투 완료를 저장하지 못했습니다.");
        }

        void OnBattleDone()
        {
            if (_battleGo == null) return;
            Attempt(() =>
            {
                SaveBattleOutcome();
                var result = _map.Result();
                CloseBattle();
                if (result.Battle == BattleOutcome.Victory) ShowResult(result);
                else Attempt(() => Resume(_checkpoint));
            });
        }

        SettlementPendingDTO Settlement(RunState run)
        {
            var settlement = new SettlementPendingDTO
            {
                RunId = run.RunId, Outcome = run.Outcome,
                ReachedFloor = run.Floor, ReachedStageIndex = run.StageIndex
            };
            for (int floor = 1; floor <= run.Floor; floor++)
            {
                if (!run.VisitedNodeIds.Contains(RunState.NodeId(floor, 9))) continue;
                var row = _data.Rows("Meta_Currency").Single(r => r.Str("Condition") == "ClearFloor" + floor);
                settlement.ClearedFloors.Add(floor);
                settlement.MetaSoul += row.Int("META_SOUL");
                settlement.MetaSigil += row.Int("META_SIGIL");
            }
            return settlement;
        }

        void ShowResult(BattleResultView result)
        {
            _pauseButton.gameObject.SetActive(false);
            _mapRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(true);
            _result = result;
            _pickCard = null;
            _pickRelic = null;
            string kind = RunMapSession.KindLabel(result.Kind);
            string head = result.NodeId + " " + kind + "\nHP " + result.HpBefore + " → " + result.HpAfter + " / " + result.MaxHp;
            if (result.Battle == BattleOutcome.Victory)
            {
                _resultText.text = "승리\n" + head + "\n보상 골드 +" + result.RewardGold
                    + (result.AutoRelic != null ? "\n보스 유물 " + NameOf(result.AutoRelic) + " 획득" : "")
                    + (result.NeedsRelicChoice ? "\n유물 1개를 고르세요. 카드는 고르거나 건너뛸 수 있습니다." : "\n카드 1장을 고르거나 건너뛰세요.");
            }
            else
            {
                _resultText.text = "패배 — 런 종료\n" + head;
            }
            RenderChoices();
            // 다시 시작은 패배 때만. 확정 버튼은 승리하고 아직 확정하지 않았을 때만 보인다.
            _restart.gameObject.SetActive(result.Battle == BattleOutcome.Defeat);
        }

        /// <summary>카드·유물 후보 버튼과 확정 버튼을 현재 선택에 맞게 다시 그린다.</summary>
        void RenderChoices()
        {
            for (int i = _choicesRoot.childCount - 1; i >= 0; i--) Destroy(_choicesRoot.GetChild(i).gameObject);
            bool confirmable = _result != null && _result.CanConfirm;
            _confirm.gameObject.SetActive(confirmable);
            if (!confirmable) return;

            for (int i = 0; i < _result.CardChoices.Length; i++)
            {
                int index = i;
                bool picked = _pickCard == i;
                var b = ChoiceButton(NameOf(_result.CardChoices[i]) + "\n" + _result.CardChoices[i], new Vector2(-340f + i * 340f, -20f), picked);
                b.onClick.AddListener(() => { SaveChoice(_pickCard == index ? (int?)null : index, _pickRelic); });
            }
            for (int i = 0; i < _result.RelicChoices.Length; i++)
            {
                int index = i;
                bool picked = _pickRelic == i;
                var b = ChoiceButton("유물 " + NameOf(_result.RelicChoices[i]) + "\n" + _result.RelicChoices[i], new Vector2(-340f + i * 340f, -140f), picked);
                b.onClick.AddListener(() => { SaveChoice(_pickCard, index); });
            }

            bool needRelic = _result.NeedsRelicChoice && _pickRelic == null;
            _confirm.interactable = !needRelic;
            _confirmLabel.text = needRelic ? "유물을 고르세요"
                : _pickCard == null ? "카드 건너뛰고 확정" : "확정: " + NameOf(_result.CardChoices[_pickCard.Value]);
        }

        Button ChoiceButton(string text, Vector2 pos, bool picked)
        {
            var box = Box(_choicesRoot, picked ? new Color(0.6f, 0.45f, 0.12f, 1f) : new Color(0.22f, 0.24f, 0.3f, 1f), 320f, 100f);
            Place(box, pos.x, pos.y);
            Label(box, text, 22);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            return button;
        }

        void SaveChoice(int? card, int? relic)
        {
            Attempt(() =>
            {
                var run = RunSave.Restore(_checkpoint);
                var reward = RunSave.RestoreReward(_checkpoint.Reward);
                WriteCheckpoint(RunSave.CaptureRewardPending(run, reward, card ?? -1, relic ?? -1));
                _pickCard = card;
                _pickRelic = relic;
                RenderChoices();
            });
        }

        void OnConfirm()
        {
            if (_result == null || !_result.CanConfirm || !_confirm.interactable) return;
            Attempt(() =>
            {
                var run = RunSave.Restore(_checkpoint);
                RunFlow.CompleteNode(run);
                var settlement = run.Outcome == RunOutcome.Victory ? Settlement(run) : null;
                if (!_store.ConfirmRewardAndPersist(_checkpoint, _pickCard, _pickRelic, settlement))
                    throw new InvalidOperationException("보상 확정을 저장하지 못했습니다.");
                Attempt(() => Resume(_checkpoint = _store.LoadRun()));
            });
        }

        void ShowRunEnd()
        {
            _pauseButton.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(true);
            _mapRoot.gameObject.SetActive(false);
            var snap = _map.Capture();
            _resultText.text = (snap.Outcome == RunOutcome.Forfeit ? "런 포기" : snap.Outcome == RunOutcome.Victory ? "런 승리\n2층 보스를 처치했습니다." : "패배 — 런 종료")
                + "\nHP " + snap.Hp + "/" + snap.MaxHp + "  골드 " + snap.Gold;
            _result = null;
            RenderChoices();
            _restart.gameObject.SetActive(true);
        }

        string NameOf(string id)
        {
            string name;
            return _names.TryGetValue(id, out name) && name.Length > 0 ? name : id;
        }

        void OnRestart()
        {
            Attempt(() =>
            {
                _store.DeleteSettledRun();
                ShowTitle();
            });
        }

        static Color NodeColor(MapNodeView node)
        {
            if (node.State == MapNodeState.Visited) return new Color(0.2f, 0.28f, 0.22f, 1f);
            if (node.State == MapNodeState.Current) return node.Enterable ? new Color(0.6f, 0.45f, 0.12f, 1f) : new Color(0.45f, 0.2f, 0.2f, 1f);
            return new Color(0.16f, 0.17f, 0.22f, 1f);
        }

        void Build()
        {
            _canvasGo = new GameObject("MapCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasGo.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                _eventsGo = new GameObject("EventSystem");
                _eventsGo.AddComponent<EventSystem>();
                _eventsGo.AddComponent<InputSystemUIInputModule>();
            }

            _mapRoot = Stretch(_canvasGo.transform, "MapRoot");
            var topBar = Box(_mapRoot, new Color(0.08f, 0.08f, 0.1f, 1f), 1900f, 56f);
            Place(topBar, 0f, 490f);
            _top = Label(topBar, "", 22);
            _nodesRoot = Stretch(_mapRoot, "Nodes");
            var hintBar = Box(_mapRoot, new Color(0.1f, 0.1f, 0.12f, 1f), 1900f, 56f);
            Place(hintBar, 0f, -400f);
            _hint = Label(hintBar, "", 24);

            _resultRoot = Stretch(_canvasGo.transform, "ResultRoot");
            var panel = Box(_resultRoot, new Color(0f, 0f, 0f, 0.9f), 1100f, 720f);
            Place(panel, 0f, 0f);
            _resultText = Label(panel, "", 30);
            _resultText.rectTransform.offsetMin = new Vector2(20f, 380f);
            _choicesRoot = Stretch(panel, "Choices");
            _confirm = ActionButton(panel, "확정", new Vector2(0f, -290f), OnConfirm);
            _confirm.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 64f);
            _confirmLabel = _confirm.GetComponentInChildren<Text>();
            _restart = ActionButton(panel, "타이틀로", new Vector2(0f, -290f), OnRestart);
            _resultRoot.gameObject.SetActive(false);

            var pauseControl = Stretch(_canvasGo.transform, "PauseControl");
            var pauseCanvas = pauseControl.gameObject.AddComponent<Canvas>();
            pauseCanvas.overrideSorting = true;
            pauseCanvas.sortingOrder = 60;
            pauseControl.gameObject.AddComponent<GraphicRaycaster>();
            _pauseButton = ActionButton(pauseControl, "일시정지", new Vector2(820f, 415f), OpenPause);
            _pauseButton.name = "PauseGame";
            _title = Stretch(_canvasGo.transform, "Title");
            Label(_title, "다크 레기온", 44).rectTransform.anchoredPosition = new Vector2(0f, 230f);
            ActionButton(_title, "새 게임", new Vector2(0f, 90f), StartNew).name = "NewGame";
            _continue = ActionButton(_title, "이어하기", new Vector2(0f, 0f), () => Attempt(() => Resume(_checkpoint = _store.LoadRun())));
            _continue.name = "ContinueGame";
            ActionButton(_title, "설정", new Vector2(0f, -85f), () => OpenSettings(false));
            ActionButton(_title, "크레딧", new Vector2(0f, -170f), () =>
            {
                _title.gameObject.SetActive(false);
                _creditsRoot.gameObject.SetActive(true);
            });
            ActionButton(_title, "종료", new Vector2(0f, -255f), () => Application.Quit());
            _status = Label(_title, "", 24);
            _status.rectTransform.anchoredPosition = new Vector2(0f, -350f);
            _status.raycastTarget = false;

            _replaceDialog = Stretch(_canvasGo.transform, "ReplaceRunDialog");
            var replacePanel = Box(_replaceDialog, new Color(0.08f, 0.08f, 0.1f, 1f), 1000f, 420f);
            Place(replacePanel, 0f, 0f);
            var question = Label(replacePanel, "기존 런 포기\n기존 진행을 포기하고 새 게임을 시작할까요?\n포기한 런의 정산 보상은 지급되지 않습니다.", 30);
            question.rectTransform.anchoredPosition = new Vector2(0f, 75f);
            question.raycastTarget = false;
            _replaceConfirm = ActionButton(replacePanel, "포기하고 시작", new Vector2(-170f, -100f), ConfirmReplacement);
            _replaceConfirm.name = "ConfirmReplaceRun";
            ActionButton(replacePanel, "취소", new Vector2(170f, -100f), CancelReplacement).name = "CancelReplaceRun";
            _replaceDialog.gameObject.SetActive(false);

            BuildPauseMenus();

            var overlay = new GameObject("SaveErrorCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            overlay.transform.SetParent(_canvasGo.transform, false);
            var errorCanvas = overlay.GetComponent<Canvas>();
            errorCanvas.overrideSorting = true;
            errorCanvas.sortingOrder = 100;
            _errorRoot = Stretch(overlay.transform, "SaveError");
            var backdrop = Box(_errorRoot, new Color(0f, 0f, 0f, 0.95f), 1920f, 1080f);
            _errorText = Label(backdrop, "", 30);
            _errorText.raycastTarget = false;
            ActionButton(backdrop, "다시 시도", new Vector2(0f, -180f), () =>
            {
                _errorRoot.gameObject.SetActive(false);
                if (_retry != null) Attempt(_retry);
            });
            _errorRoot.gameObject.SetActive(false);
        }

        void BuildPauseMenus()
        {
            var pause = BuildMenu("Pause", 70, "일시정지", out _pauseRoot);
            ActionButton(pause, "계속", new Vector2(0f, 90f), ResumePlay).name = "ResumeGame";
            ActionButton(pause, "설정", new Vector2(0f, 0f), () => OpenSettings(true));
            ActionButton(pause, "런 포기", new Vector2(0f, -90f), OpenForfeit).name = "OpenForfeit";
            ActionButton(pause, "타이틀로", new Vector2(0f, -180f), ReturnToTitle).name = "PauseToTitle";

            var settings = BuildMenu("Settings", 71, "화면 설정", out _settingsRoot);
            _displayOptions = Label(settings, "", 24);
            _displayOptions.rectTransform.anchoredPosition = new Vector2(0f, 125f);
            ActionButton(settings, "화면 모드", new Vector2(-215f, 0f), () =>
            {
                _modeIndex = (_modeIndex + 1) % DisplayModes.Length;
                ApplyDisplayOptions();
            });
            ActionButton(settings, "해상도", new Vector2(0f, 0f), () =>
            {
                _resolutionIndex = (_resolutionIndex + 1) % ResolutionWidths.Length;
                ApplyDisplayOptions();
            });
            ActionButton(settings, "수직 동기화", new Vector2(215f, 0f), () =>
            {
                QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
                UpdateDisplayOptions();
            });
            ActionButton(settings, "뒤로", new Vector2(0f, -180f), CloseSettings);

            var credits = BuildMenu("Credits", 71, "크레딧", out _creditsRoot);
            Label(credits, "다크 레기온 개발팀\n게임 엔진: Unity\n현재 UI 글꼴: 시스템 글꼴", 26);
            ActionButton(credits, "뒤로", new Vector2(0f, -180f), CloseCredits);

            var forfeit = BuildMenu("Forfeit", 71, "런을 포기할까요?", out _forfeitRoot);
            Label(forfeit, "현재 런은 패배로 정산됩니다.", 25);
            ActionButton(forfeit, "런 포기", new Vector2(-120f, -150f), ConfirmForfeit).name = "ConfirmForfeit";
            ActionButton(forfeit, "취소", new Vector2(120f, -150f), CancelForfeit).name = "CancelForfeit";
        }

        RectTransform BuildMenu(string name, int order, string heading, out RectTransform root)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            layer.transform.SetParent(_canvasGo.transform, false);
            var canvas = layer.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            root = Stretch(layer.transform, name + "Root");
            var backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.8f);
            var panel = Box(root, new Color(0.10f, 0.11f, 0.15f), 760f, 540f);
            Place(panel, 0f, 0f);
            Label(panel, heading, 36).rectTransform.anchoredPosition = new Vector2(0f, 210f);
            root.gameObject.SetActive(false);
            return panel;
        }

        RectTransform Box(Transform parent, Color color, float width, float height)
        {
            var go = new GameObject("Box", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            var image = go.AddComponent<Image>();
            image.sprite = _white;
            image.color = color;
            return rt;
        }

        Text Label(RectTransform parent, string text, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -6f);
            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        Button ActionButton(RectTransform parent, string text, Vector2 pos, Action action)
        {
            var box = Box(parent, new Color(0.22f, 0.24f, 0.3f, 1f), 180f, 56f);
            Place(box, pos.x, pos.y);
            Label(box, text, 24);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => action());
            return button;
        }

        static void Place(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}
