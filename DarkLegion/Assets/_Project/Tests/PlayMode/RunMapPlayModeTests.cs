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
    /// 18노드 맵 → 전투 → 결과 → 맵 (`37` §1·§2)의 Unity PlayMode 스모크. 화면은 Assembly-CSharp에 있어 리플렉션으로 만든다.
    /// 전투 승패는 Core의 `EvaluateOutcome`으로 만들고(전투 규칙은 `BattleScreenPlayModeTests`와 EditMode가 본다),
    /// 여기서는 화면 전환과 표시를 본다. 마우스·드래그·글자 겹침은 batchmode에서 볼 수 없다.
    /// </summary>
    public sealed class RunMapPlayModeTests
    {
        GameObject _root;
        Component _view;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var type = Type.GetType("DarkLegion.UI.RunMapView, Assembly-CSharp");
            Assert.NotNull(type, "RunMapView를 찾지 못했습니다");
            _root = new GameObject("RunMap");
            _view = _root.AddComponent(type);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            // 화면 정리는 RunMapView.OnDestroy와 BattleScreenView.OnDestroy가 한다. 남은 것이 있으면 이름으로 지운다.
            foreach (var name in new[] { "BattleScreen", "MapCanvas", "BattleCanvas", "EventSystem" })
            {
                var go = GameObject.Find(name);
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        RunMapSession Map => (RunMapSession)_view.GetType().GetField("_map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_view);

        static Button[] Buttons => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);

        static string TextOf(Button b) => b.GetComponentInChildren<Text>().text;

        static string AllText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text));

        IEnumerator Press(Func<Button, bool> match, string what)
        {
            var button = Buttons.FirstOrDefault(match);
            Assert.NotNull(button, what + " 버튼이 없습니다");
            button.onClick.Invoke();
            yield return null;
            yield return null;
        }

        Component BattleView()
        {
            var go = GameObject.Find("BattleScreen");
            Assert.NotNull(go, "전투 화면이 열리지 않았습니다");
            var type = Type.GetType("DarkLegion.UI.BattleScreenView, Assembly-CSharp");
            return go.GetComponent(type);
        }

        IEnumerator EndBattle(BattleOutcome outcome)
        {
            var battle = BattleView();
            var session = (BattleScreenSession)battle.GetType().GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(battle);
            if (outcome == BattleOutcome.Victory) session.Context.EvaluateOutcome(session.Context.Player.CurrentHP, 0);
            else
            {
                session.Context.Player.CurrentHP = 0;
                session.Context.EvaluateOutcome(0, 2);
            }
            battle.GetType().GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(battle, null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Start_ShowsEighteenNodes_WithFirstNodeCurrent()
        {
            Assert.NotNull(Map, AllText());
            var nodeButtons = Buttons.Where(b => TextOf(b).StartsWith("F")).OrderBy(b => TextOf(b), StringComparer.Ordinal).ToArray();
            Assert.AreEqual(18, nodeButtons.Length);
            StringAssert.Contains("HP 80/80", AllText());
            StringAssert.Contains("F1-N1\n전투\n현재", TextOf(nodeButtons[0]));
            StringAssert.Contains("F1-N3\n이벤트", TextOf(nodeButtons[2]));
            StringAssert.Contains("F2-N9\n보스", TextOf(nodeButtons[17]));
            StringAssert.Contains("노드를 눌러 전투에 들어갑니다", AllText());
            yield break;
        }

        [UnityTest]
        public IEnumerator UpcomingNode_DoesNotOpenBattle()
        {
            yield return Press(b => TextOf(b).StartsWith("F1-N2"), "F1-N2");
            Assert.IsNull(GameObject.Find("BattleScreen"), "현재 노드가 아니면 들어가지 않습니다");
            Assert.AreEqual(1, Map.Capture().Node);
        }

        [UnityTest]
        public IEnumerator Victory_Result_ThenMapAdvancesToNode2()
        {
            yield return Press(b => TextOf(b).StartsWith("F1-N1"), "F1-N1");
            Assert.NotNull(GameObject.Find("BattleScreen"));
            Assert.IsFalse(Buttons.Any(b => TextOf(b) == "결과 보기"), "전투 중에는 결과 보기가 없습니다");

            yield return EndBattle(BattleOutcome.Victory);
            yield return Press(b => TextOf(b) == "결과 보기", "결과 보기");
            Assert.IsNull(GameObject.Find("BattleScreen"), "결과 화면으로 넘어가면 전투 화면은 닫힙니다");
            StringAssert.Contains("승리", AllText());
            StringAssert.Contains("F1-N1 전투", AllText());
            StringAssert.Contains("HP 80 → 80 / 80", AllText());
            Assert.AreEqual(1, Map.Capture().Node, "확정하기 전에는 노드가 그대로");
            Assert.IsFalse(Buttons.Any(b => TextOf(b) == "다시 시작"), "일반 승리에는 다시 시작이 없습니다");
            var result = Map.Result();
            Assert.AreEqual(3, result.CardChoices.Length);
            foreach (var id in result.CardChoices)
                Assert.IsTrue(Buttons.Any(b => TextOf(b).Contains(id)), id + " 카드 후보 버튼");
            Assert.IsTrue(Buttons.Any(b => TextOf(b) == "카드 건너뛰고 확정"), "고르지 않으면 건너뛰기로 확정");

            // 두 번째 후보를 고르면 확정 버튼이 그 카드 이름으로 바뀐다
            yield return Press(b => TextOf(b).Contains(result.CardChoices[1]), "두 번째 카드 후보");
            Assert.IsTrue(Buttons.Any(b => TextOf(b).StartsWith("확정: ")));
            Assert.AreEqual(1, Map.Capture().Node, "고르기만 해서는 확정되지 않습니다");
            Assert.AreEqual(0, Map.Capture().Gold);

            yield return Press(b => TextOf(b).StartsWith("확정: "), "확정");
            var snap = Map.Capture();
            Assert.AreEqual(2, snap.Node);
            Assert.AreEqual(11, snap.DeckCount, "고른 카드가 덱에 들어갑니다");
            Assert.AreEqual(result.CardChoices[1], Map.Run.Deck.Last().CardId);
            Assert.AreEqual(result.RewardGold, snap.Gold);
            Assert.IsFalse(Buttons.Any(b => TextOf(b).StartsWith("확정")), "확정한 뒤 보상 화면은 닫힙니다");
            Assert.AreEqual(MapNodeState.Visited, snap.Nodes[0].State);
            StringAssert.Contains("F1-N1\n전투\n완료", Buttons.First(b => TextOf(b).StartsWith("F1-N1")).GetComponentInChildren<Text>().text);
            StringAssert.Contains("F1-N2 전투: 노드를 눌러", AllText());
        }

        [UnityTest]
        public IEnumerator Victory_SkipCard_TakesGoldOnly()
        {
            yield return Press(b => TextOf(b).StartsWith("F1-N1"), "F1-N1");
            yield return EndBattle(BattleOutcome.Victory);
            yield return Press(b => TextOf(b) == "결과 보기", "결과 보기");
            var gold = Map.Result().RewardGold;

            yield return Press(b => TextOf(b) == "카드 건너뛰고 확정", "건너뛰기 확정");
            Assert.AreEqual(2, Map.Capture().Node);
            Assert.AreEqual(10, Map.Capture().DeckCount);
            Assert.AreEqual(gold, Map.Capture().Gold);
        }

        [UnityTest]
        public IEnumerator EliteVictory_RelicMustBePicked_ThenConfirmAdvances()
        {
            // F1-N5 엘리트를 현재 노드로 만들고 맵을 다시 그린다
            Map.Run.StageIndex = 4;
            _view.GetType().GetMethod("RefreshMap", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_view, null);
            yield return null;

            yield return Press(b => TextOf(b).StartsWith("F1-N5"), "F1-N5");
            yield return EndBattle(BattleOutcome.Victory);
            yield return Press(b => TextOf(b) == "결과 보기", "결과 보기");

            StringAssert.Contains("승리", AllText());
            StringAssert.Contains("유물 1개를 고르세요", AllText());
            Assert.IsFalse(Buttons.Any(b => TextOf(b) == "다시 시작"), "유물 선택 대기에서는 재시작 버튼이 없습니다");
            foreach (var id in new[] { "REL_COM_001", "REL_COM_002", "REL_COM_003" })
                Assert.IsTrue(Buttons.Any(b => TextOf(b).StartsWith("유물 ") && TextOf(b).Contains(id)), id + " 유물 후보 버튼");

            var confirm = Buttons.First(b => TextOf(b) == "유물을 고르세요");
            Assert.IsFalse(confirm.interactable, "유물을 고르기 전에는 확정 버튼이 눌리지 않습니다");
            confirm.onClick.Invoke();
            yield return null;
            Assert.AreEqual(5, Map.Capture().Node, "유물을 고르지 않고 확정해도 노드는 그대로");
            Assert.AreEqual(0, Map.Capture().Gold);

            yield return Press(b => TextOf(b).StartsWith("유물 ") && TextOf(b).Contains("REL_COM_002"), "REL_COM_002");
            Assert.IsTrue(Buttons.Any(b => TextOf(b) == "카드 건너뛰고 확정" && b.interactable));
            yield return Press(b => TextOf(b) == "카드 건너뛰고 확정", "확정");

            Assert.AreEqual(6, Map.Capture().Node);
            CollectionAssert.AreEqual(new[] { "REL_BLOOD_CHALICE", "REL_COM_002" }, Map.Run.Relics);
            Assert.AreEqual(10, Map.Capture().DeckCount);
            Assert.AreEqual(RunOutcome.InProgress, Map.Capture().Outcome);
        }

        [UnityTest]
        public IEnumerator Defeat_Result_ThenRestartStartsNewRun()
        {
            yield return Press(b => TextOf(b).StartsWith("F1-N1"), "F1-N1");
            yield return EndBattle(BattleOutcome.Defeat);
            yield return Press(b => TextOf(b) == "결과 보기", "결과 보기");
            StringAssert.Contains("패배", AllText());
            Assert.IsFalse(Buttons.Any(b => TextOf(b).StartsWith("확정") || TextOf(b).Contains("건너뛰고")), "패배하면 보상 선택이 없습니다");
            Assert.AreEqual(RunOutcome.Defeat, Map.Capture().Outcome);

            yield return Press(b => TextOf(b) == "다시 시작", "다시 시작");
            var snap = Map.Capture();
            Assert.AreEqual(RunOutcome.InProgress, snap.Outcome);
            Assert.AreEqual(1, snap.Node);
            Assert.AreEqual(80, snap.Hp);
            StringAssert.Contains("HP 80/80", AllText());
        }
    }
}
