using UnityEngine;
// Закомментировано до получения контрактов от участника №1
// using Game.Core.Contracts; 

public class CutterTrap : MonoBehaviour
{
    [Header("Настройки ловушки")]
    [Tooltip("Доля стоимости, которая будет потеряна. В прототипе это 35% (0.35).")]
    [Range(0f, 1f)]
    public float lossFraction = 0.35f; 

    // Защита от двойного срабатывания (как t.used = true в прототипе)
    private bool _isUsed = false;

    private void OnTriggerEnter(Collider other)
    {
        // Если ловушка уже сработала, игнорируем новые контакты
        if (_isUsed) return; 

        // Ловушка реагирует ТОЛЬКО на мешок (Слой Bag - 12)
        if (other.gameObject.layer == 12) 
        {
            _isUsed = true; // Отмечаем как использованную
            Debug.Log($"[CutterTrap] Мешок разрезан! Потеря: {lossFraction * 100}%");

            // --- БЛОК ИНТЕГРАЦИИ ---
            /*
            ILootInventory loot = other.GetComponentInParent<ILootInventory>();
            if (loot != null)
            {
                loot.RemoveFraction(lossFraction);
            }
            */
            // -----------------------
        }
    }
}