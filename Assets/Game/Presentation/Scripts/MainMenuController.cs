using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FunFlow.Presentation
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private GameObject menuUi;
        [SerializeField] private GameObject home;
        [SerializeField] private GameObject levels;
        [SerializeField] private GameObject settings;
        [SerializeField] private Button playButton, settingsButton, quitButton, levelsBack, settingsBack;
        [SerializeField] private RectTransform content;
        [SerializeField] private LevelCardView cardTemplate;
        [SerializeField] private ScrollRect ribbon;
        [SerializeField] private TMP_Text status, musicValue, effectsValue;
        [SerializeField] private Slider musicSlider, effectsSlider;
        private readonly List<LevelCardView> cards = new List<LevelCardView>();
        private bool loading;
        private string loadingScenePath;
        private AudioSource clickSource;
        private AudioClip clickClip;

        private void Awake()
        {
            if (clickSource == null) clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 0;
            if (clickClip == null) clickClip = ProceduralSfx.Tick();
        }

        public void PlayClick()
        {
            if (clickSource != null && clickClip != null)
                clickSource.PlayOneShot(clickClip, AudioPreferences.EffectsVolume * 0.25f);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= LevelLoaded;
            if (clickClip != null) Destroy(clickClip);
        }

        private void Update()
        {
            if (!loading && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                ShowHome();
        }

        private void OnEnable()
        {
            playButton.onClick.AddListener(ShowLevels);
            settingsButton.onClick.AddListener(ShowSettings);
            quitButton.onClick.AddListener(Quit);
            levelsBack.onClick.AddListener(ShowHome);
            settingsBack.onClick.AddListener(ShowHome);
            musicSlider.onValueChanged.AddListener(SetMusic);
            effectsSlider.onValueChanged.AddListener(SetEffects);
            musicSlider.SetValueWithoutNotify(AudioPreferences.MusicVolume);
            effectsSlider.SetValueWithoutNotify(AudioPreferences.EffectsVolume);
            RefreshVolumes();
            ShowHome();
        }

        private void OnDisable()
        {
            playButton.onClick.RemoveListener(ShowLevels);
            settingsButton.onClick.RemoveListener(ShowSettings);
            quitButton.onClick.RemoveListener(Quit);
            levelsBack.onClick.RemoveListener(ShowHome);
            settingsBack.onClick.RemoveListener(ShowHome);
            musicSlider.onValueChanged.RemoveListener(SetMusic);
            effectsSlider.onValueChanged.RemoveListener(SetEffects);
            AudioPreferences.Save();
        }

        private void ShowHome() { Show(home); Focus(playButton.gameObject); }
        private void ShowSettings() { Show(settings); Focus(effectsSlider.gameObject); }
        private void Focus(GameObject target)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target);
        }
        private void Show(GameObject panel)
        {
            home.SetActive(panel == home);
            levels.SetActive(panel == levels);
            settings.SetActive(panel == settings);
        }

        private void ShowLevels()
        {
            Show(levels);
            if (cards.Count == 0 && catalog != null)
            {
                for (int i = 0; i < catalog.Levels.Count; i++)
                {
                    var entry = catalog.Levels[i];
                    if (entry == null) continue;
                    var card = Instantiate(cardTemplate, content);
                    card.name = "Level " + (i + 1);
                    card.Bind(entry, i, () => Load(entry));
                    card.gameObject.SetActive(true);
                    cards.Add(card);
                }
            }
            status.text = cards.Count == 0 ? "Уровни пока не добавлены" : "Выбери уровень • прокручивай ленту мышью";
            Canvas.ForceUpdateCanvases();
            ribbon.horizontalNormalizedPosition = 0;
            var first = cards.Find(card => card.Button.interactable);
            Focus(first != null ? first.gameObject : levelsBack.gameObject);
        }

        private void SetMusic(float value) { AudioPreferences.SetMusic(value); RefreshVolumes(); }
        private void SetEffects(float value) { AudioPreferences.SetEffects(value); RefreshVolumes(); }
        private void RefreshVolumes()
        {
            musicValue.text = Mathf.RoundToInt(AudioPreferences.MusicVolume * 100) + "%";
            effectsValue.text = Mathf.RoundToInt(AudioPreferences.EffectsVolume * 100) + "%";
        }

        private void Load(LevelCatalog.Entry entry)
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(entry.ScenePath)) { status.text = "Сцена уровня недоступна"; return; }
            loading = true;
            loadingScenePath = entry.ScenePath;
            AudioPreferences.Save();
            menuUi.SetActive(false);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += LevelLoaded;
            SceneManager.LoadSceneAsync(entry.ScenePath, LoadSceneMode.Single);
        }

        private void LevelLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != loadingScenePath) return;
            SceneManager.sceneLoaded -= LevelLoaded;
            // sceneLoaded приходит после Awake/OnEnable: источники и подписки HUD уже готовы.
            bool started = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var source in root.GetComponentsInChildren<MonoBehaviour>())
                {
                    if (source is IRunSession session) { session.StartRun(); started = true; break; }
                }
                if (started) break;
            }
            if (!started) Debug.LogError($"В уровне {scene.path} не найден IRunSession.");
            Destroy(gameObject);
        }

        private void Quit()
        {
            AudioPreferences.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
