using UnityEngine;

[CreateAssetMenu(fileName = "LevelSequence", menuName = "Game/Core/Level Sequence")]
public sealed class LevelSequenceConfig : ScriptableObject
{
    // Saved scenes in play order; each must be enabled in Build Settings.
    [SerializeField] private string[] _scenePaths = new string[0];

    public int Count => _scenePaths != null ? _scenePaths.Length : 0;
    public string GetScenePath(int index) => _scenePaths[index];

    public int IndexOf(string scenePath)
    {
        for (int i = 0; i < Count; i++)
            if (_scenePaths[i] == scenePath)
                return i;
        return -1;
    }
}
