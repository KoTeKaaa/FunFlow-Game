using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace FunFlow.Presentation.EditorTools
{
    /// <summary>Оформляет существующую иерархию HUD, не меняя игровые связи.</summary>
    public static class HudArtDirection
    {
        private const string UiPath = "Assets/Game/Presentation/UI/";

        public static void Apply(GameObject root)
        {
            Sprite plate = Plate();
            TMP_SpriteAsset icons = Icons();
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.color = HudStyle.Text;
                text.characterSpacing = 0;
                text.spriteAsset = icons;
            }
            var bar = root.transform.Find("TopBar");
            bar.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().enabled = false;
            bar.GetComponent<UnityEngine.UI.Image>().enabled = false;
            var br = (RectTransform)bar;
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.offsetMin = br.offsetMax = Vector2.zero;
            Cell(bar.Find("Loot"), plate, new Vector2(0, 1), new Vector2(28, -24), new Vector2(350, 120), "<sprite=0> ДОБЫЧА", HudStyle.Gold);
            Cell(bar.Find("Weight"), plate, new Vector2(0, 1), new Vector2(28, -152), new Vector2(350, 108), "ТЯЖЕСТЬ МЕШКА", HudStyle.Text);
            Cell(bar.Find("Route"), plate, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(320, 120), "<sprite=2> ДО ВЫХОДА", HudStyle.Mint);
            Cell(bar.Find("Alarm"), plate, new Vector2(1, 1), new Vector2(-28, -24), new Vector2(280, 120), "<sprite=1> СИГНАЛИЗАЦИЯ", HudStyle.Mint);
            Panel(root.transform.Find("Status"), plate, HudStyle.Panel);
            var status = root.transform.Find("Status").GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            status.padding = new RectOffset(28, 28, 18, 18);
            Text(root.transform.Find("Status/Text"), 24, HudStyle.Text);
            Panel(root.transform.Find("Overlay/Card"), plate, HudStyle.Panel);
            var card = root.transform.Find("Overlay/Card");
            ((RectTransform)card).sizeDelta = new Vector2(760, 0);
            card.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().padding = new RectOffset(48, 48, 38, 38);
            Text(card.Find("Tag"), 20, HudStyle.Gold);
            Text(card.Find("Title"), 48, HudStyle.Text);
            Text(card.Find("Body"), 26, HudStyle.Body);
            Text(card.Find("Hint"), 20, HudStyle.Muted);
            Panel(card.Find("PrimaryButton"), plate, HudStyle.Gold);
            Text(card.Find("PrimaryButton/Label"), 28, HudStyle.Dark);
            var button = card.Find("PrimaryButton").GetComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 0.94f, 0.8f);
            colors.pressedColor = new Color(0.8f, 0.68f, 0.5f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            card.Find("PrimaryButton").GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 72;
            foreach (var image in root.transform.Find("AlarmBorder").GetComponentsInChildren<UnityEngine.UI.Image>())
                image.color = new Color(HudStyle.Red.r, HudStyle.Red.g, HudStyle.Red.b, 0.22f);
            var controller = new SerializedObject(root.GetComponent<PresentationController>());
            controller.FindProperty("playingHint").stringValue = "Зажми • оттяни • отпусти";
            controller.ApplyModifiedPropertiesWithoutUndo();
            ApplyStatusIcons(root);
            ApplyTutorialIcons(root);
            ApplyStatusMouse(root);
            ApplyResultMenuButton(root);
        }

        public static void ApplyStatusIcons(GameObject root)
        {
            var bar = root.transform.Find("TopBar");
            foreach (string name in new[] { "Loot", "Weight", "Route", "Alarm" })
            {
                var cell = bar.Find(name);
                var rect = (RectTransform)cell;
                rect.sizeDelta = new Vector2(name is "Loot" or "Weight" ? 410 : name == "Route" ? 380 : 350, rect.sizeDelta.y);
                var caption = cell.Find("Label").GetComponent<TMP_Text>();
                caption.text = System.Text.RegularExpressions.Regex.Replace(caption.text, "<sprite[^>]*>", "").Trim();
                float size = name == "Weight" ? 42 : name == "Route" ? 72 : 84;
                float inset = name == "Weight" ? 76 : 112;
                foreach (string text in new[] { "Label", "Value" })
                {
                    var band = (RectTransform)cell.Find(text);
                    var min = band.offsetMin; min.x = inset;
                    var max = band.offsetMax; max.x = -20;
                    if (name == "Loot" && text == "Value") band.pivot = new Vector2(0, 0.5f);
                    band.offsetMin = min; band.offsetMax = max;
                }
                var icon = cell.Find("StatusIcon");
                if (icon == null)
                {
                    var go = new GameObject("StatusIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    go.layer = cell.gameObject.layer;
                    go.transform.SetParent(cell, false); icon = go.transform;
                }
                var image = icon.GetComponent<UnityEngine.UI.Image>();
                string asset = name == "Route" ? "Exit" : name == "Alarm" ? "AlarmIdle" : name;
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Game/Presentation/UI/HudIcons/{asset}.png");
                image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
                var position = (RectTransform)icon;
                position.anchorMin = position.anchorMax = position.pivot = new Vector2(0, 0.5f);
                position.sizeDelta = Vector2.one * size;
                position.anchoredPosition = new Vector2(16, name == "Weight" ? 8 : name == "Route" ? 10 : 0);
            }
            var data = new SerializedObject(root.GetComponent<HudView>());
            data.FindProperty("alarmIcon").objectReferenceValue = bar.Find("Alarm/StatusIcon").GetComponent<UnityEngine.UI.Image>();
            data.FindProperty("alarmIdleSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Presentation/UI/HudIcons/AlarmIdle.png");
            data.FindProperty("alarmAlertSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Presentation/UI/HudIcons/Alarm.png");
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ApplyTutorialIcons(GameObject root)
        {
            var card = root.transform.Find("Overlay/Card");
            var body = card.Find("Body").GetComponent<TextMeshProUGUI>();
            var tutorial = card.Find("Tutorial");
            if (tutorial == null)
            {
                var go = new GameObject("Tutorial", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup));
                go.layer = body.gameObject.layer;
                go.transform.SetParent(card, false);
                tutorial = go.transform;
            }
            tutorial.SetSiblingIndex(body.transform.GetSiblingIndex() + 1);
            var group = tutorial.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            group.spacing = 10; group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            string[] assets = { "Mouse", "Loot", "Alarm", "Exit" };
            string[] captions = {
                "<b>Зажми • оттяни • отпусти.</b> Дальше оттянул — сильнее запуск.",
                "<b>Собирай добычу.</b> Тяжёлый мешок сокращает дальность.",
                "<b>Берегись ловушек.</b> Лезвия режут мешок, красные плиты включают тревогу.",
                "<b>Выход справа.</b> Двигайся только вперёд." };
            for (int i = 0; i < assets.Length; i++)
            {
                var row = tutorial.Find(assets[i]);
                if (row != null) continue;
                var go = new GameObject(assets[i], typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
                go.layer = body.gameObject.layer; go.transform.SetParent(tutorial, false); row = go.transform;
                var layout = go.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                layout.spacing = 14; layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
                icon.layer = go.layer; icon.transform.SetParent(row, false);
                var image = icon.GetComponent<UnityEngine.UI.Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "HudIcons/" + assets[i] + ".png");
                image.preserveAspect = true; image.raycastTarget = false;
                var size = icon.GetComponent<UnityEngine.UI.LayoutElement>();
                size.minWidth = size.preferredWidth = 44; size.minHeight = size.preferredHeight = 44;
                var label = Object.Instantiate(body, row);
                label.name = "Text"; label.text = captions[i]; label.raycastTarget = false;
                var textLayout = label.GetComponent<UnityEngine.UI.LayoutElement>();
                if (textLayout == null) textLayout = label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                textLayout.flexibleWidth = 1;
            }
            var data = new SerializedObject(root.GetComponent<HudScreens>());
            data.FindProperty("tutorial").objectReferenceValue = tutorial.gameObject;
            data.ApplyModifiedPropertiesWithoutUndo();
            tutorial.gameObject.SetActive(false);
        }

        public static void ApplyStatusMouse(GameObject root)
        {
            var status = root.transform.Find("Status");
            var icon = status.Find("MouseIcon");
            if (icon == null)
            {
                var go = new GameObject("MouseIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
                go.layer = status.gameObject.layer; go.transform.SetParent(status, false); icon = go.transform;
            }
            icon.SetAsFirstSibling();
            var image = icon.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "HudIcons/Mouse.png");
            image.preserveAspect = true; image.raycastTarget = false;
            var size = icon.GetComponent<UnityEngine.UI.LayoutElement>();
            size.minWidth = size.preferredWidth = 32; size.minHeight = size.preferredHeight = 32;
            ((RectTransform)icon).sizeDelta = Vector2.one * 32;
            status.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing = 14;
            var data = new SerializedObject(root.GetComponent<HudView>());
            data.FindProperty("statusIcon").objectReferenceValue = image;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ApplyResultMenuButton(GameObject root)
        {
            var flow = root.GetComponent<HudLevelFlow>();
            if (flow == null) return;
            var card = root.transform.Find("Overlay/Card");
            var button = card.Find("MenuButton");
            if (button == null)
            {
                button = Object.Instantiate(card.Find("PrimaryButton"), card);
                button.name = "MenuButton";
            }
            button.SetSiblingIndex(card.Find("PrimaryButton").GetSiblingIndex() + 1);
            var label = button.Find("Label").GetComponent<TMP_Text>();
            label.color = HudStyle.Text;
            ButtonLabels.Set(label, "Выход в главное меню");
            button.GetComponent<UnityEngine.UI.Image>().color = HudStyle.Panel;
            var data = new SerializedObject(flow);
            data.FindProperty("menuButton").objectReferenceValue = button.GetComponent<UnityEngine.UI.Button>();
            data.ApplyModifiedPropertiesWithoutUndo();
            button.gameObject.SetActive(false);
        }

        private static void Cell(Transform cell, Sprite plate, Vector2 anchor, Vector2 position, Vector2 size, string label, Color accent)
        {
            var r = (RectTransform)cell;
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            Panel(cell, plate, HudStyle.Panel);
            var caption = Text(cell.Find("Label"), 22, accent);
            caption.text = label;
            Band(caption.rectTransform, 10, 38);
            var value = Text(cell.Find("Value"), 42, accent);
            value.fontStyle = FontStyles.Bold;
            Band(value.rectTransform, 46, 52);
            var meter = cell.Find("Meter");
            if (meter != null)
            {
                var m = (RectTransform)meter;
                m.offsetMin = new Vector2(24, 12);
                m.offsetMax = new Vector2(-24, 20);
                meter.GetComponent<UnityEngine.UI.Image>().color = HudStyle.Line;
                meter.Find("Fill").GetComponent<UnityEngine.UI.Image>().color = cell.name == "Weight" ? HudStyle.Gold : HudStyle.Mint;
                Band(value.rectTransform, 42, 44);
                value.fontSize = 34;
            }
        }

        private static void Band(RectTransform r, float top, float height)
        {
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(24, -top - height);
            r.offsetMax = new Vector2(-24, -top);
        }

        private static TextMeshProUGUI Text(Transform t, float size, Color color)
        {
            var text = t.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            return text;
        }

        private static void Panel(Transform t, Sprite plate, Color color)
        {
            var image = t.GetComponent<UnityEngine.UI.Image>();
            image.sprite = plate;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1;
            image.color = color;
        }

        private static Sprite Plate()
        {
            string path = UiPath + "HeistPlate.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, 63 - x), Mathf.Min(y, 63 - y));
                int corner = Mathf.Min(x, 63 - x) + Mathf.Min(y, 63 - y) - 10;
                int d = Mathf.Min(edge, corner / 2);
                texture.SetPixel(x, y, d < 0 ? Color.clear : d < 3 ? new Color(0.35f, 0.32f, 0.42f) : d < 5 ? new Color(1.6f, 1.5f, 1.7f) : Color.white);
            }
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(18, 18, 18, 18);
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static TMP_SpriteAsset Icons()
        {
            string path = UiPath + "HeistIcons.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(path);
            if (asset != null) return asset;
            var texture = new Texture2D(256, 64, TextureFormat.RGBA32, false) { name = "HeistIcons" };
            for (int i = 0; i < 4; i++)
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = x - 32, dy = y - 28;
                bool ink = i == 0 ? (dx * dx / 400 + dy * dy / 324 < 1 && y < 43) || (y >= 43 && y < 54 && Mathf.Abs(dx) < (y - 40) * 0.7f) || (y >= 40 && y < 45 && Mathf.Abs(dx) < 14)
                    : i == 1 ? (dx * dx + dy * dy < 280 && y >= 20) || (y >= 12 && y <= 20 && Mathf.Abs(dx) < 23) || (y > 49 && y < 59 && Mathf.Abs(dx) < 3) || (Mathf.Abs(dx) > 23 && Mathf.Abs(dx) < 30 && y > 30 && y < 35)
                    : i == 2 ? ((x >= 16 && x <= 20 || x >= 44 && x <= 48) && y >= 10 && y <= 54) || (y >= 50 && y <= 54 && x >= 16 && x <= 48) || (x > 23 && x < 42 && y >= 10 && y <= 47 && !(x > 34 && y > 25 && y < 31))
                    : ((dx * dx / 324 + (y - 32) * (y - 32) / 576f < 1) && (dx * dx / 225 + (y - 32) * (y - 32) / 441f > 1)) || (Mathf.Abs(dx) < 2 && y > 35 && y < 54);
                texture.SetPixel(i * 64 + x, y, ink ? Color.white : Color.clear);
            }
            texture.Apply();
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = "1.1.0";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.name = "HeistIcons";
            asset.faceInfo = new UnityEngine.TextCore.FaceInfo { pointSize = 48, scale = 1, ascentLine = 56, capLine = 48 };
            asset.spriteSheet = texture;
            asset.material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "HeistIcons Material", mainTexture = texture };
            for (uint i = 0; i < 4; i++)
            {
                var glyph = new TMP_SpriteGlyph(i, new GlyphMetrics(64, 64, 0, 56, 68), new GlyphRect((int)i * 64, 0, 64, 64), 1, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xE000 + i, asset, glyph) { name = "heist" + i, scale = 1 });
            }
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(texture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
