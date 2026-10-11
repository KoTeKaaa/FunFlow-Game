using UnityEngine;
// using Game.Core.Contracts; 

public class AlarmTrap : MonoBehaviour
{
    [Header("Core Reference")]
    [Tooltip("Ссылка на объект Core, реализующий IRunSession. Будет назначено участником №1.")]
    [SerializeField] private MonoBehaviour coreSessionSource;
    
    // private IRunSession runSession;
    private bool _isUsed = false;

    private void Start()
    {
        // runSession = coreSessionSource as IRunSession;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isUsed) return; 

        // Сигнализация реагирует ТОЛЬКО на вора (Слой Player - 8)
        if (other.gameObject.layer == 8)
        {
            _isUsed = true;
            Debug.Log("[AlarmTrap] Сигнализация активирована! (Запуск таймера на 60 сек)");

            // --- БЛОК ИНТЕГРАЦИИ ---
            /*
            if (runSession != null)
            {
                runSession.TriggerAlarm(); 
            }
            */
            // -----------------------
        }
    }
}