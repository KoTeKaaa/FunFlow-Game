using UnityEngine;

[RequireComponent(typeof(Camera)), DisallowMultipleComponent]
public sealed class CorridorCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _offset = new Vector3(6f, 14f, -8f);

    private void OnEnable() => FollowTarget();
    private void LateUpdate() => FollowTarget();

    private void FollowTarget()
    {
        if (_target == null)
        {
            Debug.LogError("CorridorCamera: assign Target in Inspector.", this);
            enabled = false;
            return;
        }

        // Follow progression only; retain the angle authored in the scene.
        transform.position = new Vector3(_target.position.x + _offset.x, _offset.y, _offset.z);
    }
}
