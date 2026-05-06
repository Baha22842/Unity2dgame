using UnityEngine;

public class DestructibleWall : MonoBehaviour, IHittable
{
    [Header("Настройки разрушения")]
    [Tooltip("Опционально: Эффект частиц, который появится при разрушении")]
    public GameObject destructionEffectPrefab;

    public void OnHit(bool isHeavyAttack = false)
    {
        // Легкие атаки не могут пробить эту стену!
        if (!isHeavyAttack) 
        {
            return;
        }

        // Если у нас есть префаб эффекта разрушения (например, куски камней) - создаем его
        if (destructionEffectPrefab != null)
        {
            Instantiate(destructionEffectPrefab, transform.position, Quaternion.identity);
        }

        // Удаляем саму стену
        Destroy(gameObject);
    }
}
