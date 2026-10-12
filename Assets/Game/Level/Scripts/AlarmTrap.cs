using UnityEngine;

public class AlarmTrap : MonoBehaviour
{
    private IRunSession runSession;
    private bool _isUsed = false;

    private void Start()
    {
        // В Unity 6 можно искать объекты, реализующие интерфейс, напрямую.
        // Это избавит от необходимости привязывать ядро руками в каждой ловушке.
        var sessionSource = Object.FindAnyObjectByType<MonoBehaviour>(); 
        
        // Перебираем MonoBehaviour'ы, чтобы найти тот, который реализует IRunSession
        // (Архитектор Участник №1 скорее всего повесит IRunSession на какой-то глобальный менеджер)
        var allScripts = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var script in allScripts)
        {
            if (script is IRunSession session)
            {
                runSession = session;
                break;
            }
        }

        if (runSession == null)
        {
            Debug.LogWarning($"[{gameObject.name}] IRunSession не найден на сцене! Сигнализация не сработает.");
        }
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
                Debug.Log("[AlarmTrap] Сигнализация активирована! (Запуск таймера на 60 сек)");
                runSession.TriggerAlarm(); 
            }
        }
    }
}