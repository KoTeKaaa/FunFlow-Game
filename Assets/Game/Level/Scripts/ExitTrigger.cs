using UnityEngine;
// using Game.Core.Contracts; // Раскомментируем, когда участник №1 добавит контракты

public class ExitTrigger : MonoBehaviour
{
    [Header("Core Reference")]
    [Tooltip("Ссылка на объект Core, реализующий IRunSession. Будет назначено участником №1.")]
    [SerializeField] private MonoBehaviour coreSessionSource;
    
    // private IRunSession runSession;

    private void Start()
    {
        // runSession = coreSessionSource as IRunSession;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что к нам приехал игрок (Слой Player - 8)
        if (other.gameObject.layer == 8)
        {
            Debug.Log("[ExitTrigger] Игрок достиг выхода! Уровень пройден!");

            /*
            if (runSession != null)
            {
                // Сообщаем ядру, что выход достигнут (название метода по правилам team-guide)
                runSession.ReachExit();
            }
            */
        }
    }
}