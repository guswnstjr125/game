using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkLegion.UI
{
    /// <summary>씬 파일 없이 휴식 화면을 단독으로 연다 (1층 F1-N8, HP 50/80). 스크립트 .meta는 Unity가 만든다.</summary>
    public static class RestScreenMenu
    {
        [MenuItem("DarkLegion/휴식 화면")]
        public static void PlayRestScreen()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("RestScreen").AddComponent<RestScreenView>();
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }
    }
}
