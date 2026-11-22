using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using DarkLegion.Core;

namespace DarkLegion.Tests.PlayMode
{
    /// <summary>
    /// 1층 노드 1 전투 화면(#46, PR #105)의 Unity PlayMode 스모크. 화면은 Assembly-CSharp에 있어 리플렉션으로 만든다.
    /// 안내(D-32)를 켠 채 `37` §3 순서대로 클릭만으로 진행한다. 기대값은 `BattleScreenTests`에 손으로 계산한 값과 같다.
    /// 마우스 드래그·키보드·시각 배치는 batchmode에서 볼 수 없어 여기서 확인하지 않는다.
    /// </summary>
    public sealed class BattleScreenPlayModeTests
    {
        GameObject _root;
        Component _view;
        Type _clicker;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var viewType = Type.GetType("DarkLegion.UI.BattleScreenView, Assembly-CSharp");
            Assert.NotNull(viewType, "BattleScreenView를 찾지 못했습니다");
            _clicker = viewType.Assembly.GetType("DarkLegion.UI.Clicker");
            Assert.NotNull(_clicker);
            _root = new GameObject("BattleScreen");
            _view = _root.AddComponent(viewType);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            // 화면은 BattleCanvas와 EventSystem을 따로 만들므로 같이 지운다.
            foreach (var name in new[] { "BattleCanvas", "EventSystem" })
            {
                var go = GameObject.Find(name);
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        T Field<T>(string name) => (T)_view.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_view);

        BattleScreenSession Session => Field<BattleScreenSession>("_session");
        BattleScreenSnapshot Snap => Session.Capture();

        static void Click(Component clicker)
        {
            var left = clicker.GetType().GetField("Left").GetValue(clicker) as Action;
            Assert.NotNull(left, "Left 동작이 없습니다");
            left();
        }

        Component ClickerOn(Transform t) => t.GetComponent(_clicker);

        IEnumerator ClickHand(string cardId)
        {
            var row = Field<RectTransform>("_handRow");
            int index = Array.FindIndex(Snap.Hand, c => c.CardId == cardId);
            Assert.GreaterOrEqual(index, 0, cardId + "가 손패에 없습니다");
            Click(ClickerOn(row.GetChild(index)));
            yield return null;
        }

        IEnumerator ClickAlly(SlotPosition position)
        {
            var row = Field<RectTransform>("_allyRow");
            int index = Array.FindIndex(Snap.Allies, s => s.Position == position);
            Click(ClickerOn(row.GetChild(index)));
            yield return null;
        }

        IEnumerator ClickButton(string label)
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetComponentInChildren<Text>().text == label);
            Assert.NotNull(button, label + " 버튼이 없습니다");
            button.onClick.Invoke();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Start_ShowsFirstBattleFromCore()
        {
            Assert.NotNull(Session, Field<Text>("_top").text);
            var s = Snap;
            Assert.AreEqual(80, s.Hp);
            Assert.AreEqual(3, s.Crystal);
            Assert.AreEqual(5, s.Hand.Length);
            Assert.AreEqual(1, s.Turn);
            Assert.AreEqual(1, s.TutorialStep);
            Assert.AreEqual(5, Field<RectTransform>("_handRow").childCount);
            Assert.AreEqual(2, s.Enemies.Count(e => e.Occupied));
            Assert.IsTrue(s.Enemies.Where(e => e.Occupied).All(e => e.IntentLabel == "단일 공격"), "의도는 종류 글자만");
            yield break;
        }

        [UnityTest]
        public IEnumerator Tutorial_LocksEndTurnAndFusion()
        {
            yield return ClickButton("턴 종료");
            Assert.AreEqual(1, Snap.Turn, "안내 중에는 턴 종료가 잠깁니다");
            Assert.IsFalse(Snap.EndTurnEnabled);
            Assert.IsFalse(Snap.FusionEnabled);
            string other = Snap.Hand.First(c => c.CardId != Snap.HighlightCardId).CardId;
            int before = Snap.Hand.Length;
            yield return ClickHand(other);
            Assert.AreEqual(before, Snap.Hand.Length, "안내가 요구하지 않은 카드는 쓸 수 없습니다");
            Assert.AreEqual(3, Snap.Crystal);
        }

        [UnityTest]
        public IEnumerator FirstBattle_Guided_SummonSacrificeLegionCommandEndTurn()
        {
            // 1단계: 파편 소환 (비용 0)
            string shard = Snap.HighlightCardId;
            var slot = Snap.HighlightAllySlot ?? SlotPosition.Front;
            yield return ClickHand(shard);
            yield return ClickAlly(slot);
            Assert.AreEqual(3, Snap.Crystal);
            Assert.AreEqual(4, Snap.Hand.Length);
            Assert.IsTrue(Snap.Allies.Single(a => a.Position == slot).Occupied);
            Assert.AreEqual(2, Snap.TutorialStep);

            // 2단계: 영혼 추출 → 파편 희생 (Crystal 3 + 2 = 5)
            yield return ClickHand(Snap.HighlightCardId);
            yield return ClickAlly(slot);
            Assert.AreEqual(3, Snap.Crystal);
            yield return ClickAlly(slot);
            yield return ClickButton("희생");
            yield return ClickButton("확인");
            Assert.AreEqual(5, Snap.Crystal);
            Assert.AreEqual(3, Snap.TutorialStep);

            // 3단계: 뼈 병사와 구울 (각 비용 1, 나머지 Crystal 3)
            var empty = Snap.Allies.Where(a => !a.Occupied).Select(a => a.Position).ToArray();
            yield return ClickHand(Snap.HighlightCardId);
            yield return ClickAlly(empty[0]);
            yield return ClickHand(Snap.HighlightCardId2 ?? Snap.Hand.First(c => c.Type == CardType.Monster).CardId);
            yield return ClickAlly(empty[1]);
            Assert.AreEqual(3, Snap.Crystal);
            Assert.AreEqual(2, Snap.Allies.Count(a => a.Occupied));
            Assert.AreEqual(0, Snap.TutorialStep, "1턴의 안내 3단계가 끝나면 입력 잠금이 풀립니다");
            Assert.IsTrue(Snap.EndTurnEnabled);

            // 공격/방어 전환
            var ally = Snap.Allies.First(a => a.Occupied);
            var before = ally.Command;
            int allyIndex = Array.FindIndex(Snap.Allies, a => a.UnitId == ally.UnitId);
            var cmd = Field<RectTransform>("_allyRow").GetChild(allyIndex).GetComponentsInChildren(_clicker, true).Skip(1).First();
            Click(cmd);
            yield return null;
            Assert.AreNotEqual(before, Snap.Allies.First(a => a.UnitId == ally.UnitId).Command, "공격/방어가 바뀌어야 합니다");

            // 턴 종료: 적 턴을 지나 턴 2, 안내 4(의도)·5(공격/방어)는 팁이라 3초 뒤 닫힌다
            yield return ClickButton("턴 종료");
            Assert.AreEqual(2, Snap.Turn);
            Assert.AreEqual(BattleOutcome.None, Snap.Outcome);
            Assert.AreEqual(4, Snap.TutorialStep);
            yield return new WaitForSecondsRealtime(3.3f);
            Assert.AreEqual(5, Snap.TutorialStep);
            yield return new WaitForSecondsRealtime(3.3f);
            Assert.AreEqual(0, Snap.TutorialStep);
        }
    }
}
