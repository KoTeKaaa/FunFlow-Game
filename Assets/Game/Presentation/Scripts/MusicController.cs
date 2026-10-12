using System.Collections;
using UnityEngine;
using FunFlow.Presentation;

public class MusicController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private MonoBehaviour runSessionSource;

    [Header("Audio Sources (без AudioListener!)")]
    [SerializeField] private AudioSource sourceA;
    [SerializeField] private AudioSource sourceB;

    [Header("Clips")]
    [SerializeField] private AudioClip lobbyMusic;
    [SerializeField] private AudioClip levelMusic;
    [SerializeField] private AudioClip alarmMusic;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 1.5f;

    private IRunSession _runSession;
    private IRunPause _runPause;

    private AudioSource _active;
    private AudioSource _inactive;
    private Coroutine _fadeRoutine;

    private RunState _lastState;
    private bool _hasLastState;

    private void Awake()
    {
        _runSession = runSessionSource as IRunSession;
        _runPause = runSessionSource as IRunPause;

        if (_runSession == null)
        {
            Debug.LogError("MusicController: runSessionSource не реализует IRunSession");
            enabled = false;
            return;
        }

        if (_runPause == null)
        {
            Debug.LogWarning("MusicController: runSessionSource не реализует IRunPause.");
        }

        _active = sourceA;
        _inactive = sourceB;

        foreach (var src in new[] { sourceA, sourceB })
        {
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.ignoreListenerPause = true;
        }
    }

    private void OnEnable()
    {
        if (_runSession != null)
        {
            _runSession.StateChanged += HandleStateChanged;
            _hasLastState = false;
            HandleStateChanged(_runSession.State);
        }

        if (_runPause != null)
            _runPause.PauseChanged += HandlePauseChanged;
    }

    private void OnDisable()
    {
        if (_runSession != null)
            _runSession.StateChanged -= HandleStateChanged;

        if (_runPause != null)
            _runPause.PauseChanged -= HandlePauseChanged;

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    private void Update()
    {
        if (_active != null && _active.isPlaying && _fadeRoutine == null)
            _active.volume = AudioPreferences.MusicVolume;
    }

    private void HandlePauseChanged(bool isPaused)
    {
        // RunState не меняется на паузе — музыка продолжает играть как есть.
    }

    private void HandleStateChanged(RunState state)
    {
        if (_hasLastState && state == _lastState) return;
        _hasLastState = true;
        _lastState = state;

        AudioClip next = state switch
        {
            RunState.Waiting => lobbyMusic,
            RunState.Playing => levelMusic,
            RunState.Alarm => alarmMusic,
            RunState.Won => null,
            RunState.Lost => null,
            _ => null
        };

        if (_active.clip == next) return;

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(CrossFade(next));
    }

    private IEnumerator CrossFade(AudioClip next)
    {
        float startActiveVolume = _active.volume;

        if (next != null)
        {
            _inactive.clip = next;
            _inactive.volume = 0f;
            _inactive.Play();
        }

        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / fadeDuration;

            _active.volume = Mathf.Lerp(startActiveVolume, 0f, progress);

            if (next != null)
            {
                float targetVolume = AudioPreferences.MusicVolume;
                _inactive.volume = Mathf.Lerp(0f, targetVolume, progress);
            }

            yield return null;
        }

        _active.Stop();
        _active.volume = 0f;

        if (next != null)
        {
            (_active, _inactive) = (_inactive, _active);
        }

        _fadeRoutine = null;
    }
}