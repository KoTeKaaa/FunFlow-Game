using System.Collections;
using System.Linq;
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
    private bool _started;
    private AudioClip _targetClip;
    private float _activeGain;
    private float _inactiveGain;

    private void Awake()
    {
        if (sourceA == null || sourceB == null || sourceA == sourceB)
        {
            Debug.LogError("MusicController: assign two distinct AudioSources.", this);
            enabled = false;
            return;
        }

        _active = sourceA;
        _inactive = sourceB;

        foreach (var src in new[] { sourceA, sourceB })
        {
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.ignoreListenerPause = true;
            src.spatialBlend = 0f;
            src.Stop();
        }
    }

    private void Start()
    {
        // Scene roots are available in Start. The main menu needs no fake Core.
        bool hasSession = LevelServices.Components(gameObject.scene)
            .Any(c => c.isActiveAndEnabled && c is IRunSession);
        if (runSessionSource != null || hasSession)
        {
            _runSession = LevelServices.Resolve<IRunSession>(this, runSessionSource);
            if (_runSession == null) { enabled = false; return; }
            _runPause = _runSession as IRunPause;
        }
        _started = true;
        OnEnable();
    }

    private void OnEnable()
    {
        if (!_started) return;
        _hasLastState = false;
        if (_runSession != null)
        {
            _runSession.StateChanged += HandleStateChanged;
            HandleStateChanged(_runSession.State);
        }
        else HandleStateChanged(RunState.Waiting);

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
        if (sourceA != null) { sourceA.Stop(); sourceA.volume = 0f; }
        if (sourceB != null) { sourceB.Stop(); sourceB.volume = 0f; }
        _activeGain = _inactiveGain = 0f;
        _targetClip = null;
    }

    private void Update()
    {
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        float volume = AudioPreferences.MusicVolume;
        if (_active != null) _active.volume = _activeGain * volume;
        if (_inactive != null) _inactive.volume = _inactiveGain * volume;
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

        if (_targetClip == next) return;
        _targetClip = next;

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
        // Keep the louder track when interrupting a fade, and retire the other voice.
        if (_inactiveGain > _activeGain)
        {
            (_active, _inactive) = (_inactive, _active);
            (_activeGain, _inactiveGain) = (_inactiveGain, _activeGain);
        }
        _inactive.Stop();
        _inactiveGain = 0f;
        if (next != null && _active.clip == next)
        {
            _activeGain = 1f;
            if (!_active.isPlaying) _active.Play();
            ApplyVolumes();
            return;
        }

        _fadeRoutine = StartCoroutine(CrossFade(next));
    }

    private IEnumerator CrossFade(AudioClip next)
    {
        float startActiveGain = _activeGain;

        if (next != null)
        {
            _inactive.clip = next;
            _inactive.volume = 0f;
            _inactive.Play();
        }

        float t = 0f;
        float duration = float.IsFinite(fadeDuration) ? Mathf.Max(0f, fadeDuration) : 0f;
        // Ensure the coroutine handle is assigned even for an instant transition.
        yield return null;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / duration);

            _activeGain = Mathf.Lerp(startActiveGain, 0f, progress);

            if (next != null)
            {
                _inactiveGain = progress;
            }
            ApplyVolumes();

            yield return null;
        }

        _active.Stop();
        _active.volume = 0f;
        _activeGain = 0f;

        if (next != null)
        {
            (_active, _inactive) = (_inactive, _active);
            _activeGain = 1f;
            _inactiveGain = 0f;
        }

        _fadeRoutine = null;
        ApplyVolumes();
    }
}
