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
    /// 휴식 화면 (`37` §2.4·§2.9). 규칙은 <see cref="RestScreenSession"/>만 부른다.
    /// 단독으로 열면(에디터 메뉴) 1층 휴식 노드(F1-N8)의 새 NEC 런을 스스로 만든다. HP는 필드로 정한다.
    /// 맵이 쓸 때는 Start 전에 <see cref="Attach"/>로 세션을 넘긴다. 맵 연결은 이 컴포넌트의 범위 밖이다.
    /// </summary>
    public sealed class RestScreenView : MonoBehaviour
    {
        /// <summary>단독 실행 HP (회복 미리보기를 보려고 최대 HP보다 낮게 둔다).</summary>
        public int StandaloneHp = 50;

        RestScreenSession _session;
        RestScreenSession _attached;
        Func<string, string> _attachedNames;
        Func<string, string> _nameOf = id => id;
        readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        readonly Dictionary<string, string> _before = new Dictionary<string, string>();
        readonly Dictionary<string, string> _after = new Dictionary<string, string>();
        Font _font;
        Sprite _white;
        GameObject _canvasGo;
        GameObject _eventsGo;
        RectTransform _root;
        RectTransform _content;
        Text _header;

        /// <summary>맵이 만든 세션을 이어받는다 (Start 전에 호출).</summary>
        internal void Attach(RestScreenSession session, Func<string, string> nameOf)
        {
            _attached = session;
            _attachedNames = nameOf;
        }

        internal Action<string, Action> HandleInput;

        internal RestScreenSession Session => _session;

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
            try
            {
                var data = BattleScreenView.LoadData();
                LoadUpgradeText(data);
                if (_attached != null)
                {
                    _session = _attached;
                    if (_attachedNames != null) _nameOf = _attachedNames;
                    else LoadNames(data);
                    Refresh();
                    return;
                }
                LoadNames(data);
                var loaded = GameDataLoader.Load(data);
                var run = RunFlow.NewRun("RUN-1", DateTime.UtcNow.Ticks, "NEC", BattleScreenSession.StartHp,
                    BattleScreenSession.NecStartDeck, new[] { BattleScreenSession.StartRelicId });
                run.StageIndex = 7;
                run.CurrentHp = Mathf.Clamp(StandaloneHp, 1, run.MaxHp);
                _session = new RestScreenSession(run, loaded, null, _nameOf);
                Refresh();
            }
            catch (Exception ex)
            {
                _header.text = "휴식 데이터를 읽지 못했습니다. " + ex.Message;
            }
        }

        void LoadNames(GameData data)
        {
            foreach (var row in data.Rows("Cards_Master")) _names[row.Str("ID")] = row.Str("카드명");
            foreach (var row in data.Rows("Cards_Text")) _names[row.Str("Card_ID")] = row.Str("Name_KO");
            _nameOf = NameOf;
        }

        void LoadUpgradeText(GameData data)
        {
            foreach (var row in data.Rows("Cards_Upgrade"))
            {
                _before[row.Str("Card_ID")] = row.Str("Before");
                _after[row.Str("Card_ID")] = row.Str("After");
            }
            foreach (var row in data.Rows("Cards_Text"))
            {
                string id = row.Str("Card_ID");
                if (!_before.ContainsKey(id) || _before[id].Length == 0) _before[id] = row.Str("Text_KO");
                if (!_after.ContainsKey(id) || _after[id].Length == 0) _after[id] = row.Str("Text_Up_KO");
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
            _header.text = "휴식 · " + snap.NodeId + "    HP " + snap.Hp + "/" + snap.MaxHp;
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            switch (snap.Phase)
            {
                case RestScreenPhase.Choosing: BuildChoosing(snap); break;
                case RestScreenPhase.PreviewHeal: BuildHealPreview(snap); break;
                case RestScreenPhase.PickingUpgrade: BuildUpgrade(snap); break;
                default: BuildResult(snap); break;
            }
        }

        void BuildChoosing(RestScreenSnapshot snap)
        {
            if (snap.ContinueOnly)
            {
                AddText("회복할 HP도 강화할 카드도 없습니다.", 28, 0f, 200f, 1400f, 100f, "Intro");
                AddButton("계속", 0f, -100f, 240f, 64f, "Continue", () => { _session.Continue(); Refresh(); });
                return;
            }
            AddText("모닥불 곁에서 쉰다. 하나를 고르세요.", 28, 0f, 300f, 1400f, 80f, "Intro");
            string heal = "회복\nHP " + snap.Hp + " → " + snap.HealPreviewHp + " (+" + (snap.HealPreviewHp - snap.Hp) + ")";
            if (!snap.CanHeal) heal = "회복\n(HP가 가득 찼습니다)";
            AddButton(heal, -340f, 80f, 600f, 160f, "Heal", () => { _session.ChooseHeal(); Refresh(); }).interactable = snap.CanHeal;
            string up = snap.CanUpgrade ? "강화\n덱에서 카드 1장을 강화합니다" : "강화\n(강화할 카드가 없습니다)";
            AddButton(up, 340f, 80f, 600f, 160f, "Upgrade", () => { _session.ChooseUpgrade(); Refresh(); }).interactable = snap.CanUpgrade;
        }

        void BuildHealPreview(RestScreenSnapshot snap)
        {
            AddText("회복\nHP " + snap.Hp + " → " + snap.HealPreviewHp + " / " + snap.MaxHp, 32, 0f, 200f, 1400f, 160f, "Preview");
            AddButton("확정", -140f, -100f, 240f, 64f, "Confirm", () => { _session.Confirm(); Refresh(); });
            AddButton("취소", 140f, -100f, 240f, 64f, "Cancel", () => { _session.Back(); Refresh(); });
        }

        void BuildUpgrade(RestScreenSnapshot snap)
        {
            AddText("강화할 카드를 고르세요", 26, 0f, 400f, 1500f, 40f, "UpgradeTitle");
            for (int i = 0; i < snap.UpgradeCandidates.Length; i++)
            {
                var card = snap.UpgradeCandidates[i];
                int col = i % 5, row = i / 5;
                bool picked = snap.SelectedUpgrade == card.InstanceId;
                int id = card.InstanceId;
                var button = AddButton(card.Name + "\n" + card.CardId, -560f + col * 280f, 320f - row * 90f - 60f, 260f, 76f, "Card" + id, () => { _session.SelectUpgrade(id); Refresh(); });
                button.GetComponent<Image>().color = picked ? new Color(0.6f, 0.45f, 0.12f, 1f) : new Color(0.22f, 0.24f, 0.3f, 1f);
            }
            if (snap.SelectedUpgrade >= 0)
            {
                var card = snap.UpgradeCandidates.First(c => c.InstanceId == snap.SelectedUpgrade);
                _before.TryGetValue(card.CardId, out var before);
                _after.TryGetValue(card.CardId, out var after);
                AddText("강화 전: " + (string.IsNullOrEmpty(before) ? "-" : before) + "\n강화 후: " + (string.IsNullOrEmpty(after) ? "-" : after),
                    22, 0f, -200f, 1500f, 110f, "Compare");
            }
            var confirm = AddButton("확인", -140f, -380f, 240f, 64f, "Confirm", () => { _session.Confirm(); Refresh(); });
            confirm.interactable = snap.CanConfirm;
            AddButton("취소", 140f, -380f, 240f, 64f, "Cancel", () => { _session.Back(); Refresh(); });
        }

        void BuildResult(RestScreenSnapshot snap)
        {
            var r = snap.Result;
            string text = r.Healed ? "회복했습니다.\nHP " + r.HpBefore + " → " + r.HpAfter + " / " + r.MaxHp
                : r.Upgraded ? "강화했습니다.\n" + _nameOf(r.UpgradedCardId)
                : "휴식을 마칩니다.";
            AddText(text, 32, 0f, 200f, 1400f, 200f, "ResultText");
            AddButton("계속", 0f, -100f, 240f, 64f, "Continue", () => { _session.Continue(); Refresh(); }).interactable = !_session.Completed;
        }

        void BuildChrome()
        {
            _canvasGo = new GameObject("RestCanvas");
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
            Stretch(_root);
            var bar = Box(_root, new Color(0.08f, 0.08f, 0.1f, 1f), 1900f, 48f, 0f, 500f, "Header");
            _header = Label(bar, "", 22);
            _content = NewRect("Content", _root);
            Stretch(_content);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            Stretch(rt);
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
            Label(box, text, 22);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => { if (HandleInput == null) action(); else HandleInput(name, action); });
            return button;
        }
    }
}
