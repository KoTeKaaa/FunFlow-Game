using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FunFlow.Presentation
{
    public sealed class LevelCardView : MonoBehaviour, UnityEngine.EventSystems.ISelectHandler
    {
        [SerializeField] private TMP_Text number, title, description, action;
        [SerializeField] private Image artwork;
        [SerializeField] private Button button;
        public Button Button => button;

        public void OnSelect(UnityEngine.EventSystems.BaseEventData eventData)
        {
            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            float left = scroll.viewport.InverseTransformPoint(corners[0]).x;
            float right = scroll.viewport.InverseTransformPoint(corners[2]).x;
            var bounds = scroll.viewport.rect;
            float delta = left < bounds.xMin ? bounds.xMin - left : right > bounds.xMax ? bounds.xMax - right : 0;
            var position = scroll.content.anchoredPosition;
            position.x = Mathf.Clamp(position.x + delta, -Mathf.Max(0, scroll.content.rect.width - bounds.width), 0);
            scroll.content.anchoredPosition = position;
        }

        public void Bind(LevelCatalog.Entry entry, int index, Action selected)
        {
            number.text = $"УРОВЕНЬ {index + 1:00}";
            title.text = string.IsNullOrWhiteSpace(entry.Title) ? $"Уровень {index + 1}" : entry.Title;
            description.text = entry.Description ?? string.Empty;
            if (entry.Artwork != null) artwork.sprite = entry.Artwork;
            var aspect = artwork.GetComponent<AspectRatioFitter>();
            if (aspect != null && artwork.sprite != null)
                aspect.aspectRatio = artwork.sprite.rect.width / artwork.sprite.rect.height;
            button.interactable = !string.IsNullOrWhiteSpace(entry.ScenePath) && UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(entry.ScenePath) >= 0;
            ButtonLabels.Set(action, button.interactable ? "Начать" : "Недоступен в сборке");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selected());
        }
    }
}
