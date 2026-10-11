using UnityEngine;

public class LootItem : MonoBehaviour
{
    [SerializeField] private int value = 10;
    [SerializeField] private LootInventory inventory;

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        if (other.gameObject.layer !=
            LayerMask.NameToLayer("Player"))
            return;

        if (inventory == null)
            return;

        collected = true;

        inventory.AddValue(value);

        gameObject.SetActive(false);
    }
}