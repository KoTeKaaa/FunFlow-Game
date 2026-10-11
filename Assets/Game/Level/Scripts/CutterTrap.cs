using UnityEngine;

public class CutterTrap : MonoBehaviour
{
    [Header("Настройки ловушки")]
    [Tooltip("Доля стоимости, которая будет потеряна. В прототипе это 35% (0.35).")]
    [Range(0f, 1f)]
    public float lossFraction = 0.35f; 
    [SerializeField] private MonoBehaviour lootSource;

    // Защита от двойного срабатывания (как t.used = true в прототипе)
    private bool _isUsed = false;

    private void OnTriggerEnter(Collider other)
    {
        // Если ловушка уже сработала, игнорируем новые контакты
        if (_isUsed) return; 

        // Ловушка реагирует ТОЛЬКО на мешок (Слой Bag - 12)
        if (other.gameObject.layer == 12 && lootSource is ILootInventory)
        {
            _isUsed = true; // Отмечаем как использованную
            Debug.Log($"[CutterTrap] Мешок разрезан! Потеря: {lossFraction * 100}%");

            ILootInventory loot = lootSource as ILootInventory;
            if (loot != null)
            {
                loot.RemoveFraction(lossFraction);
            }
        }
    }
}
