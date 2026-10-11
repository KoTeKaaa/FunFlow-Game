using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Runs before Player's polling Update; a UI gesture stays blocked until release.
[DefaultExecutionOrder(-200)]
public sealed class UiLaunchGuard : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _launchInput;
    private readonly List<RaycastResult> _hits = new List<RaycastResult>();
    private bool _blockedGesture;

    private void Update()
    {
        if (_launchInput == null || Mouse.current == null)
            return;

        bool overUi = false;
        var events = EventSystem.current;
        if (events != null)
        {
            var pointer = new PointerEventData(events) { position = Mouse.current.position.ReadValue() };
            _hits.Clear();
            events.RaycastAll(pointer, _hits);
            overUi = _hits.Exists(hit => hit.module is UnityEngine.UI.GraphicRaycaster);
        }

        bool pressed = Mouse.current.leftButton.isPressed;
        if (overUi && pressed)
            _blockedGesture = true;

        _launchInput.enabled = !overUi && !_blockedGesture;
        if (!pressed)
            _blockedGesture = false; // Keep disabled on the release frame too.
    }
}
