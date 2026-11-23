using System;
using System.Collections.Generic;
using System.Linq;
using DarkLegion.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DarkLegion.UI
{
    /// <summary>
    /// 1·2층 이벤트 선택 화면 (`37` §2.5). 규칙은 <see cref="EventScreenSession"/>만 부른다.
    /// 단독으로 열면(에디터 메뉴) 새 NEC 런의 이벤트 노드(층·이벤트 ID는 필드)를 스스로 만든다.
    /// 맵이 쓸 때는 Start 전에 <see cref="Attach"/>로 세션을 넘긴다.
    /// </summary>
    public sealed class EventScreenView : MonoBehaviour
    {
        /// <summary>단독 실행 층 (1 또는 2).</summary>
        public int StandaloneFloor = 1;
        /// <summary>단독 실행 이벤트 ID. 비우면 EventFlow.Enter가 노드 시드로 고른다.</summary>
        public string StandaloneEventId = "";

        EventScreenSession _session;
        EventScreenSession _attached;
        Func<string, string> _attachedNames;
        Func<string, string> _nameOf = id => id;
        readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        Font _font;
        Sprite _white;
        GameObject _canvasGo;
        GameObject _eventsGo;
        RectTransform _root;
        RectTransform _content;
        Text _header;

        internal void Attach(EventScreenSession session, Func<string, string> nameOf)
        {
            _attached = session;
            _attachedNames = nameOf;
        }

        internal Action<string, Action> HandleInput;

        internal EventScreenSession Session => _session;

        void OnDestroy()
        {
            if (_canvasGo != null) Destroy(_canvasGo);
            if (_eventsGo != null) Destroy(_eventsGo);
        }

        void Start()
        {
            _font = BattleScreenView.LoadFont();
            _white = BattleScreenView.White();
            BuildChrome();
            if (_attached != null)
            {
                _session = _attached;
                if (_attachedNames != null) _nameOf = _attachedNames;
                Refresh();
                return;
            }
            try
            {
                var data = BattleScreenView.LoadData();
                foreach (var row in data.Rows("Cards_Master")) _names[row.Str("ID")] = row.Str("카드명");
                foreach (var row in data.Rows("Cards_Text")) _names[row.Str("Card_ID")] = row.Str("Name_KO");
                foreach (var row in data.Rows("Curse_Cards")) _names[row.Str("Curse_ID")] = row.Str("Name_KO");
                foreach (var row in data.Rows("Relics_MVP")) _names[row.Str("Relic_ID")] = row.Str("Name_KO");
                _nameOf = NameOf;
                var loaded = GameDataLoader.Load(data);
                var run = RunFlow.NewRun("RUN-1", DateTime.UtcNow.Ticks, "NEC", BattleScreenSession.StartHp,
                    BattleScreenSession.NecStartDeck, new[] { BattleScreenSession.StartRelicId });
                run.Floor = StandaloneFloor;
                run.StageIndex = 2;
                string eventId = string.IsNullOrEmpty(StandaloneEventId) ? EventFlow.Enter(run, loaded) : StandaloneEventId;
                _session = new EventScreenSession(StandaloneFloor, eventId, run, loaded, null, _nameOf);
                Refresh();
            }
            catch (Exception ex)
            {
                _header.text = "이벤트 데이터를 읽지 못했습니다. " + ex.Message;
            }
        }

        string NameOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            return _names.TryGetValue(id, out var name) && name.Length > 0 ? name : id;
        }

        void Refresh()
        {
            if (_session == null) return;
            var snap = _session.Capture();
            _header.text = snap.Floor + "층 · " + snap.Title + "    HP " + snap.Hp + "/" + snap.MaxHp + "    골드 " + snap.Gold;
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            switch (snap.Phase)
            {
                case EventScreenPhase.Choosing: BuildChoosing(snap); break;
                case EventScreenPhase.Picking: BuildPicking(snap); break;
                default: BuildResult(snap); break;
            }
        }

        void BuildChoosing(EventScreenSnapshot snap)
        {
            AddText(snap.Intro, 24, 0f, 300f, 1500f, 160f, "Intro");
            foreach (var option in snap.Options)
            {
                if (string.IsNullOrEmpty(option.Label)) continue;
                string label = option.Index + ". " + option.Label;
                if (!option.Enabled && !string.IsNullOrEmpty(option.Condition)) label += "\n(" + option.Condition + ")";
                int index = option.Index;
                var button = AddButton(label, 0f, 100f - (index - 1) * 130f, 1300f, 110f, "Option" + index, () =>
                {
                    _session.Choose(index);
                    Refresh();
                });
                button.interactable = option.Enabled;
            }
        }

        void BuildPicking(EventScreenSnapshot snap)
        {
            float y = 340f;
            if (snap.NeedsSacrifice)
            {
                y = Candidates("제물로 바칠 몬스터", snap.SacrificeCandidates, snap.SelectedSacrifice, "Sacrifice", y, id => { _session.SelectSacrifice(id); Refresh(); });
            }
            if (snap.NeedsUpgrade)
            {
                y = Candidates("강화할 카드", snap.UpgradeCandidates, snap.SelectedUpgrade, "Upgrade", y, id => { _session.SelectUpgrade(id); Refresh(); });
            }
            if (snap.NeedsRelic)
            {
                AddText("유물 선택", 24, 0f, y, 1500f, 36f, "RelicTitle");
                for (int i = 0; i < snap.RelicChoices.Length; i++)
                {
                    int index = i;
                    string mark = snap.SelectedRelic == i ? "[선택] " : "";
                    AddButton(mark + _nameOf(snap.RelicChoices[i]), -420f + i * 420f, y - 70f, 380f, 80f, "Relic" + i, () => { _session.SelectRelic(index); Refresh(); });
                }
                y -= 150f;
            }
            if (snap.NeedsCard)
            {
                AddText("카드 3택1 (건너뛸 수 있다)", 24, 0f, y, 1500f, 36f, "CardTitle");
                for (int i = 0; i < snap.CardChoices.Length; i++)
                {
                    int index = i;
                    string mark = snap.SelectedCard == i ? "[선택] " : "";
                    AddButton(mark + _nameOf(snap.CardChoices[i]), -420f + i * 420f, y - 70f, 380f, 80f, "Card" + i, () => { _session.SelectCard(index); Refresh(); });
                }
                AddButton(snap.SelectedCard < 0 ? "[선택] 건너뛰기" : "건너뛰기", 0f, y - 160f, 380f, 60f, "Skip", () => { _session.SelectCard(null); Refresh(); });
            }
            AddButton("확인", 120f, -420f, 200f, 60f, "Confirm", () => { _session.Confirm(); Refresh(); }).interactable = snap.CanConfirm;
            AddButton("뒤로", -120f, -420f, 200f, 60f, "Back", () => { _session.Back(); Refresh(); });
        }

        float Candidates(string title, EventCardView[] cards, int selected, string prefix, float y, Action<int> pick)
        {
            AddText(title, 24, 0f, y, 1500f, 36f, prefix + "Title");
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                string mark = card.InstanceId == selected ? "[선택] " : "";
                AddButton(mark + _nameOf(card.CardId), -700f + (i % 8) * 200f, y - 60f - (i / 8) * 54f, 190f, 46f, prefix + card.InstanceId, () => pick(card.InstanceId));
            }
            return y - 60f - ((cards.Length + 7) / 8) * 54f - 50f;
        }

        void BuildResult(EventScreenSnapshot snap)
        {
            var r = snap.Result;
            var lines = new List<string> { r.Text, "" };
            if (r.HpDelta != 0) lines.Add("HP " + Signed(r.HpDelta) + " (" + r.HpAfter + "/" + r.MaxHpAfter + ")");
            if (r.MaxHpDelta != 0) lines.Add("최대 HP " + Signed(r.MaxHpDelta));
            if (r.GoldDelta != 0) lines.Add("골드 " + Signed(r.GoldDelta));
            if (r.RelicGained != null) lines.Add("유물 " + _nameOf(r.RelicGained));
            foreach (var c in r.CardsGained) lines.Add("카드 " + _nameOf(c));
            foreach (var c in r.CursesGained) lines.Add("저주 " + _nameOf(c));
            if (r.SacrificedCardId != null) lines.Add("제거 " + _nameOf(r.SacrificedCardId));
            foreach (var c in r.UpgradedCardIds) lines.Add("강화 " + _nameOf(c));
            if (r.ForcedBattle) lines.Add("전투가 시작된다");
            if (r.RunOutcome == RunOutcome.Defeat) lines.Add("패배");
            AddText(string.Join("\n", lines.ToArray()), 26, 0f, 120f, 1400f, 500f, "ResultText");
            AddButton("계속", 0f, -420f, 240f, 64f, "Continue", () => { _session.Continue(); Refresh(); }).interactable = !_session.Completed;
        }

        static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        void BuildChrome()
        {
            _canvasGo = new GameObject("EventCanvas");
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
            _root = NewRect("Root", _canvasGo.transform);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            var bar = Box(_root, new Color(0.08f, 0.08f, 0.1f, 1f), 1900f, 48f, 0f, 500f, "Header");
            _header = Label(bar, "", 22);
            _content = NewRect("Content", _root);
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        RectTransform Box(Transform parent, Color color, float w, float h, float x, float y, string name)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = _white;
            image.color = color;
            return rt;
        }

        Text Label(RectTransform parent, string text, int size)
        {
            var rt = NewRect("Label", parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 6f);
            rt.offsetMax = new Vector2(-8f, -6f);
            var label = rt.gameObject.AddComponent<Text>();
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

        void AddText(string text, int size, float x, float y, float w, float h, string name)
        {
            var box = Box(_content, new Color(0f, 0f, 0f, 0f), w, h, x, y, name);
            box.GetComponent<Image>().raycastTarget = false;
            Label(box, text, size);
        }

        Button AddButton(string text, float x, float y, float w, float h, string name, Action action)
        {
            var box = Box(_content, new Color(0.22f, 0.24f, 0.3f, 1f), w, h, x, y, name);
            Label(box, text, 20);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => { if (HandleInput == null) action(); else HandleInput(name, action); });
            return button;
        }
    }
}
