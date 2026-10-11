using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Links the level scenes into one session. The attempt itself stays in RunSession:
// Restart reloads the current scene there, this component only moves between scenes.
[DisallowMultipleComponent]
public sealed class LevelSequence : MonoBehaviour, ILevelSequence
{
    [SerializeField] private RunSession _session;
    [SerializeField] private LevelSequenceConfig _config;

    // Survives scene loads within one Play session; reset on every Play Mode entry.
    private static int[] s_levelValues = new int[0];

    private int _index = -1;
    private bool _loading;

    public int LevelNumber => _index + 1;
    public int LevelCount => _config != null ? _config.Count : 0;
    public bool HasNextLevel => _index >= 0 && _index + 1 < LevelCount;

    public int TotalValue
    {
        get
        {
            int total = 0;
            for (int i = 0; i < s_levelValues.Length; i++)
                total += s_levelValues[i];
            return total;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => s_levelValues = new int[0];

    public int GetLevelValue(int levelNumber) =>
        levelNumber >= 1 && levelNumber <= s_levelValues.Length ? s_levelValues[levelNumber - 1] : 0;

    private void OnEnable()
    {
        _index = _config != null ? _config.IndexOf(gameObject.scene.path) : -1;
        if (_session == null || _index < 0)
        {
            Debug.LogError("LevelSequence: assign Session and a Level Sequence config that lists this scene.", this);
            enabled = false;
            return;
        }

        if (s_levelValues.Length != LevelCount)
            Array.Resize(ref s_levelValues, LevelCount);
        _session.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_session != null)
            _session.StateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(RunState state)
    {
        if (state == RunState.Won && _session.Result.HasValue)
            s_levelValues[_index] = _session.Result.Value.FinalValue;
    }

    [ContextMenu("Test Run/Next Level")]
    public void LoadNextLevel()
    {
        if (isActiveAndEnabled && HasNextLevel && _session.State == RunState.Won)
            Load(_index + 1);
    }

    [ContextMenu("Test Run/First Level")]
    public void RestartFromFirstLevel()
    {
        if (!isActiveAndEnabled || LevelCount == 0)
            return;

        s_levelValues = new int[LevelCount];
        Load(0);
    }

    private void Load(int index)
    {
        if (!Application.isPlaying || _loading)
            return;

        string path = _config.GetScenePath(index);
        if (!Application.CanStreamedLevelBeLoaded(path))
        {
            Debug.LogError($"LevelSequence: scene '{path}' must be saved and enabled in Build Settings.", this);
            return;
        }

        _loading = true;
        SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
    }
}
