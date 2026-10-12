using UnityEngine;

public class AlarmTrap : MonoBehaviour
{
    [SerializeField] private MonoBehaviour coreSessionSource;
    private IRunSession runSession;
    private bool _isUsed = false;

    private void Start()
    {
        runSession = LevelServices.Resolve<IRunSession>(this, coreSessionSource);
        coreSessionSource = runSession as MonoBehaviour;
    }

    private void OnEnable()
    {
        // Обязательно сбрасываем триггер при перезапуске уровня
        _isUsed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isUsed || runSession == null) return;

        // Сигнализация реагирует ТОЛЬКО на вора (Слой Player)
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            if (runSession.State == RunState.Playing)
            {
                _isUsed = true;
                Debug.Log("[AlarmTrap] Сигнализация активирована!");
                runSession.TriggerAlarm(); 
            }
        }
    }
}
