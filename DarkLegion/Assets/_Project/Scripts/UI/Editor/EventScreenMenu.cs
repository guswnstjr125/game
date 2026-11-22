using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkLegion.UI
{
    /// <summary>씬 파일 없이 이벤트 화면을 단독으로 연다 (1층, 이벤트는 노드 시드가 고른다). 스크립트 .meta는 Unity가 만든다.</summary>
    public static class EventScreenMenu
    {
        [MenuItem("DarkLegion/이벤트 화면")]
        public static void PlayEventScreen()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("EventScreen").AddComponent<EventScreenView>();
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }
    }
}
