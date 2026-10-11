using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FunFlow.Presentation
{
    public sealed class LevelRibbon : ScrollRect
    {
        public override void OnScroll(PointerEventData data)
        {
            Vector2 original = data.scrollDelta;
            // Колесо вниз должно вести к следующим карточкам, вправо по ленте.
            if (Mathf.Abs(original.y) > Mathf.Abs(original.x))
                data.scrollDelta = new Vector2(original.x, -original.y);
            base.OnScroll(data);
            data.scrollDelta = original;
        }
    }
}
