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
            controller.FindProperty("playingHint").stringValue = "<sprite=3>  Зажми • оттяни • отпусти";
            controller.ApplyModifiedPropertiesWithoutUndo();
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
