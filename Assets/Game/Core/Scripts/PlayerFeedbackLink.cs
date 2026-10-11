using UnityEngine;
using UnityEngine.Events;

public sealed class PlayerFeedbackLink : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _playerSource;
    [SerializeField] private UnityEvent<Vector3, float> _impact = new UnityEvent<Vector3, float>();
    private IPlayerControl _player;

    private void OnEnable()
    {
        _player = _playerSource as IPlayerControl;
        if (_player != null) _player.Collided += HandleImpact;
    }

    private void OnDisable()
    {
        if (_player != null) _player.Collided -= HandleImpact;
        _player = null;
    }

    private void HandleImpact(Vector3 point, float strength) => _impact.Invoke(point, strength);
}
