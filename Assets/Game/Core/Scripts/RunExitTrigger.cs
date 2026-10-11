using UnityEngine;

[RequireComponent(typeof(Collider)), DisallowMultipleComponent]
public sealed class RunExitTrigger : MonoBehaviour
{
    [SerializeField] private RunSession _session;
    [SerializeField] private Rigidbody _player;

    private void OnEnable()
    {
        if (_session != null && _player != null && GetComponent<Collider>().isTrigger)
            return;

        Debug.LogError("RunExitTrigger: assign Session, Player and a trigger Collider in Inspector.", this);
        enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActiveAndEnabled && _session != null && _player != null
            && other.attachedRigidbody == _player)
            _session.ReachExit();
    }
}
