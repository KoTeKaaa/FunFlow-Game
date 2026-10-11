using UnityEngine;

public class LootBagVisual : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Transform target; // За кем следуем (TestPlayer)
    [SerializeField] private MonoBehaviour inventorySource; // Сюда перетащим LootManager

    [Header("Следование")]
    [SerializeField] private Vector3 offset = new Vector3(-0.8f, -0.1f, 0f);

    [Header("Размер")]
    [SerializeField] private float minScale = 0.4f;
    [SerializeField] private float maxScale = 1.2f;

    private ILootInventory _inventory;

    private void Awake()
    {
        _inventory = inventorySource as ILootInventory;

        if (_inventory == null)
        {
            Debug.LogError("LootBagVisual: inventorySource не реализует ILootInventory");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_inventory != null)
            _inventory.Changed += UpdateVisual;

        UpdateVisual();
    }

    private void OnDisable()
    {
        if (_inventory != null)
            _inventory.Changed -= UpdateVisual;
    }

    private void LateUpdate()
    {
        if (target != null)
            transform.position = target.position + offset;
    }

    private void UpdateVisual()
    {
        if (_inventory == null) return;

        float scale = Mathf.Lerp(minScale, maxScale, _inventory.NormalizedWeight);
        transform.localScale = Vector3.one * scale;
    }
}