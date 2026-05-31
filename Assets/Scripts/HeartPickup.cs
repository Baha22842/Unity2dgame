using UnityEngine;

public class HeartPickup : MonoBehaviour
{
    [Header("Настройки Лечения")]
    [Tooltip("Количество восстанавливаемых сердец")]
    [SerializeField] private int healAmount = 1;

    [Tooltip("Собирать сердце только если игрок ранен?")]
    [SerializeField] private bool collectOnlyWhenHurt = true;

    [Tooltip("Очки, если здоровье полно (работает только если collectOnlyWhenHurt = false)")]
    [SerializeField] private int scoreIfFullHealth = 50;

    [Header("Эффекты")]
    [Tooltip("Эффект (партиклы), создаваемый при сборе сердца")]
    [SerializeField] private GameObject pickupEffectPrefab;

    private bool _collected = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_collected) return;

        _collected = true;

        if (GameManager.Instance != null)
        {
            bool isFull = GameManager.Instance.IsHealthFull;

            if (isFull)
            {
                if (collectOnlyWhenHurt)
                {
                    // Игрок здоров, и мы настроили не подбирать сердце зря
                    return;
                }
                else
                {
                    // Игрок здоров, но подбор разрешен -> конвертируем сердце в очки!
                    GameManager.Instance.AddScore(scoreIfFullHealth);
                    Debug.Log($"Здоровье полно! Сердце сконвертировано в +{scoreIfFullHealth} очков.");
                }
            }
            else
            {
                // Игрок ранен -> лечим его
                GameManager.Instance.Heal(healAmount);
                Debug.Log($"Игрок вылечен на {healAmount} хп!");
            }
        }

        // Спавним красивый эффект сбора
        if (pickupEffectPrefab != null)
        {
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
        }

        // Уничтожаем сердце
        Destroy(gameObject);
    }
}
