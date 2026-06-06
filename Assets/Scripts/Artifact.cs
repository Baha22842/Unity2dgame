using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Artifact : MonoBehaviour
{
    [Header("Эффекты при подборе (Опционально)")]
    public GameObject collectEffect; // сюда можно кинуть Particle System искр

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
        string key = "Slot_" + slot + "_CollectedArtifacts";
        string list = PlayerPrefs.GetString(key, "");
        return list.Contains(id);
    }

    private void SaveCollection(string id)
    {
        int slot = SaveSystem.SelectedSlot;
        string key = "Slot_" + slot + "_CollectedArtifacts";
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (_collected) return;
            _collected = true;

            string id = GetUniqueID();
            SaveCollection(id);

            // Говорим менеджеру, что подобрали ключ
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CollectArtifact();
            }

            // Создаем искры, если они есть
            if (collectEffect != null)
            {
                Instantiate(collectEffect, transform.position, Quaternion.identity);
            }

            // Опционально: звук подбора (можно добавить AudioSource)

            // Уничтожаем объект ключа на сцене
            Destroy(gameObject);
        }
    }
}
