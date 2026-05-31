using UnityEngine;

public class MaxHealthUpgrade : MonoBehaviour
{
    [Header("Настройки Улучшения")]
    [Tooltip("На сколько ячеек увеличить максимальное здоровье")]
    [SerializeField] private int increaseAmount = 1;

    [Header("Эффекты")]
    [Tooltip("Красивый эффект (партиклы), создаваемый при сборе предмета")]
    [SerializeField] private GameObject pickupEffectPrefab;

    private bool _collected = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем, что столкнулся именно игрок
        if (!other.CompareTag("Player")) return;
        if (_collected) return;

        _collected = true;

        if (GameManager.Instance != null)
        {
            // Увеличиваем максимальное здоровье игрока на всю игру
            GameManager.Instance.IncreaseMaxHealth(increaseAmount);
        }

        // Спавним красивый визуальный эффект сбора
        if (pickupEffectPrefab != null)
        {
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
        }

        // Уничтожаем объект улучшения
        Destroy(gameObject);
    }
}
