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
    /// 이벤트 선택 화면 (`37` §2.5)의 Unity PlayMode 스모크. 화면은 Assembly-CSharp에 있어 리플렉션으로 만든다.
    /// 규칙 값은 `EventScreenSessionTests`(EditMode)가 보고, 여기서는 단독 화면의 표시·버튼·전환을 본다.
    /// 기대값은 NEC 시작 값(HP 80)과 `40` 문구, `06` Events_MVP에서 손으로 적었다. 글자 겹침·마우스는 batchmode에서 볼 수 없다.
    /// </summary>
    public sealed class EventScreenPlayModeTests
    {
        GameObject _root;
        Component _view;

        IEnumerator Open(string eventId, int floor = 1)
        {
            var type = Type.GetType("DarkLegion.UI.EventScreenView, Assembly-CSharp");
            Assert.NotNull(type, "EventScreenView를 찾지 못했습니다");
            _root = new GameObject("EventScreen");
            _view = _root.AddComponent(type);
            type.GetField("StandaloneFloor").SetValue(_view, floor);
            type.GetField("StandaloneEventId").SetValue(_view, eventId);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var name in new[] { "EventCanvas", "EventSystem" })
            {
                var go = GameObject.Find(name);
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        EventScreenSession Session => (EventScreenSession)_view.GetType().GetProperty("Session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_view);

        RunState Run => (RunState)typeof(EventScreenSession).GetField("_run", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Session);

        static string AllText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text));

        static Button ButtonNamed(string name)
        {
            var go = GameObject.Find(name);
            return go == null ? null : go.GetComponent<Button>();
        }

        IEnumerator Press(string name)
        {
            var button = ButtonNamed(name);
            Assert.NotNull(button, name + " 버튼이 없습니다");
            Assert.IsTrue(button.interactable, name + " 버튼이 잠겨 있습니다");
            button.onClick.Invoke();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Opens_ShowingTitleIntroStatusAndThreeOptions()
        {
            yield return Open("EVT_FORGOTTEN_SHRINE");

            string text = AllText();
            StringAssert.Contains("1층 · 잊힌 제단", text);
            StringAssert.Contains("HP 80/80", text);
            StringAssert.Contains("골드 0", text);
            StringAssert.Contains("이름이 지워진 신의 제단이다.", text);
            StringAssert.Contains("1. 무릎을 꿇는다 (HP +15)", text);
            StringAssert.Contains("2. 몬스터를 바친다", text);
            StringAssert.Contains("3. 떠난다", text);
            foreach (var n in new[] { "Option1", "Option2", "Option3" }) Assert.IsTrue(ButtonNamed(n).interactable, n);
        }

        [UnityTest]
        public IEnumerator DisabledOption_IsDimmedWithCondition()
        {
            yield return Open("EVT_WANDER_MERCHANT");

            Assert.IsFalse(ButtonNamed("Option1").interactable, "골드 0 < 8");
            Assert.IsTrue(ButtonNamed("Option2").interactable);
            StringAssert.Contains("(골드 8 이상)", AllText());
            ButtonNamed("Option1").onClick.Invoke();
            yield return null;
            Assert.AreEqual(EventScreenPhase.Choosing, Session.Phase);
        }

        [UnityTest]
        public IEnumerator Decline_ShowsResultThenContinueCompletes()
        {
            yield return Open("EVT_FORGOTTEN_SHRINE");
            yield return Press("Option3");

            Assert.AreEqual(EventScreenPhase.Result, Session.Phase);
            StringAssert.Contains("촛대는 다시 어둠에 묻힌다.", AllText());
            Assert.IsNull(GameObject.Find("Option1"), "선택지는 사라진다");
            yield return Press("Continue");
            Assert.IsTrue(Session.Completed);
            Assert.IsFalse(ButtonNamed("Continue").interactable, "두 번 누를 수 없다");
        }

        [UnityTest]
        public IEnumerator Heal_ShowsHpChangeInResult()
        {
            yield return Open("EVT_FORGOTTEN_SHRINE");
            Run.CurrentHp = 60;
            yield return Press("Option1");

            string text = AllText();
            StringAssert.Contains("HP +15 (75/80)", text);
            StringAssert.Contains("오래전 버린 기도가 입에서 흘러나온다.", text);
            StringAssert.Contains("HP 75/80", text);
        }

        [UnityTest]
        public IEnumerator BloodAltar_PickUpgradeThenConfirm_ShowsCostAndUpgrade()
        {
            yield return Open("EVT_BLOOD_ALTAR");
            yield return Press("Option1");

            Assert.AreEqual(EventScreenPhase.Picking, Session.Phase);
            StringAssert.Contains("강화할 카드", AllText());
            Assert.IsFalse(ButtonNamed("Confirm").interactable, "카드를 고르기 전");
            var target = Run.Deck.First(c => c.CardId == "NEC_SKL_003");
            yield return Press("Upgrade" + target.InstanceId);
            Assert.IsTrue(ButtonNamed("Confirm").interactable);
            Assert.AreEqual(80, Run.CurrentHp, "확정 전에는 HP가 그대로");
            yield return Press("Confirm");

            Assert.AreEqual(EventScreenPhase.Result, Session.Phase);
            string text = AllText();
            StringAssert.Contains("HP -20 (60/80)", text);
            StringAssert.Contains("강화 ", text);
            StringAssert.Contains("피가 홈을 채우자 덱의 카드 하나가 붉게 달아오른다.", text);
        }

        [UnityTest]
        public IEnumerator Back_ReturnsToOptionsWithoutChange()
        {
            yield return Open("EVT_BLOOD_ALTAR");
            yield return Press("Option1");
            yield return Press("Back");

            Assert.AreEqual(EventScreenPhase.Choosing, Session.Phase);
            Assert.AreEqual(80, Run.CurrentHp);
            Assert.NotNull(ButtonNamed("Option1"));
        }

        [UnityTest]
        public IEnumerator DevilPact_RelicChoice_RequiresPick()
        {
            yield return Open("EVT_DEVIL_PACT");
            yield return Press("Option2");

            Assert.IsFalse(ButtonNamed("Confirm").interactable, "유물을 고르기 전");
            Assert.NotNull(ButtonNamed("Relic0"));
            Assert.NotNull(ButtonNamed("Relic2"));
            yield return Press("Relic2");
            yield return Press("Confirm");

            Assert.AreEqual(EventScreenPhase.Result, Session.Phase);
            StringAssert.Contains("유물 피 묻은 장갑", AllText(), "`06` Relics_MVP REL_COM_003 이름");
            CollectionAssert.Contains(Run.Relics, "REL_COM_003");
        }
    }
}
