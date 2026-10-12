#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

internal static class PlayModeStartup
{
    private const string AppliedKey = "Game.Core.PlayModeStartup.Applied";
    private const string MenuPath = "Assets/Game/Presentation/Scenes/MainMenu.unity";

    [InitializeOnLoadMethod]
    private static void Initialize() => EditorApplication.delayCall += ApplyOnce;

    private static void ApplyOnce()
    {
        // Apply once per Editor launch, preserving test overrides across domain reloads.
        if (SessionState.GetBool(AppliedKey, false)) return;
        var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
        if (menu == null) return;
        EditorSceneManager.playModeStartScene = menu;
        SessionState.SetBool(AppliedKey, true);
    }
}
#endif
