using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FunFlow.Presentation
{
    // Navigation icons use their own sprite, independently of the text font.
    public static class ButtonLabels
    {
        public static void Set(TMP_Text text, string value)
        {
            if (text == null) return;
            bool left = value.Contains("←"), right = value.Contains("→");
            text.text = value.Replace("←", "").Replace("→", "").Trim();
            var child = text.transform.Find("NavigationIcon");
            if (!left && !right)
            {
                if (child != null) child.gameObject.SetActive(false);
                return;
            }
            if (child == null)
            {
                var go = new GameObject("NavigationIcon", typeof(RectTransform), typeof(Image));
                go.layer = text.gameObject.layer;
                go.transform.SetParent(text.transform, false);
                child = go.transform;
            }
            child.gameObject.SetActive(true);
            var image = child.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("NavigationArrow");
            image.color = text.color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            var rect = (RectTransform)child;
            float size = Mathf.Clamp(text.fontSize * 0.95f, 18f, 26f);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = Vector2.one * size;
            rect.anchoredPosition = new Vector2((text.preferredWidth * 0.5f + size * 0.5f + 8f) * (left ? -1 : 1), 0);
            rect.localScale = new Vector3(left ? -1 : 1, 1, 1);
            rect.localRotation = Quaternion.identity;
        }
    }
}
