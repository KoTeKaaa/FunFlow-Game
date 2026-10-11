using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>Всплывающие числа над точкой мира («+100», «−35») и вспышка экрана.</summary>
    public sealed class HudFx : MonoBehaviour
    {
        [SerializeField] private RectTransform popupLayer;
        [SerializeField] private TMP_Text popupTemplate;
        [SerializeField] private CanvasGroup flash;
        [SerializeField] private UnityEngine.UI.Image flashImage;
        [Tooltip("Пусто — используется Camera.main")]
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0.1f)] private float popupLifetime = 0.9f;
        [SerializeField] private float popupRise = 80f;
        [SerializeField, Min(0.05f)] private float flashFade = 0.35f;

        private sealed class PopupItem
        {
            public TMP_Text Text;
            public Vector3 World;
            public float Age;
            public bool Active;
        }

        private readonly List<PopupItem> popups = new List<PopupItem>();
        private Canvas rootCanvas;

        private void Awake()
        {
            if (popupTemplate != null) popupTemplate.gameObject.SetActive(false);
            if (flash != null) flash.alpha = 0f;
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
        }

        public void ShowPopup(Vector3 worldPosition, string text, Color color)
        {
            if (popupTemplate == null || popupLayer == null) return;

            PopupItem p = null;
            for (int i = 0; i < popups.Count; i++)
            {
                if (!popups[i].Active) { p = popups[i]; break; }
            }
            if (p == null)
            {
                p = new PopupItem { Text = Instantiate(popupTemplate, popupLayer) };
                p.Text.name = "Popup";
                popups.Add(p);
            }

            p.Text.text = text;
            p.Text.color = color;
            p.World = worldPosition;
            p.Age = 0f;
            p.Active = true;
            p.Text.gameObject.SetActive(true);
            Place(p, 0f);
        }

        public void Flash(Color color, float strength)
        {
            if (flash == null) return;
            if (flashImage != null) flashImage.color = new Color(color.r, color.g, color.b, 1f);
            flash.alpha = Mathf.Max(flash.alpha, Mathf.Clamp01(strength));
        }

        public void ClearAll()
        {
            for (int i = 0; i < popups.Count; i++)
            {
                popups[i].Active = false;
                popups[i].Text.gameObject.SetActive(false);
            }
            if (flash != null) flash.alpha = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (flash != null && flash.alpha > 0f)
                flash.alpha = Mathf.MoveTowards(flash.alpha, 0f, dt / flashFade);

            for (int i = 0; i < popups.Count; i++)
            {
                PopupItem p = popups[i];
                if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= popupLifetime)
                {
                    p.Active = false;
                    p.Text.gameObject.SetActive(false);
                    continue;
                }
                Place(p, p.Age / popupLifetime);
            }
        }

        private void Place(PopupItem p, float t)
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) { p.Text.enabled = false; return; }

            Vector3 screen = cam.WorldToScreenPoint(p.World);
            if (screen.z < 0f) { p.Text.enabled = false; return; }
            p.Text.enabled = true;

            Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, screen, uiCamera, out Vector2 local);

            float ease = 1f - (1f - t) * (1f - t);
            p.Text.rectTransform.localPosition = local + Vector2.up * (popupRise * ease);
            p.Text.rectTransform.localScale = Vector3.one * (1f + 0.3f * (1f - ease));
            p.Text.alpha = 1f - t * t;
        }
    }
}
