using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkLegion.UI
{
    /// <summary>씬 파일 없이 1층 노드 1 전투 화면을 연다. 스크립트 .meta는 Unity가 만든다.</summary>
    public static class BattleScreenMenu
    {
        [MenuItem("DarkLegion/1층 노드 1 전투")]
        public static void PlayFloor1Node1()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BattleScreen").AddComponent<BattleScreenView>();
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }
    }
}
