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
    private Vector3 _initialPosition;

    private void Awake()
    {
        _initialPosition = transform.position;
    }

    private void Start()
    {
        string id = GetUniqueID();
        if (IsCollected(id))
        {
            Destroy(gameObject);
        }
    }

    private string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private string GetUniqueID()
    {
        // Уникальный ID на основе имени сцены, пути в иерархии и начальных координат с 1 знаком после запятой
        return string.Format("{0}_{1}_{2:F1}_{3:F1}", 
            gameObject.scene.name,
            GetHierarchyPath(transform),
            _initialPosition.x,
            _initialPosition.y
        );
    }

    private bool IsCollected(string id)
    {
        int slot = SaveSystem.SelectedSlot;
        string key = "Slot_" + slot + "_CollectedHearts";
        string list = PlayerPrefs.GetString(key, "");
        return list.Contains(id);
    }

    private void SaveCollection(string id)
    {
        int slot = SaveSystem.SelectedSlot;
        string key = "Slot_" + slot + "_CollectedHearts";
        string list = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(list))
        {
            list = id;
        }
        else if (!list.Contains(id))
        {
            list += "," + id;
        }
        PlayerPrefs.SetString(key, list);
        PlayerPrefs.Save();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем, что столкнулся именно игрок
        if (!other.CompareTag("Player")) return;
        if (_collected) return;

        _collected = true;

        string id = GetUniqueID();
        SaveCollection(id);

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
