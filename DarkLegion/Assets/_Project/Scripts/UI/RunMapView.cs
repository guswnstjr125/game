using System;
using System.Linq;
using DarkLegion.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DarkLegion.UI
{
    /// <summary>
    /// 18노드 맵과 전투 결과 화면 (`37` §1·§2). 규칙은 <see cref="RunMapSession"/>만 부르고,
    /// 전투는 <see cref="BattleScreenView"/>가 같은 런 상태로 진행한다. 타이틀·보상 선택·휴식·이벤트·저장은 이번 범위 밖이다.
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

        void Start()
        {
            _font = BattleScreenView.LoadFont();
            _white = BattleScreenView.White();
            Build();
            try
            {
                _data = BattleScreenView.LoadData();
                foreach (var row in _data.Rows("Cards_Text")) _names[row.Str("Card_ID")] = row.Str("Name_KO");
                foreach (var row in _data.Rows("Relics_MVP")) _names[row.Str("Relic_ID")] = row.Str("Name_KO");
                var loaded = GameDataLoader.Load(_data);
                _map = RunMapSession.NewNecRun(loaded, DateTime.UtcNow.Ticks, tutorialEnabled: true);
                RefreshMap();
            }
            catch (Exception ex)
            {
                _top.text = "데이터를 읽지 못했습니다. " + ex.Message;
            }
        }

        void OnDestroy()
        {
            if (_battleGo != null) Destroy(_battleGo);
            if (_canvasGo != null) Destroy(_canvasGo);
            if (_eventsGo != null) Destroy(_eventsGo);
        }

        void RefreshMap()
        {
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
                    ? current.Id + " " + current.KindLabel + ": 노드를 눌러 전투에 들어갑니다."
                    : current.Id + " " + current.KindLabel + ": 이 노드의 화면은 아직 없습니다.";
            }
        }

        void ClickNode(MapNodeView node)
        {
            if (!node.Enterable || !_map.CanEnter(node.Floor, node.Node)) return;
            var session = _map.EnterBattle();
            _mapRoot.gameObject.SetActive(false);
            _battleGo = new GameObject("BattleScreen");
            var view = _battleGo.AddComponent<BattleScreenView>();
            view.Attach(session, _data, OnBattleDone);
        }

        void OnBattleDone()
        {
            if (_battleGo != null) Destroy(_battleGo);
            _battleGo = null;
            ShowResult(_map.Result());
        }

        void ShowResult(BattleResultView result)
        {
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
                b.onClick.AddListener(() => { _pickCard = _pickCard == index ? (int?)null : index; RenderChoices(); });
            }
            for (int i = 0; i < _result.RelicChoices.Length; i++)
            {
                int index = i;
                bool picked = _pickRelic == i;
                var b = ChoiceButton("유물 " + NameOf(_result.RelicChoices[i]) + "\n" + _result.RelicChoices[i], new Vector2(-340f + i * 340f, -140f), picked);
                b.onClick.AddListener(() => { _pickRelic = index; RenderChoices(); });
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

        void OnConfirm()
        {
            if (_result == null || !_result.CanConfirm) return;
            if (!_map.ConfirmReward(_pickCard, _pickRelic)) return;
            _result = null;
            RefreshMap();
            if (_map.Capture().Outcome == RunOutcome.Victory) ShowRunEnd();
        }

        void ShowRunEnd()
        {
            _resultRoot.gameObject.SetActive(true);
            _mapRoot.gameObject.SetActive(false);
            var snap = _map.Capture();
            _resultText.text = "런 승리\n2층 보스를 처치했습니다.\nHP " + snap.Hp + "/" + snap.MaxHp + "  골드 " + snap.Gold;
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
            _map.Restart(DateTime.UtcNow.Ticks);
            RefreshMap();
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
            _restart = ActionButton(panel, "다시 시작", new Vector2(0f, -290f), OnRestart);
            _resultRoot.gameObject.SetActive(false);
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
