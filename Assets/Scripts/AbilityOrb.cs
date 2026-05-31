using UnityEngine;

public class AbilityOrb : MonoBehaviour
{
    public enum AbilityType
    {
        DoubleJump,
        Dash,
        HeavyAttack,
        Thrust
    }

    [Header("Настройки Способности")]
    public AbilityType abilityToUnlock;
    
    [Header("Эффекты")]
    [Tooltip("Партиклы или префаб, который появится при взятии сферы")]
    public GameObject pickupEffectPrefab;

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                // Разблокируем способность в зависимости от выбранного типа
                GameManager.Instance.UnlockAbility(abilityToUnlock.ToString());
            }

            PlayerMovement pm = collider.GetComponent<PlayerMovement>();
            if (pm != null) pm.CollectPowerUp();

            // Создаем красивый эффект (если он назначен)
            if (pickupEffectPrefab != null)
            {
                Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            }

            // Уничтожаем сферу
            Destroy(gameObject);
        }
    }
}
