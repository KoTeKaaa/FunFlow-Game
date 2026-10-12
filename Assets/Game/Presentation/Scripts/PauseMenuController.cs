using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FunFlow.Presentation
{
    [DefaultExecutionOrder(-300)]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour runSessionSource, levelSequenceSource;
        [SerializeField] private GameObject pausePanel, settingsPanel;
        [SerializeField] private Button resumeButton, settingsButton, exitButton, settingsBack;
        [SerializeField] private Button restartButton;
        [SerializeField] private Slider musicSlider, effectsSlider;
        [SerializeField] private TMP_Text musicValue, effectsValue;
        private IRunPause pause;
        private ILevelSequence sequence;
        private bool listenerWasPaused;
        private GameObject previousSelection;
        private AudioSource clickSource;
        private AudioClip clickClip;

        private void Awake()
        {
            if (clickSource == null) clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.ignoreListenerPause = true;
            if (clickClip == null) clickClip = ProceduralSfx.Tick();
        }

        private void OnEnable()
        {
            pause = runSessionSource as IRunPause;
            sequence = levelSequenceSource as ILevelSequence;
            if (pause == null || sequence == null) { enabled = false; return; }
            pause.PauseChanged += ShowPause;
            resumeButton.onClick.AddListener(Resume);
            restartButton?.onClick.AddListener(Restart);
            settingsButton.onClick.AddListener(ShowSettings);
            exitButton.onClick.AddListener(Exit);
            settingsBack.onClick.AddListener(Back);
            musicSlider.onValueChanged.AddListener(SetMusic);
            effectsSlider.onValueChanged.AddListener(SetEffects);
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        private void OnDisable()
        {
            if (pause != null) pause.PauseChanged -= ShowPause;
            if (runSessionSource != null && pause != null && pause.IsPaused)
            {
                pause.SetPaused(false);
                AudioListener.pause = listenerWasPaused;
            }
            resumeButton.onClick.RemoveListener(Resume);
            restartButton?.onClick.RemoveListener(Restart);
            settingsButton.onClick.RemoveListener(ShowSettings);
            exitButton.onClick.RemoveListener(Exit);
            settingsBack.onClick.RemoveListener(Back);
            musicSlider.onValueChanged.RemoveListener(SetMusic);
            effectsSlider.onValueChanged.RemoveListener(SetEffects);
            AudioPreferences.Save();
        }

        private void OnDestroy() { if (clickClip != null) Destroy(clickClip); }

        private void Update()
        {
            if (pause != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                pause.SetPaused(!pause.IsPaused);
        }

        public void PlayClick() => clickSource?.PlayOneShot(clickClip, AudioPreferences.EffectsVolume * 0.25f);
        private void Resume() => pause.SetPaused(false);
        private void Restart()
        {
            if (pause.IsPaused && runSessionSource is IRunSession session)
            {
                pause.SetPaused(false);
                session.RestartRun();
            }
        }
        private void Exit() => sequence.ReturnToMainMenu();
        private void Back() { settingsPanel.SetActive(false); pausePanel.SetActive(true); Focus(resumeButton.gameObject); }
        private void ShowPause(bool paused)
        {
            if (paused)
            {
                listenerWasPaused = AudioListener.pause;
                previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                AudioListener.pause = true;
            }
            else
            {
                AudioListener.pause = listenerWasPaused;
                AudioPreferences.Save();
            }
            settingsPanel.SetActive(false);
            pausePanel.SetActive(paused);
            Focus(paused ? resumeButton.gameObject : previousSelection);
        }

        private void ShowSettings()
        {
            musicSlider.SetValueWithoutNotify(AudioPreferences.MusicVolume);
            effectsSlider.SetValueWithoutNotify(AudioPreferences.EffectsVolume);
            RefreshVolumes();
            pausePanel.SetActive(false);
            settingsPanel.SetActive(true);
            Focus(effectsSlider.gameObject);
        }
        private void SetMusic(float value) { AudioPreferences.SetMusic(value); RefreshVolumes(); }
        private void SetEffects(float value)
        {
            AudioPreferences.SetEffects(value);
            GetComponent<PresentationAudio>()?.RefreshVolume();
            RefreshVolumes();
        }
        private void RefreshVolumes()
        {
            musicValue.text = Mathf.RoundToInt(AudioPreferences.MusicVolume * 100) + "%";
            effectsValue.text = Mathf.RoundToInt(AudioPreferences.EffectsVolume * 100) + "%";
        }
        private static void Focus(GameObject target) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target); }
    }
}
