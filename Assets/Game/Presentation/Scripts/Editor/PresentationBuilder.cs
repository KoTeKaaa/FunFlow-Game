using FunFlow.Presentation.Sandbox;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace FunFlow.Presentation.EditorTools
{
    /// <summary>
    /// Меню FunFlow → Presentation: собирает HUD.prefab и PresentationSandbox.unity,
    /// чтобы не строить Canvas вручную. Общую сцену Game.unity не трогает.
    /// </summary>
    public static class PresentationBuilder
    {
        private const string Root = "Assets/Game/Presentation";
        private const string PrefabPath = Root + "/Prefabs/HUD.prefab";
        private const string ScenePath = Root + "/Scenes/PresentationSandbox.unity";
        private const string ConfigPath = "Assets/Game/Core/Config/GameConfig.asset";
        private const int UiLayer = 5;

        private static Sprite roundSprite;

        [MenuItem("FunFlow/Presentation/Собрать HUD и PresentationSandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("FunFlow", "Сначала выйди из Play Mode.", "OK");
                return;
            }

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("FunFlow",
                    "Нет ресурсов TextMeshPro.\nWindow → TextMeshPro → Import TMP Essential Resources, затем запусти сборку снова.",
                    "OK");
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath) != null ||
                          AssetDatabase.LoadAssetAtPath<Object>(ScenePath) != null;
            if (exists && !EditorUtility.DisplayDialog("FunFlow",
                    "HUD.prefab или PresentationSandbox.unity уже существуют.\n" +
                    "Пересобрать? Ручные правки в них пропадут, GUID сохранится.",
                    "Пересобрать", "Отмена"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Scenes");
            roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Transform thief = BuildSandboxWorld();

            GameObject hud = BuildHud();
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(hud, PrefabPath, InteractionMode.AutomatedAction);

            // Поддельные Core/Loot/Player по контрактам — только в песочнице
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) Debug.LogWarning($"FunFlow: {ConfigPath} не найден — «% дальности» не будет показан.");

            var fakeGo = new GameObject("SandboxFakeGame (только песочница)");
            var fake = fakeGo.AddComponent<SandboxFakeGame>();
            Wire(fake, "thief", thief);
            Wire(fake, "config", config);

            var routeStart = new GameObject("RouteStart").transform;
            routeStart.position = new Vector3(-8f, 0f, 0f);
            var routeExit = new GameObject("RouteExit").transform;
            routeExit.position = new Vector3(8f, 0f, 0f);

            // Ссылки на экземпляре HUD в сцене — так же №1 сделает в Game.unity
            var binder = hud.GetComponent<PresentationBinder>();
            Wire(binder, "runSessionSource", fake);
            Wire(binder, "lootSource", fake);
            Wire(binder, "playerSource", fake);
            Wire(binder, "playerTransform", thief);
            Wire(binder, "config", config);
            Wire(binder, "routeStart", routeStart);
            Wire(binder, "routeExit", routeExit);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"FunFlow: собраны {PrefabPath} и {ScenePath}. Нажми Play.");
        }

        // ---------------- Мир песочницы ----------------

        private static Transform BuildSandboxWorld()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(new Vector3(0f, 9f, -11f), Quaternion.Euler(40f, 0f, 0f));
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = HudStyle.Background;
            }

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2.2f, 1f, 0.8f);

            GameObject thief = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            thief.name = "FakeThief";
            thief.transform.position = new Vector3(-8f, 0.4f, 0f);
            thief.transform.localScale = Vector3.one * 0.8f;
            return thief.transform;
        }

        // ---------------- HUD ----------------

        private static GameObject BuildHud()
        {
            GameObject root = Ui("HUD", null);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Красная рамка тревоги
            GameObject border = Ui("AlarmBorder", root.transform);
            Stretch(border);
            CanvasGroup borderGroup = Group(border, 0f);
            Edge(border.transform, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 10));
            Edge(border.transform, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 10));
            Edge(border.transform, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(10, 0));
            Edge(border.transform, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(10, 0));

            // Вспышка экрана
            GameObject flash = Ui("Flash", root.transform);
            Stretch(flash);
            var flashImage = Img(flash, Color.white, false, false);
            CanvasGroup flashGroup = Group(flash, 0f);

            // Всплывающие числа
            GameObject popups = Ui("Popups", root.transform);
            Stretch(popups);
            TextMeshProUGUI popupTemplate = Txt(popups.transform, "PopupTemplate", "+100", 36, HudStyle.Gold,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Center(popupTemplate.rectTransform, new Vector2(320, 60));
            popupTemplate.gameObject.SetActive(false);

            // Верхняя панель
            GameObject bar = Ui("TopBar", root.transform);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0, 1);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(0.5f, 1);
            barRect.offsetMin = new Vector2(32, -24 - 104);
            barRect.offsetMax = new Vector2(-32, -24);
            Img(bar, HudStyle.Line, false, true);
            var barLayout = bar.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(1, 1, 1, 1);
            barLayout.spacing = 1;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = true;

            Stat(bar.transform, "Loot", "В мешке", 1f, HudStyle.Gold, null, out TextMeshProUGUI lootValue, out _);
            Stat(bar.transform, "Weight", "Тяжесть мешка", 1.1f, HudStyle.Text, HudStyle.Gold, out TextMeshProUGUI weightValue, out RectTransform weightFill);
            GameObject route = Stat(bar.transform, "Route", "До выхода", 1.3f, HudStyle.Text, HudStyle.Mint, out TextMeshProUGUI routeValue, out RectTransform routeFill);
            Stat(bar.transform, "Alarm", "Сигнализация", 0.9f, HudStyle.Mint, null, out TextMeshProUGUI alarmValue, out _);

            // Строка статуса внизу
            GameObject status = Ui("Status", root.transform);
            var statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0);
            statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.anchoredPosition = new Vector2(0, 32);
            Img(status, new Color(0.067f, 0.098f, 0.137f, 0.92f), false, true);
            var statusLayout = status.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            statusLayout.padding = new RectOffset(24, 24, 12, 12);
            statusLayout.childControlWidth = true;
            statusLayout.childControlHeight = true;
            statusLayout.childForceExpandWidth = false;
            statusLayout.childForceExpandHeight = false;
            var statusFitter = status.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            statusFitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            statusFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI statusText = Txt(status.transform, "Text", "Заберите добычу и доберитесь до выхода", 20,
                HudStyle.Text, FontStyles.Normal, TextAlignmentOptions.Center);

            // Карточка старта/результата
            GameObject overlay = Ui("Overlay", root.transform);
            Stretch(overlay);
            Img(overlay, new Color(0.043f, 0.067f, 0.102f, 0.83f), true, false);
            CanvasGroup overlayGroup = Group(overlay, 1f);
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = true;

            GameObject card = Ui("Card", overlay.transform);
            Center((RectTransform)card.transform, new Vector2(700, 0));
            Img(card, new Color(0.094f, 0.129f, 0.169f, 0.97f), false, true);
            var cardLayout = card.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(48, 48, 40, 40);
            cardLayout.spacing = 14;
            cardLayout.childAlignment = TextAnchor.UpperLeft;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = false;
            cardLayout.childForceExpandHeight = false;
            var cardFitter = card.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            cardFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI tag = CardText(card.transform, "Tag", 15, HudStyle.Gold, FontStyles.Bold);
            tag.characterSpacing = 8;
            TextMeshProUGUI title = CardText(card.transform, "Title", 46, HudStyle.Text, FontStyles.Bold);
            TextMeshProUGUI body = CardText(card.transform, "Body", 21, HudStyle.Body, FontStyles.Normal);
            body.lineSpacing = 12;

            GameObject buttonGo = Ui("PrimaryButton", card.transform);
            var buttonImage = Img(buttonGo, HudStyle.Gold, true, true);
            var button = buttonGo.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = buttonImage;
            var buttonLayout = buttonGo.AddComponent<UnityEngine.UI.LayoutElement>();
            buttonLayout.minWidth = buttonLayout.preferredWidth = 360;
            buttonLayout.minHeight = buttonLayout.preferredHeight = 62;
            TextMeshProUGUI buttonLabel = Txt(buttonGo.transform, "Label", "Начать ограбление →", 22, HudStyle.Dark,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(buttonLabel.gameObject);

            TextMeshProUGUI hint = CardText(card.transform, "Hint", 15, HudStyle.Muted, FontStyles.Normal);

            // Ввод для UI: только Input System
            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(root.transform, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            // Компоненты модуля
            var view = root.AddComponent<HudView>();
            Wire(view, "lootValue", lootValue);
            Wire(view, "weightValue", weightValue);
            Wire(view, "weightFill", weightFill);
            Wire(view, "routeBlock", route);
            Wire(view, "routeValue", routeValue);
            Wire(view, "routeFill", routeFill);
            Wire(view, "alarmValue", alarmValue);
            Wire(view, "alarmBorder", borderGroup);
            Wire(view, "statusText", statusText);

            var screens = root.AddComponent<HudScreens>();
            Wire(screens, "overlay", overlayGroup);
            Wire(screens, "tagText", tag);
            Wire(screens, "titleText", title);
            Wire(screens, "bodyText", body);
            Wire(screens, "hintText", hint);
            Wire(screens, "primaryButton", button);
            Wire(screens, "primaryLabel", buttonLabel);

            var fx = root.AddComponent<HudFx>();
            Wire(fx, "popupLayer", popups.transform);
            Wire(fx, "popupTemplate", popupTemplate);
            Wire(fx, "flash", flashGroup);
            Wire(fx, "flashImage", flashImage);

            var audio = root.AddComponent<PresentationAudio>();
            var vfx = root.AddComponent<WorldVfx>();

            var controller = root.AddComponent<PresentationController>();
            Wire(controller, "hud", view);
            Wire(controller, "screens", screens);
            Wire(controller, "fx", fx);
            Wire(controller, "sfx", audio);
            Wire(controller, "vfx", vfx);

            var binder = root.AddComponent<PresentationBinder>();
            Wire(binder, "presentation", controller);

            return root;
        }

        private static GameObject Stat(Transform parent, string name, string label, float flex, Color valueColor,
            Color? meterColor, out TextMeshProUGUI value, out RectTransform fill)
        {
            GameObject cell = Ui(name, parent);
            Img(cell, HudStyle.Panel, false, false);
            var layout = cell.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.flexibleWidth = flex;
            layout.preferredWidth = 0;

            TextMeshProUGUI caption = Txt(cell.transform, "Label", label.ToUpperInvariant(), 14, HudStyle.Muted,
                FontStyles.Bold, TextAlignmentOptions.TopLeft);
            caption.characterSpacing = 8;
            TopBand(caption.rectTransform, 14, 20);

            value = Txt(cell.transform, "Value", "0", 34, valueColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            TopBand(value.rectTransform, 36, 44);

            fill = null;
            if (meterColor.HasValue)
            {
                GameObject meter = Ui("Meter", cell.transform);
                var m = (RectTransform)meter.transform;
                m.anchorMin = new Vector2(0, 0);
                m.anchorMax = new Vector2(1, 0);
                m.pivot = new Vector2(0.5f, 0);
                m.offsetMin = new Vector2(22, 14);
                m.offsetMax = new Vector2(-22, 19);
                Img(meter, new Color(0.208f, 0.255f, 0.318f, 1f), false, false);

                GameObject fillGo = Ui("Fill", meter.transform);
                fill = (RectTransform)fillGo.transform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0, 1);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                Img(fillGo, meterColor.Value, false, false);
            }
            return cell;
        }

        // ---------------- Помощники ----------------

        private static GameObject Ui(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static UnityEngine.UI.Image Img(GameObject go, Color color, bool raycast, bool rounded)
        {
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (rounded && roundSprite != null)
            {
                image.sprite = roundSprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.35f;
            }
            return image;
        }

        private static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color,
            FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = Ui(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = alignment;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false; // текст не должен перехватывать клики по игре
            return t;
        }

        private static TextMeshProUGUI CardText(Transform parent, string name, float size, Color color, FontStyles style)
        {
            TextMeshProUGUI t = Txt(parent, name, name, size, color, style, TextAlignmentOptions.TopLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            var layout = t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.flexibleWidth = 1;
            return t;
        }

        private static void Edge(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
        {
            GameObject go = Ui(name, parent);
            var r = (RectTransform)go.transform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = pivot;
            r.sizeDelta = size;
            r.anchoredPosition = Vector2.zero;
            Img(go, HudStyle.Red, false, false);
        }

        private static void Stretch(GameObject go)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform r, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = Vector2.zero;
        }

        private static void TopBand(RectTransform r, float top, float height)
        {
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(22, -top - height);
            r.offsetMax = new Vector2(-22, -top);
        }

        private static CanvasGroup Group(GameObject go, float alpha)
        {
            var g = go.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            g.blocksRaycasts = false;
            g.interactable = false;
            return g;
        }

        private static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"FunFlow: поле {field} не найдено в {target.GetType().Name}");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
