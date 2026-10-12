using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-local lookup after scene loading; no global cache survives a scene reload.
public static class LevelServices
{
    public static MonoBehaviour[] Components(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
        .Where(component => component != null).ToArray();

    public static T Resolve<T>(Component owner, MonoBehaviour assigned = null) where T : class
    {
        if (assigned != null && assigned.isActiveAndEnabled
            && assigned.gameObject.scene == owner.gameObject.scene && assigned is T service)
            return service;

        var sources = Components(owner.gameObject.scene)
            .Where(component => component.isActiveAndEnabled && component is T).ToArray();
        if (sources.Length == 1)
            return sources[0] as T;

        Debug.LogError($"{owner.name}: expected one active {typeof(T).Name} in this scene; found {sources.Length}.", owner);
        return null;
    }

    public static Transform FindExit(Scene scene)
    {
        var exits = Components(scene).Where(component => component.isActiveAndEnabled && component is ILevelExit)
            .Select(component => component.transform).Distinct().ToArray();
        return exits.Length == 1 ? exits[0] : null;
    }
}
