using System;
using System.Collections.Generic;
using System.IO;
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
    /// 1층 노드 1 전투 화면 (`37` §2.1·§3). 규칙은 <see cref="BattleScreenSession"/>만 부른다.
    /// 글자는 uGUI Text다. 한글 폰트가 OS에 있으면 그걸 쓰고, 없으면 카드 ID가 남는다.
    ///</summary>
    public sealed class BattleScreenView : MonoBehaviour
    {
        BattleScreenSession _session;
        GameData _data;
        readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        readonly Dictionary<string, string> _cardText = new Dictionary<string, string>();
        Font _font;
        Sprite _white;
        int _selectedCard = -1;
        string _selectedAlly;
        int _fuseA = -1;
        int _fuseB = -1;
        int _shownTip = -1;
        bool _modal;
        RectTransform _allyRow;
        RectTransform _enemyRow;
        RectTransform _handRow;
        Text _top;
        Text _resources;
        Text _tutorial;
        Text _outcome;
        Button _endTurn;
        Button _sacrifice;
        Button _fusion;
        RectTransform _modalRoot;
        Text _modalText;

        void Start()
        {
            _font = LoadFont();
            _white = White();
            BuildChrome();
            try
            {
                _data = LoadData();
                RememberNames(_data);
                var loaded = GameDataLoader.Load(_data);
                _session = BattleScreenSession.StartFirstNecBattle(loaded, DateTime.UtcNow.Ticks, tutorialEnabled: true);
                Refresh();
            }
            catch (Exception ex)
            {
                _top.text = "전투 데이터를 읽지 못했습니다. " + ex.Message;
            }
        }

        void Update()
        {
            if (_session == null) return;
            var before = _session.TutorialStep;
            if (before == 4 || before == 5) _session.AdvanceTipClock(Time.unscaledDeltaTime);
            var keys = Keyboard.current;
            if (keys != null) ReadKeys(keys);
            if (_session.TutorialStep != _shownTip) Refresh();
        }

        void LateUpdate()
        {
            DragSource.SkipClick = false;
        }

        void ReadKeys(Keyboard keys)
        {
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (_modal) CloseModal();
                else ClearSelection();
                Refresh();
                return;
            }
            if (_modal || _session.Context.Outcome != BattleOutcome.None) return;
            if (keys.spaceKey.wasPressedThisFrame && _session.Allows(BattleScreenInput.EndTurn))
            {
                _session.TryEndTurn();
                Refresh();
                return;
            }
            if (keys.sKey.wasPressedThisFrame) OpenSacrifice();
            if (keys.fKey.wasPressedThisFrame) OpenFusion();
            if (keys.dKey.wasPressedThisFrame) ShowPile("덱", _session.Capture().DrawPile);
            var digits = new[]
            {
                Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
                Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
            };
            var hand = _session.Capture().Hand;
            for (int i = 0; i < digits.Length && i < hand.Length; i++)
            {
                if (keys[digits[i]].wasPressedThisFrame) ClickCard(hand[i]);
            }
        }

        public void DropOn(SlotPosition position, bool allySlot, int cardInstance, string unitId)
        {
            if (_session == null || _modal || _session.Context.Outcome != BattleOutcome.None) return;
            if (cardInstance >= 0 && allySlot)
            {
                var card = _session.Capture().Hand.FirstOrDefault(c => c.InstanceId == cardInstance);
                if (card != null && card.Type == CardType.Monster) _session.TrySummon(cardInstance, position);
            }
            else if (!string.IsNullOrEmpty(unitId) && allySlot)
            {
                _session.TryMove(unitId, position);
            }
            ClearSelection();
            Refresh();
        }

        void ClickCard(HandCardView card)
        {
            if (_session.Capture().WaitingDiscard)
            {
                _session.TryDiscard(card.InstanceId);
                Refresh();
                return;
            }
            if (!card.Playable)
            {
                ShowDetail(CardDetail(card));
                return;
            }
            if (card.Type == CardType.Monster || card.NeedsTarget)
            {
                _selectedCard = card.InstanceId;
                _selectedAlly = null;
                Refresh();
                return;
            }
            _session.TryPlay(card.InstanceId, null);
            ClearSelection();
            Refresh();
        }

        void ClickAlly(SlotView slot)
        {
            var snap = _session.Capture();
            if (snap.WaitingReplace && slot.Occupied)
            {
                _session.TryReplace(slot.UnitId);
                Refresh();
                return;
            }
            if (_selectedCard >= 0)
            {
                var card = snap.Hand.FirstOrDefault(c => c.InstanceId == _selectedCard);
                if (card != null && card.Type == CardType.Monster && !slot.Occupied)
                    _session.TrySummon(_selectedCard, slot.Position);
                else if (card != null && card.NeedsTarget && slot.Occupied)
                    _session.TryPlay(_selectedCard, slot.UnitId);
                ClearSelection();
                Refresh();
                return;
            }
            if (_selectedAlly != null && !slot.Occupied)
            {
                _session.TryMove(_selectedAlly, slot.Position);
                ClearSelection();
                Refresh();
                return;
            }
            _selectedAlly = slot.Occupied ? slot.UnitId : null;
            Refresh();
        }

        void ClickEnemy(SlotView slot)
        {
            if (!slot.Occupied) return;
            var snap = _session.Capture();
            if (_selectedCard >= 0)
            {
                var card = snap.Hand.FirstOrDefault(c => c.InstanceId == _selectedCard);
                if (card != null && card.NeedsTarget) _session.TryPlay(_selectedCard, slot.UnitId);
                ClearSelection();
                Refresh();
                return;
            }
            if (_selectedAlly != null) _session.TrySetAttackTarget(_selectedAlly, slot.UnitId);
            Refresh();
        }

        void OpenSacrifice()
        {
            if (_modal || string.IsNullOrEmpty(_selectedAlly)) return;
            if (!_session.Allows(BattleScreenInput.Sacrifice)) return;
            int gain = _session.SacrificeGain(_selectedAlly);
            if (gain <= 0) return;
            ShowModal("희생\nCrystal +" + gain + "\n확인하면 그 아군을 희생한다.", ConfirmSacrifice, "확인");
        }

        void ConfirmSacrifice()
        {
            _session.TrySacrifice(_selectedAlly);
            CloseModal();
            ClearSelection();
            Refresh();
        }

        void OpenFusion()
        {
            if (_modal || !_session.Capture().FusionEnabled) return;
            _fuseA = -1;
            _fuseB = -1;
            ShowModal(FusionBody(), ConfirmFusion, "융합");
            var monsters = _session.Capture().Hand.Where(c => c.Type == CardType.Monster).ToArray();
            for (int i = 0; i < monsters.Length; i++)
            {
                var card = monsters[i];
                var button = ActionButton(_modalRoot, NameOf(card.CardId), new Vector2(-280f + (i % 4) * 180f, 80f - (i / 4) * 48f), () => PickFusion(card.InstanceId));
                button.name = "Pick";
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 40f);
            }
        }

        void ClearPicks()
        {
            if (_modalRoot == null) return;
            for (int i = _modalRoot.childCount - 1; i >= 0; i--)
            {
                var child = _modalRoot.GetChild(i);
                if (child.name == "Pick") Destroy(child.gameObject);
            }
        }

        string FusionBody()
        {
            var lines = new List<string> { "같은 레벨 몬스터 2장. 비용·충전·예상 결과." };
            var hand = _session.Capture().Hand.Where(c => c.Type == CardType.Monster).ToArray();
            foreach (var card in hand)
            {
                string mark = card.InstanceId == _fuseA || card.InstanceId == _fuseB ? "[선택] " : "";
                lines.Add(mark + NameOf(card.CardId) + " (" + card.CardId + ") 비용 " + card.Cost);
            }
            if (_fuseA >= 0 && _fuseB >= 0)
            {
                var preview = _session.PreviewFusion(_fuseA, _fuseB);
                string result = preview.Random ? "랜덤" : NameOf(preview.ResultCardId) + " (" + preview.ResultCardId + ")";
                lines.Add("비용 " + preview.Cost + " / 결과 " + result);
            }
            return string.Join("\n", lines.ToArray());
        }

        void ConfirmFusion()
        {
            if (_fuseA >= 0 && _fuseB >= 0) _session.TryFuse(_fuseA, _fuseB);
            CloseModal();
            ClearSelection();
            Refresh();
        }

        void PickFusion(int instanceId)
        {
            if (_fuseA < 0) _fuseA = instanceId;
            else if (_fuseA == instanceId) _fuseA = -1;
            else if (_fuseB < 0) _fuseB = instanceId;
            else if (_fuseB == instanceId) _fuseB = -1;
            else _fuseB = instanceId;
            if (_modalText != null) _modalText.text = FusionBody();
        }

        void ShowPile(string title, string[] ids)
        {
            string body = ids.Length == 0 ? "(없음)" : string.Join("\n", ids.Select(NameOf).ToArray());
            ShowModal(title + "\n" + body, CloseModal, "닫기");
        }

        void ShowDetail(string body) => ShowModal(body, CloseModal, "닫기");

        string CardDetail(HandCardView card)
        {
            string text;
            _cardText.TryGetValue(card.CardId, out text);
            if (string.IsNullOrEmpty(text)) text = "비용 " + card.Cost;
            string up = card.Upgraded ? " 강화" : "";
            return NameOf(card.CardId) + " (" + card.CardId + ")" + up + "\n비용 " + card.Cost + "\n" + text;
        }

        void Refresh()
        {
            if (_session == null) return;
            var snap = _session.Capture();
            _shownTip = snap.TutorialStep;
            _top.text = "층 " + snap.Floor + " · 노드 " + snap.Node
                + "    턴 " + snap.Turn + "/" + snap.MaxTurn
                + "    덱 " + snap.DrawCount + "  버림 " + snap.DiscardCount + "  소멸 " + snap.ExhaustCount;
            string shield = snap.PlayerShield > 0 ? "  Shield " + snap.PlayerShield : "";
            string statuses = snap.PlayerStatuses.Length == 0
                ? ""
                : "  " + string.Join(" ", snap.PlayerStatuses.Select(s => s.Name + s.Amount).ToArray());
            _resources.text = "HP " + snap.Hp + "/" + snap.MaxHp + shield + statuses
                + "    Crystal " + snap.Crystal
                + "    융합 충전 " + Pips(snap.FusionCharge);
            _tutorial.text = snap.TutorialMessage ?? "";
            _endTurn.interactable = snap.EndTurnEnabled;
            _sacrifice.interactable = snap.SacrificeEnabled && !string.IsNullOrEmpty(_selectedAlly);
            _fusion.interactable = snap.FusionEnabled;
            Tint(_sacrifice, snap.HighlightSacrifice);
            Rebuild(_allyRow, snap.Allies, true, snap);
            Rebuild(_enemyRow, snap.Enemies, false, snap);
            RebuildHand(snap);
            _outcome.rectTransform.parent.GetComponent<Image>().raycastTarget = snap.Outcome != BattleOutcome.None;
            if (snap.Outcome == BattleOutcome.None)
            {
                _outcome.text = "";
            }
            else
            {
                _outcome.text = (snap.Outcome == BattleOutcome.Victory ? "승리" : "패배")
                    + "\nCore " + snap.Outcome + "\nHP " + snap.Hp + "/" + snap.MaxHp;
            }
        }

        static string Pips(int charge)
        {
            var chars = new char[3];
            for (int i = 0; i < 3; i++) chars[i] = i < charge ? '●' : '○';
            return new string(chars);
        }

        static void Tint(Button button, bool on)
        {
            var colors = button.colors;
            colors.normalColor = on ? new Color(0.95f, 0.75f, 0.25f, 1f) : Color.white;
            button.colors = colors;
        }

        void Rebuild(RectTransform row, SlotView[] slots, bool allies, BattleScreenSnapshot snap)
        {
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            foreach (var slot in slots)
            {
                if (slot.Hidden) continue;
                bool highlight = allies && snap.HighlightAllySlot == slot.Position;
                var box = Box(row, highlight ? new Color(0.55f, 0.42f, 0.12f, 1f) : allies ? AllyColor(slot) : EnemyColor(slot), allies ? 270 : 200, 250);
                string title = allies ? slot.Position.ToString() : slot.Position + (string.IsNullOrEmpty(slot.IntentLabel) ? "" : "\n" + slot.IntentLabel + " (" + slot.Intent + ")");
                if (!slot.Occupied)
                {
                    Label(box, title + "\n(빈 칸)", 18);
                }
                else
                {
                    string command = allies ? "\n" + slot.CommandLabel + " (" + slot.Command + ")" : "";
                    Label(box, title + "\n" + NameOf(slot.DataId) + " (" + slot.DataId + ")"
                        + "\nHP " + slot.Hp + "/" + slot.MaxHp
                        + "  ATK " + slot.Atk + "  DEF " + slot.Def
                        + "  Shield " + slot.Shield
                        + command
                        + "\n" + string.Join(" ", slot.Statuses.Select(s => s.Name + s.Amount).ToArray()), 16);
                }
                var click = box.gameObject.AddComponent<Clicker>();
                var captured = slot;
                click.Left = () => { if (allies) ClickAlly(captured); else ClickEnemy(captured); };
                click.Right = () => { if (captured.Occupied) ShowDetail(UnitDetail(captured)); };
                var drag = box.gameObject.AddComponent<DragSource>();
                drag.Owner = this;
                drag.UnitId = slot.Occupied && allies ? slot.UnitId : null;
                var drop = box.gameObject.AddComponent<SlotDrop>();
                drop.Owner = this;
                drop.Position = slot.Position;
                drop.AllySlot = allies;
                if (allies && slot.Occupied)
                {
                    var cmd = Box(box, new Color(0.25f, 0.25f, 0.3f, 1f), 120, 36);
                    cmd.anchorMin = new Vector2(0.5f, 0f);
                    cmd.anchorMax = new Vector2(0.5f, 0f);
                    cmd.anchoredPosition = new Vector2(0f, 24f);
                    Label(cmd, slot.CommandLabel + " (" + slot.Command + ")", 16);
                    var cmdClick = cmd.gameObject.AddComponent<Clicker>();
                    string unitId = slot.UnitId;
                    cmdClick.Left = () =>
                    {
                        _session.ToggleCommand(unitId);
                        Refresh();
                    };
                }
            }
        }

        void RebuildHand(BattleScreenSnapshot snap)
        {
            for (int i = _handRow.childCount - 1; i >= 0; i--) Destroy(_handRow.GetChild(i).gameObject);
            for (int i = 0; i < snap.Hand.Length; i++)
            {
                var card = snap.Hand[i];
                bool selected = card.InstanceId == _selectedCard;
                var box = Box(_handRow, CardColor(card, selected, snap), 170, 140);
                string key = i < 9 ? (i + 1).ToString() : "0";
                Label(box, key + "  " + NameOf(card.CardId) + " (" + card.CardId + ")"
                    + (card.Upgraded ? " 강화" : "")
                    + "\n비용 " + card.Cost
                    + (card.Playable ? "" : "\n사용 불가"), 16);
                var click = box.gameObject.AddComponent<Clicker>();
                var captured = card;
                click.Left = () => ClickCard(captured);
                click.Right = () => ShowDetail(CardDetail(captured));
                var drag = box.gameObject.AddComponent<DragSource>();
                drag.Owner = this;
                drag.CardInstance = card.InstanceId;
            }
        }

        static Color CardColor(HandCardView card, bool selected, BattleScreenSnapshot snap)
        {
            bool highlight = card.CardId == snap.HighlightCardId || card.CardId == snap.HighlightCardId2;
            if (selected) return new Color(0.75f, 0.6f, 0.2f, 1f);
            if (!card.Playable) return new Color(0.2f, 0.2f, 0.22f, 1f);
            if (highlight) return new Color(0.45f, 0.4f, 0.15f, 1f);
            return new Color(0.18f, 0.22f, 0.28f, 1f);
        }

        static Color AllyColor(SlotView slot)
        {
            if (slot.Occupied && slot.Command == UnitCommand.Defend) return new Color(0.15f, 0.25f, 0.35f, 1f);
            return new Color(0.12f, 0.16f, 0.22f, 1f);
        }

        static Color EnemyColor(SlotView slot)
        {
            if (!string.IsNullOrEmpty(slot.IntentLabel)) return new Color(0.28f, 0.14f, 0.14f, 1f);
            return new Color(0.16f, 0.12f, 0.12f, 1f);
        }

        string UnitDetail(SlotView slot)
        {
            return NameOf(slot.DataId) + " (" + slot.DataId + ")\nHP " + slot.Hp + "/" + slot.MaxHp
                + " ATK " + slot.Atk + " DEF " + slot.Def + " Shield " + slot.Shield
                + "\n" + string.Join(" ", slot.Statuses.Select(s => s.Name + " " + s.Amount).ToArray());
        }

        void ClearSelection()
        {
            _selectedCard = -1;
            _selectedAlly = null;
        }

        void ShowModal(string body, Action confirm, string confirmLabel)
        {
            _modal = true;
            ClearPicks();
            _modalRoot.gameObject.SetActive(true);
            _modalText.text = body;
            var confirmButton = _modalRoot.Find("Confirm").GetComponent<Button>();
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => confirm());
            confirmButton.GetComponentInChildren<Text>().text = confirmLabel;
        }

        void CloseModal()
        {
            _modal = false;
            if (_modalRoot != null) _modalRoot.gameObject.SetActive(false);
        }

        void BuildChrome()
        {
            var canvasGo = new GameObject("BattleCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            var root = Stretch(canvasGo.transform);
            _top = Label(Box(root, new Color(0.08f, 0.08f, 0.1f, 1f), 1900, 48), "", 22);
            Place(_top.rectTransform.parent as RectTransform, 0f, 500f);
            _allyRow = Row(root, -40f);
            _enemyRow = Row(root, -40f);
            var rows = Box(root, new Color(0f, 0f, 0f, 0f), 1900, 280);
            Place(rows, 0f, 220f);
            _allyRow.SetParent(rows, false);
            _enemyRow.SetParent(rows, false);
            _allyRow.sizeDelta = new Vector2(860f, 260f);
            _enemyRow.sizeDelta = new Vector2(1040f, 260f);
            _allyRow.anchoredPosition = new Vector2(-470f, 0f);
            _enemyRow.anchoredPosition = new Vector2(470f, 0f);
            _allyRow.GetComponent<Image>().raycastTarget = false;
            _enemyRow.GetComponent<Image>().raycastTarget = false;
            rows.GetComponent<Image>().raycastTarget = false;

            var bar = Box(root, new Color(0.1f, 0.1f, 0.12f, 1f), 1900, 56);
            Place(bar, 0f, -20f);
            _resources = Label(bar, "", 22);
            _endTurn = ActionButton(bar, "턴 종료", new Vector2(760f, 0f), () => { _session.TryEndTurn(); Refresh(); });
            _sacrifice = ActionButton(bar, "희생", new Vector2(560f, 0f), OpenSacrifice);
            _fusion = ActionButton(bar, "융합", new Vector2(640f, 0f), OpenFusion);
            Tiny(bar, "덱", new Vector2(-760f, 0f), () => ShowPile("덱", _session.Capture().DrawPile));
            Tiny(bar, "버림", new Vector2(-680f, 0f), () => ShowPile("버린 더미", _session.Capture().DiscardPile));
            Tiny(bar, "소멸", new Vector2(-600f, 0f), () => ShowPile("소멸 더미", _session.Capture().ExhaustPile));

            _handRow = Row(root, -340f);
            _handRow.GetComponent<Image>().raycastTarget = false;
            var tutorialBar = Box(root, new Color(0.12f, 0.1f, 0.05f, 1f), 1900, 72);
            Place(tutorialBar, 0f, -500f);
            _tutorial = Label(tutorialBar, "", 20);
            var outcomeBar = Box(root, new Color(0f, 0f, 0f, 0.72f), 1900, 1080);
            Place(outcomeBar, 0f, 0f);
            outcomeBar.GetComponent<Image>().raycastTarget = false;
            _outcome = Label(outcomeBar, "", 48);
            _outcome.raycastTarget = false;

            _modalRoot = Box(root, new Color(0f, 0f, 0f, 0.86f), 900, 640);
            Place(_modalRoot, 0f, 0f);
            _modalText = Label(_modalRoot, "", 22);
            var confirm = ActionButton(_modalRoot, "확인", new Vector2(-120f, -260f), () => { });
            confirm.name = "Confirm";
            ActionButton(_modalRoot, "취소", new Vector2(120f, -260f), () => { CloseModal(); Refresh(); }).name = "Cancel";
            _modalRoot.gameObject.SetActive(false);
        }

        Button Tiny(RectTransform parent, string text, Vector2 pos, Action action)
        {
            var button = ActionButton(parent, text, pos, action);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 32f);
            return button;
        }

        Button ActionButton(RectTransform parent, string text, Vector2 pos, Action action)
        {
            var box = Box(parent, new Color(0.22f, 0.24f, 0.3f, 1f), 110, 40);
            box.anchoredPosition = pos;
            Label(box, text, 18);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => action());
            return button;
        }

        RectTransform Row(Transform parent, float y)
        {
            var row = Box(parent, new Color(0f, 0f, 0f, 0f), 1800, 160);
            Place(row, 0f, y);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
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
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
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

        static void Place(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        static RectTransform Stretch(Transform parent)
        {
            var go = new GameObject("Root", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        string NameOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string name;
            return _names.TryGetValue(id, out name) && name.Length > 0 ? name : id;
        }

        static Font LoadFont()
        {
            try
            {
                var os = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" }, 20);
                if (os != null) return os;
            }
            catch (Exception)
            {
                // OS 폰트가 없으면 기본 폰트. 카드 ID는 그대로 읽힌다.
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static Sprite White()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        static GameData LoadData()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DesignData"));
            var texts = Directory.GetFiles(dir, "*.json").ToDictionary(
                f => Path.GetFileNameWithoutExtension(f),
                f => File.ReadAllText(f));
            return GameData.FromJson(texts);
        }

        void RememberNames(GameData data)
        {
            foreach (var row in data.Rows("Cards_Master")) _names[row.Str("ID")] = row.Str("카드명");
            foreach (var row in data.Rows("Cards_Text"))
            {
                _names[row.Str("Card_ID")] = row.Str("Name_KO");
                _cardText[row.Str("Card_ID")] = row.Str("Text_KO");
            }
            foreach (var row in data.Rows("Enemies_Master")) _names[row.Str("ENE_ID")] = row.Str("Name_KO");
        }
    }

    sealed class Clicker : MonoBehaviour, IPointerClickHandler
    {
        public Action Left;
        public Action Right;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (DragSource.SkipClick)
            {
                DragSource.SkipClick = false;
                return;
            }
            if (eventData.button == PointerEventData.InputButton.Right) { if (Right != null) Right(); }
            else if (Left != null) Left();
        }
    }

    sealed class SlotDrop : MonoBehaviour
    {
        public BattleScreenView Owner;
        public SlotPosition Position;
        public bool AllySlot;
    }

    sealed class DragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static bool SkipClick;
        public BattleScreenView Owner;
        public int CardInstance = -1;
        public string UnitId;
        RectTransform _ghost;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _ghost = new GameObject("Drag", typeof(RectTransform)).GetComponent<RectTransform>();
            _ghost.SetParent(transform.root, false);
            _ghost.sizeDelta = new Vector2(80f, 40f);
            var image = _ghost.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.35f);
            image.raycastTarget = false;
            _ghost.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Vector2.Distance(eventData.pressPosition, eventData.position) > 8f) SkipClick = true;
            if (_ghost != null) _ghost.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
                Destroy(_ghost.gameObject);
            }
            if (!SkipClick) return;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            foreach (var hit in hits)
            {
                var slot = hit.gameObject.GetComponentInParent<SlotDrop>();
                if (slot == null || slot.Owner == null) continue;
                slot.Owner.DropOn(slot.Position, slot.AllySlot, CardInstance, UnitId);
                break;
            }
        }
    }
}
