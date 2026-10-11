using UnityEngine;

namespace FunFlow.Presentation
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MenuPanelReveal : MonoBehaviour
    {
        private CanvasGroup group;
        private void Awake() => group = GetComponent<CanvasGroup>();
        private void OnEnable() { if (group != null) group.alpha = 0; }
        private void Update() => group.alpha = Mathf.MoveTowards(group.alpha, 1, Time.unscaledDeltaTime * 5);
    }
}
