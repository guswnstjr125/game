using System;
using System.Collections;
using System.IO;
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
    /// 휴식 화면 (`37` §2.4, `32` REST-01)의 Unity PlayMode 스모크. 화면은 Assembly-CSharp에 있어 리플렉션으로 만든다.
    /// 규칙 값(회복 floor(80 × 0.3) = 24, 확정 전 불변, 중복 방지)은 EditMode `RestScreenSessionTests`가 본다.
    /// 여기서는 화면이 그 세션을 그대로 보여 주고 버튼이 세션에 닿는지만 본다. 실제 클릭·배치·글자는 batchmode에서 볼 수 없다.
    /// </summary>
    public sealed class RestScreenPlayModeTests
    {
        GameObject _root;
        Component _view;
        Type _viewType;

        [SetUp]
        public void SetUp()
        {
            _viewType = Type.GetType("DarkLegion.UI.RestScreenView, Assembly-CSharp");
            Assert.NotNull(_viewType, "RestScreenView를 찾지 못했습니다");
            _root = new GameObject("RestScreen");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            foreach (var name in new[] { "RestCanvas", "EventSystem" })
            {
                var go = GameObject.Find(name);
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static GameDataLoadResult Loaded()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DesignData"));
            var texts = Directory.GetFiles(dir, "*.json").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => File.ReadAllText(f));
            return GameDataLoader.Load(GameData.FromJson(texts));
        }

        IEnumerator OpenStandalone(int hp)
        {
            _view = _root.AddComponent(_viewType);
            _viewType.GetField("StandaloneHp").SetValue(_view, hp);
            yield return null;
            yield return null;
        }

        RestScreenSession Session => (RestScreenSession)_viewType.GetProperty("Session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_view);

        static Button Named(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);

        static string AllText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text));

        IEnumerator Press(string name)
        {
            var button = Named(name);
            Assert.NotNull(button, name + " 버튼이 없습니다");
            button.onClick.Invoke();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Standalone_ShowsHealAndUpgradeOptions_WithPreview()
        {
            yield return OpenStandalone(50);

            Assert.NotNull(Session, AllText());
            StringAssert.Contains("HP 50/80", AllText());
            StringAssert.Contains("HP 50 → 74 (+24)", AllText(), "50 + floor(80 × 0.3) = 74");
            Assert.IsTrue(Named("Heal").interactable);
            Assert.IsTrue(Named("Upgrade").interactable);
            Assert.AreEqual(50, Session.Capture().Hp, "화면을 여는 것만으로는 런이 바뀌지 않는다");
        }

        [UnityTest]
        public IEnumerator Heal_PreviewCancelConfirm_AppliesOnce()
        {
            yield return OpenStandalone(50);

            yield return Press("Heal");
            StringAssert.Contains("HP 50 → 74 / 80", AllText());
            Assert.AreEqual(50, Session.Capture().Hp, "확정 전에는 HP가 그대로");
            yield return Press("Cancel");
            Assert.AreEqual(RestScreenPhase.Choosing, Session.Capture().Phase);
            Assert.AreEqual(50, Session.Capture().Hp);

            yield return Press("Heal");
            yield return Press("Confirm");
            StringAssert.Contains("회복했습니다", AllText());
            Assert.AreEqual(74, Session.Capture().Hp);
            Assert.IsNull(Named("Confirm"), "확정한 뒤에는 확정·선택 버튼이 없다");
            Assert.IsNull(Named("Heal"));
            Assert.IsFalse(Session.Confirm(), "중복 확정 없음");
            Assert.AreEqual(74, Session.Capture().Hp);

            yield return Press("Continue");
            Assert.IsTrue(Session.Completed);
            Assert.IsFalse(Named("Continue").interactable, "[계속]은 한 번만");
        }

        [UnityTest]
        public IEnumerator Upgrade_PickCompareConfirm_UpgradesChosenInstance()
        {
            yield return OpenStandalone(80);

            yield return Press("Upgrade");
            var candidates = Session.Capture().UpgradeCandidates;
            Assert.Greater(candidates.Length, 0);
            Assert.IsFalse(Named("Confirm").interactable, "카드를 고르기 전에는 확인이 눌리지 않는다");

            int pick = candidates[0].InstanceId;
            yield return Press("Card" + pick);
            StringAssert.Contains("강화 전:", AllText());
            StringAssert.Contains("강화 후:", AllText());
            Assert.IsTrue(Named("Confirm").interactable);
            Assert.AreEqual(pick, Session.Capture().SelectedUpgrade);

            yield return Press("Confirm");
            StringAssert.Contains("강화했습니다", AllText());
            Assert.AreEqual(pick, Session.Result.UpgradedInstanceId);
            Assert.IsNull(Named("Confirm"));
            Assert.IsFalse(Session.Confirm());
        }

        [UnityTest]
        public IEnumerator Attached_ContinueOnly_ShowsOnlyContinue_AndCompletesOnce()
        {
            var data = Loaded();
            var run = RunFlow.NewRun("RUN-1", 1L, "NEC", BattleScreenSession.StartHp, BattleScreenSession.NecStartDeck,
                new[] { BattleScreenSession.StartRelicId }, false);
            run.StageIndex = 7;
            foreach (var card in run.Deck.Where(c => RunDeckRules.IsUpgradable(c, data.Cards))) card.Upgraded = true;
            int calls = 0;
            var session = new RestScreenSession(run, data, r => calls++);

            _view = _root.AddComponent(_viewType);
            _viewType.GetMethod("Attach", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_view, new object[] { session, null });
            yield return null;
            yield return null;

            StringAssert.Contains("회복할 HP도 강화할 카드도 없습니다", AllText());
            Assert.IsNull(Named("Heal"));
            Assert.IsNull(Named("Upgrade"));
            yield return Press("Continue");
            Assert.AreEqual(1, calls, "[계속]은 노드 완료 콜백을 한 번 부른다");
            Assert.AreEqual(80, run.CurrentHp);
            Assert.AreEqual(8, run.Node, "노드 완료는 맵이 한다");
        }
    }
}
