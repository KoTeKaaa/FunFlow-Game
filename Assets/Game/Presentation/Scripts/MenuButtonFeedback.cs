using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FunFlow.Presentation
{
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private Button button;
        private Vector3 original;
        private bool hovered, selected;
        private void Awake() { button = GetComponent<Button>(); original = transform.localScale; }
        private void OnEnable() => GetComponent<Button>().onClick.AddListener(Click);
        private void OnDisable()
        {
            GetComponent<Button>().onClick.RemoveListener(Click);
            hovered = selected = false;
            if (original != Vector3.zero) transform.localScale = original;
        }
        private void Click() => GetComponentInParent<MainMenuController>()?.PlayClick();
        private void Update()
        {
            float scale = button.interactable && (hovered || selected) ? 1.025f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, original * scale, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }
        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) => selected = false;
    }
}
