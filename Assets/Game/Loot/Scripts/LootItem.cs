using UnityEngine;

public class LootItem : MonoBehaviour
{
    [Tooltip("Количество очков за подбор")]
    [SerializeField] private int value = 10;
    
    [SerializeField] private LootInventory inventory;
    private bool collected;

    private void Start() => inventory = LevelServices.Resolve<LootInventory>(this, inventory);

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
