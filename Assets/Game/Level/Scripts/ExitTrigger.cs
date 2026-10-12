using UnityEngine;

public class ExitTrigger : MonoBehaviour, ILevelExit
{
    [Header("Core Reference")]
    [Tooltip("Необязательная ссылка: при запуске Core находится в этой же сцене.")]
    [SerializeField] private MonoBehaviour coreSessionSource;
    
    private IRunSession runSession;

    private void Start()
    {
        runSession = LevelServices.Resolve<IRunSession>(this, coreSessionSource);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что к нам приехал игрок (Слой Player - 8)
        if (other.gameObject.layer == LayerMask.NameToLayer("Player") && runSession != null)
        {
            Debug.Log("[ExitTrigger] Игрок достиг выхода! Уровень пройден!");

            runSession.ReachExit();
        }
    }
}
