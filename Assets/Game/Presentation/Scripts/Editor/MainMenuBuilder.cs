using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FunFlow.Presentation.EditorTools
{
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/Game/Presentation/Scenes/MainMenu.unity";
        private const string UiPath = "Assets/Game/Presentation/UI/";
        private static Sprite plate, background;
        private static TMP_SpriteAsset icons;

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first");
            if (AssetDatabase.LoadAssetAtPath<Object>(ScenePath) != null) throw new System.InvalidOperationException("MainMenu already exists; edit the existing scene");
            AssetDatabase.ImportAsset(UiPath + "MainMenuBackground.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(UiPath + "MainMenuBackground.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            background = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "MainMenuBackground.png");
            plate = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "HeistPlate.png");
            icons = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(UiPath + "HeistIcons.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(UiPath + "LevelCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                var data = new SerializedObject(catalog);
                var entries = data.FindProperty("levels");
                entries.arraySize = 1;
                var entry = entries.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("Title").stringValue = "Маршрут 1";
                entry.FindPropertyRelative("Description").stringValue = "Собери добычу и доберись до выхода.";
                entry.FindPropertyRelative("ScenePath").stringValue = "Assets/Game/Core/Scenes/Game.unity";
                entry.FindPropertyRelative("Artwork").objectReferenceValue = background;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(catalog, UiPath + "LevelCatalog.asset");
            }
            AssetDatabase.SaveAssets();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("MenuCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HudStyle.Background;
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<AudioListener>();
            var root = new GameObject("MainMenu");
            var controller = root.AddComponent<MainMenuController>();
            var ui = Go("MenuUI", root.transform);
            var canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            ui.AddComponent<GraphicRaycaster>();
            Stretch(ui.transform);
            var art = Go("Background", ui.transform);
            Stretch(art.transform);
            var artImage = art.AddComponent<Image>();
            artImage.sprite = background;
            artImage.raycastTarget = false;
            var aspect = art.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = background.rect.width / background.rect.height;
            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(ui.transform);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var home = Panel("Home", ui.transform, false);
            var levels = Panel("Levels", ui.transform, true);
            var settings = Panel("Settings", ui.transform, true);
            Label("Eyebrow", home.transform, "ПЛАН ОГРАБЛЕНИЯ", 20, HudStyle.Gold, new Vector2(112, 194), new Vector2(640, 40));
            Label("Title", home.transform, "Хватай добычу.\nУспей к выходу!", 72, HudStyle.Text, new Vector2(108, 246), new Vector2(720, 190), true);
            Label("Subtitle", home.transform, "Разные маршруты. Много соблазнов.", 26, HudStyle.Body, new Vector2(112, 444), new Vector2(670, 50));
            var play = Button("Play", home.transform, "Играть  →", new Vector2(112, 548), new Vector2(430, 88), true);
            var options = Button("Settings", home.transform, "Настройки", new Vector2(112, 652), new Vector2(430, 80), false);
            var quit = Button("Quit", home.transform, "Выйти из игры", new Vector2(112, 748), new Vector2(430, 80), false);
            Label("Footer", home.transform, "МЫШЬ — ВЫБОР  •  ENTER — ПОДТВЕРДИТЬ", 18, HudStyle.Muted, new Vector2(112, 968), new Vector2(800, 32));
            Label("LevelHeading", levels.transform, "Выбери уровень музея", 54, HudStyle.Text, new Vector2(96, 116), new Vector2(1500, 80), true);
            var status = Label("Status", levels.transform, "", 24, HudStyle.Body, new Vector2(98, 206), new Vector2(1500, 42));
            var scroll = Go("LevelRibbon", levels.transform);
            Place(scroll.transform, new Vector2(80, 298), new Vector2(1760, 496));
            var sr = (RectTransform)scroll.transform;
            sr.anchorMax = new Vector2(1, 1);
            sr.offsetMin = new Vector2(80, -794);
            sr.offsetMax = new Vector2(-80, -298);
            var ribbon = scroll.AddComponent<LevelRibbon>();
            ribbon.horizontal = true;
            ribbon.vertical = false;
            ribbon.movementType = ScrollRect.MovementType.Clamped;
            ribbon.scrollSensitivity = 40;
            var viewport = Go("Viewport", scroll.transform);
            Stretch(viewport.transform);
            viewport.AddComponent<Image>();
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            var content = Go("Content", viewport.transform);
            var cr = (RectTransform)content.transform;
            cr.anchorMin = new Vector2(0, 0);
            cr.anchorMax = new Vector2(0, 1);
            cr.pivot = new Vector2(0, 0.5f);
            cr.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 18, 18);
            layout.spacing = 24;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            ribbon.viewport = (RectTransform)viewport.transform;
            ribbon.content = cr;
            var rail = Go("RibbonScrollbar", levels.transform);
            Place(rail.transform, new Vector2(96, 832), new Vector2(1728, 12));
            var railRect = (RectTransform)rail.transform;
            railRect.anchorMax = new Vector2(1, 1);
            railRect.offsetMin = new Vector2(96, -844);
            railRect.offsetMax = new Vector2(-96, -832);
            Image(rail, HudStyle.Line, true);
            var grip = Go("Grip", rail.transform); Stretch(grip.transform);
            var gripImage = Image(grip, HudStyle.Gold, true); gripImage.raycastTarget = true;
            var scrollbar = rail.AddComponent<Scrollbar>();
            scrollbar.handleRect = (RectTransform)grip.transform; scrollbar.targetGraphic = gripImage;
            ribbon.horizontalScrollbar = scrollbar;
            ribbon.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var template = Card(content.transform);
            template.gameObject.SetActive(false);
            var backLevels = Button("Back", levels.transform, "← Назад", new Vector2(96, 914), new Vector2(290, 72), false);
            var settingsCard = Go("SettingsCard", settings.transform);
            Place(settingsCard.transform, new Vector2(440, 172), new Vector2(1040, 724));
            var settingsRect = (RectTransform)settingsCard.transform;
            settingsRect.anchorMin = settingsRect.anchorMax = settingsRect.pivot = new Vector2(0.5f, 0.5f);
            settingsRect.anchoredPosition = Vector2.zero;
            Image(settingsCard, HudStyle.Panel, true);
            Label("Title", settingsCard.transform, "Настройки звука", 48, HudStyle.Text, new Vector2(56, 48), new Vector2(910, 68), true);
            Label("Caption", settingsCard.transform, "Настрой громкость под себя", 24, HudStyle.Body, new Vector2(58, 128), new Vector2(900, 40));
            var effects = VolumeRow(settingsCard.transform, "Effects", "Звуки", "Запуск, добыча, ловушки и интерфейс", 224, out var effectsValue);
            var music = VolumeRow(settingsCard.transform, "Music", "Музыка", "Пока без музыки — настройка сохранится", 404, out var musicValue);
            var backSettings = Button("Back", settingsCard.transform, "← Готово", new Vector2(58, 614), new Vector2(300, 72), true);
            Wire(controller, "catalog", AssetDatabase.LoadAssetAtPath<LevelCatalog>(UiPath + "LevelCatalog.asset")); Wire(controller, "menuUi", ui);
            Wire(controller, "home", home); Wire(controller, "levels", levels); Wire(controller, "settings", settings);
            Wire(controller, "playButton", play); Wire(controller, "settingsButton", options); Wire(controller, "quitButton", quit);
            Wire(controller, "levelsBack", backLevels); Wire(controller, "settingsBack", backSettings);
            Wire(controller, "content", cr); Wire(controller, "cardTemplate", template); Wire(controller, "ribbon", ribbon); Wire(controller, "status", status);
            Wire(controller, "musicSlider", music); Wire(controller, "effectsSlider", effects); Wire(controller, "musicValue", musicValue); Wire(controller, "effectsValue", effectsValue);
            levels.SetActive(false); settings.SetActive(false);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
        }

        private static GameObject Go(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }
        private static GameObject Panel(string name, Transform parent, bool dim)
        {
            var go = Go(name, parent); Stretch(go.transform);
            if (dim) Image(go, new Color(0.045f, 0.04f, 0.075f, 0.92f), false);
            go.AddComponent<CanvasGroup>(); go.AddComponent<MenuPanelReveal>();
            return go;
        }
        private static void Stretch(Transform t)
        {
            var r = (RectTransform)t; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }
        private static void Place(Transform t, Vector2 position, Vector2 size)
        {
            var r = (RectTransform)t; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(position.x, -position.y); r.sizeDelta = size;
        }
        private static Image Image(GameObject go, Color color, bool sliced)
        {
            var image = go.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (sliced) { image.sprite = plate; image.type = UnityEngine.UI.Image.Type.Sliced; }
            return image;
        }
        private static TextMeshProUGUI Label(string name, Transform parent, string value, float size, Color color, Vector2 position, Vector2 bounds, bool bold = false)
        {
            var go = Go(name, parent); Place(go.transform, position, bounds);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Presentation/UI/RussianUi SDF.asset");
            text.spriteAsset = icons;
            text.text = value; text.fontSize = size; text.color = color; text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            ButtonLabels.Set(text, value);
            return text;
        }
        private static Button Button(string name, Transform parent, string value, Vector2 position, Vector2 bounds, bool primary)
        {
            var go = Go(name, parent); Place(go.transform, position, bounds);
            var image = Image(go, primary ? HudStyle.Gold : HudStyle.Panel, true); image.raycastTarget = true;
            var button = go.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1, 0.9f, 0.75f); colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(0.7f, 0.6f, 0.48f); button.colors = colors;
            var label = Label("Label", go.transform, value, 28, primary ? HudStyle.Dark : HudStyle.Text, Vector2.zero, bounds, true);
            Stretch(label.transform); label.alignment = TextAlignmentOptions.Center;
            go.AddComponent<MenuButtonFeedback>();
            return button;
        }
        private static LevelCardView Card(Transform parent)
        {
            var button = Button("CardTemplate", parent, "", Vector2.zero, new Vector2(340, 440), false);
            Object.DestroyImmediate(button.transform.Find("Label").gameObject);
            var size = button.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = 340; size.preferredHeight = 440;
            var number = Label("Number", button.transform, "", 20, HudStyle.Gold, new Vector2(24, 22), new Vector2(290, 28), true);
            var thumb = Go("Thumbnail", button.transform); Place(thumb.transform, new Vector2(24, 64), new Vector2(292, 160)); thumb.AddComponent<Image>(); thumb.AddComponent<Mask>().showMaskGraphic = false;
            var art = Go("Artwork", thumb.transform); Stretch(art.transform); var image = Image(art, Color.white, false); image.sprite = background;
            var aspect = art.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio = background.rect.width / background.rect.height;
            var title = Label("Title", button.transform, "", 34, HudStyle.Text, new Vector2(24, 246), new Vector2(292, 48), true);
            var description = Label("Description", button.transform, "", 22, HudStyle.Body, new Vector2(24, 304), new Vector2(292, 64));
            var action = Label("Action", button.transform, "", 24, HudStyle.Gold, new Vector2(24, 388), new Vector2(292, 40), true);
            var view = button.gameObject.AddComponent<LevelCardView>();
            Wire(view, "number", number); Wire(view, "title", title); Wire(view, "description", description); Wire(view, "action", action); Wire(view, "artwork", image); Wire(view, "button", button);
            return view;
        }
        private static Slider VolumeRow(Transform parent, string name, string caption, string hint, float y, out TMP_Text value)
        {
            Label(name + "Label", parent, caption, 30, HudStyle.Text, new Vector2(58, y), new Vector2(690, 44), true);
            value = Label(name + "Value", parent, "100%", 28, HudStyle.Gold, new Vector2(830, y), new Vector2(150, 44), true);
            Label(name + "Hint", parent, hint, 20, HudStyle.Muted, new Vector2(58, y + 48), new Vector2(900, 32));
            var go = Go(name + "Slider", parent); Place(go.transform, new Vector2(58, y + 94), new Vector2(920, 32));
            Image(go, HudStyle.Line, true).raycastTarget = true;
            var fillArea = Go("FillArea", go.transform); Stretch(fillArea.transform);
            var fill = Go("Fill", fillArea.transform); Stretch(fill.transform); var fillImage = Image(fill, HudStyle.Gold, true);
            var handleArea = Go("HandleArea", go.transform); Stretch(handleArea.transform);
            var handle = Go("Handle", handleArea.transform); var hr = (RectTransform)handle.transform; hr.sizeDelta = new Vector2(28, 48);
            var handleImage = Image(handle, HudStyle.Text, true); handleImage.raycastTarget = true;
            var slider = go.AddComponent<Slider>(); slider.fillRect = (RectTransform)fill.transform; slider.handleRect = hr; slider.targetGraphic = handleImage; slider.value = 1;
            return slider;
        }
        private static void Wire(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); var property = data.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException(field);
            property.objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
