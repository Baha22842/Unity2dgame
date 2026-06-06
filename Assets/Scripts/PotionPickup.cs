using UnityEngine;

public class PotionPickup : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Количество добавляемых зелий")]
    [SerializeField] private int potionAmount = 1;

    [Tooltip("Ценность в духах, если сумка зелий уже полна")]
    [SerializeField] private int spiritsIfFull = 50;

    [Header("Эффекты")]
    [Tooltip("Эффект при сборе зелья")]
    [SerializeField] private GameObject pickupEffectPrefab;

    private bool _collected = false;

    private void Awake()
    {
        // Убеждаемся, что на объекте есть коллайдер с включенным триггером, так как префаб может быть сохранен без него
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(0.5f, 0.5f);
        }
        else
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_collected) return;

        if (GameManager.Instance != null)
        {
            // Блокируем повторный сбор
            _collected = true;

            if (GameManager.Instance.potionsCount >= GameManager.Instance.maxPotions)
            {
                // Сумка полна -> конвертируем в духов (монеты)!
                GameManager.Instance.AddScore(spiritsIfFull);
                Debug.Log($"[ЗЕЛЬЕ] Сумка зелий полна! Получено +{spiritsIfFull} духов вместо зелья.");
            }
            else
            {
                // Сумка не полна -> добавляем зелье
                GameManager.Instance.potionsCount += potionAmount;
                if (GameManager.Instance.potionsCount > GameManager.Instance.maxPotions)
                {
                    GameManager.Instance.potionsCount = GameManager.Instance.maxPotions;
                }

                // Сохраняем состояние зелий
                string potionsKey = "Slot_" + SaveSystem.SelectedSlot + "_Potions";
                PlayerPrefs.SetInt(potionsKey, GameManager.Instance.potionsCount);
                PlayerPrefs.Save();

                GameManager.Instance.UpdateScoreUI();
                Debug.Log($"[ЗЕЛЬЕ] Зелье целительного духа собрано! Текущее количество зелий: {GameManager.Instance.potionsCount}/{GameManager.Instance.maxPotions}");
            }

            // Спавним красивый эффект сбора
            if (pickupEffectPrefab != null)
            {
                Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            }

            // Уничтожаем зелье на сцене
            Destroy(gameObject);
        }
    }
}
