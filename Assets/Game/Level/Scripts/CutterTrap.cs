using UnityEngine;

public class CutterTrap : MonoBehaviour, IPathHighlightTarget
{
    [Header("Настройки ловушки")]
    [Tooltip("Доля стоимости, которая будет потеряна. В прототипе это 35% (0.35).")]
    [Range(0f, 1f)]
    public float lossFraction = 0.35f; 

    private ILootInventory _lootInventory;
    [SerializeField] private MonoBehaviour lootSource;
    private bool _isUsed = false;
    public PathHighlightKind HighlightKind => PathHighlightKind.Hazard;
    public bool CanHighlight => !_isUsed;

    private void Start()
    {
        _lootInventory = LevelServices.Resolve<ILootInventory>(this, lootSource);
        lootSource = _lootInventory as MonoBehaviour;
    }

    private void OnEnable()
    {
        _isUsed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isUsed || _lootInventory == null) return;

        // Порез вызывается контактом мешка, а не тела вора.
        if (other.gameObject.layer == LayerMask.NameToLayer("Bag"))
        {
            _isUsed = true;
            Debug.Log($"[CutterTrap] Мешок разрезан! Потеря: {lossFraction * 100}%");
            _lootInventory.RemoveFraction(lossFraction);
        }
    }
}
