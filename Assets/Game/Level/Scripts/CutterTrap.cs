using UnityEngine;

public class CutterTrap : MonoBehaviour
{
    [Header("Настройки ловушки")]
    [Tooltip("Доля стоимости, которая будет потеряна. В прототипе это 35% (0.35).")]
    [Range(0f, 1f)]
    public float lossFraction = 0.35f; 

    private ILootInventory _lootInventory;
    private bool _isUsed = false;

    private void Start()
    {
        // Ищем объект с интерфейсом ILootInventory на сцене
        var allScripts = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var script in allScripts)
        {
            if (script is ILootInventory loot)
            {
                _lootInventory = loot;
                break;
            }
        }

        if (_lootInventory == null)
        {
            Debug.LogWarning($"[{gameObject.name}] ILootInventory не найден на сцене! Ловушка не сработает.");
        }
    }

    private void OnEnable()
    {
        _isUsed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isUsed || _lootInventory == null) return; 

        // ТЕПЕРЬ ЛОВУШКА РЕАГИРУЕТ НА ИГРОКА
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            _isUsed = true; 
            Debug.Log($"[CutterTrap] Игрок задел шипы! Потеря: {lossFraction * 100}% из мешка");
            _lootInventory.RemoveFraction(lossFraction);
        }
    }
}