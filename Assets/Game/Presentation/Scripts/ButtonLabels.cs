using TMPro;

namespace FunFlow.Presentation
{
    // Keep navigation labels free of decorative arrows.
    public static class ButtonLabels
    {
        public static void Set(TMP_Text text, string value)
        {
            if (text == null) return;
            text.text = value.Replace("←", "").Replace("→", "").Trim();
            var child = text.transform.Find("NavigationIcon");
            if (child != null) child.gameObject.SetActive(false);
        }
    }
}
