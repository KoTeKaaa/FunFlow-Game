using UnityEngine;
// using Game.Core.Contracts; 

public class ValuableShowcase : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Награда за кувшин (в прототипе 320)")]
    public int rewardValue = 320;
    
    [Tooltip("Множитель скорости после пробития стекла (в прототипе 0.84)")]
    public float speedMultiplier = 0.84f;

    [Header("Визуал")]
    public GameObject glassModel; // Ссылка на модель стекла
    public GameObject vaseModel;  // Ссылка на модель кувшина

    private bool _isBroken = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_isBroken) return;

        // Проверяем, что витрину пробил сам игрок (Слой Player - 8)
        if (other.gameObject.layer == 8)
        {
            _isBroken = true;
            Debug.Log($"[Showcase] Витрина разбита! Получено: {rewardValue}. Игрок замедлен.");

            // 1. Замедляем игрока (как p.vx *= 0.84 в прототипе)
            Rigidbody playerRb = other.attachedRigidbody;
            if (playerRb != null)
            {
                // В Unity 6 используется свойство linearVelocity
                playerRb.linearVelocity *= speedMultiplier; 
            }

            // 2. Разбиваем витрину (прячем графику)
            if (glassModel != null) glassModel.SetActive(false);
            if (vaseModel != null) vaseModel.SetActive(false);

            // --- БЛОК ИНТЕГРАЦИИ ---
            /*
            ILootInventory loot = other.GetComponentInParent<ILootInventory>();
            if (loot != null)
            {
                loot.AddLoot(rewardValue);
            }
            */
            // -----------------------
        }
    }
}