using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkLegion.UI
{
    /// <summary>씬 파일 없이 18노드 맵 → 전투 → 결과 흐름을 연다. 스크립트 .meta는 Unity가 만든다.</summary>
    public static class RunMapMenu
    {
        [MenuItem("DarkLegion/18노드 맵")]
        public static void PlayRunMap()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("RunMap").AddComponent<RunMapView>();
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }
    }
}
