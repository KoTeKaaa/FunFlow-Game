using UnityEngine;

public class LootItem : MonoBehaviour
{
    [Tooltip("Количество очков за подбор")]
    [SerializeField] private int value = 10;
    
    // Ссылка больше не сериализуется (не торчит в инспекторе), мы находим её кодом
    private LootInventory inventory;
    private bool collected;

    private void Start()
    {
        // Автоматическое подключение к единственному инвентарю на сцене
        // FindAnyObjectByType работает в Unity 2023/Unity 6 гораздо быстрее старого FindObjectOfType
        inventory = Object.FindAnyObjectByType<LootInventory>();
        
        if (inventory == null)
        {
            Debug.LogWarning($"[{gameObject.name}] LootInventory не найден на сцене!");
        }
    }

    private void OnEnable()
    {
        // Восстанавливаем состояние при перезапуске уровня
        collected = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || inventory == null)
            return;

        // Проверяем, что коснулся именно игрок
        if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;

        collected = true;
        inventory.AddValue(value);
        
        // Скрываем монету (при перезапуске уровня родительский класс может просто сделать SetActive(true))
        gameObject.SetActive(false); 
    }
}