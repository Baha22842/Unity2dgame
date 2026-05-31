using UnityEngine;

public class MapRoomTrigger : MonoBehaviour
{
    [Header("Настройки Камеры")]
    [Tooltip("Ссылка на PolygonCollider2D. Если оставить пустой, скрипт автоматически найдет полигон на этом объекте или его дочерних/родительских объектах.")]
    public PolygonCollider2D roomCameraBounds;

    private GameObject _player;

    private void Awake()
    {
        // Если полигон не назначен вручную, ищем его на самом объекте, в детях или родителях
        if (roomCameraBounds == null)
        {
            roomCameraBounds = GetComponent<PolygonCollider2D>() 
                               ?? GetComponentInChildren<PolygonCollider2D>() 
                               ?? GetComponentInParent<PolygonCollider2D>();
        }

        // Переводим все коллайдеры на объекте в режим триггера в качестве резервного варианта
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (roomCameraBounds == null) return;

        // Поиск игрока на сцене (по тегу или по компоненту)
        if (_player == null)
        {
            _player = GameObject.FindGameObjectWithTag("Player");
            if (_player == null)
            {
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null) _player = pm.gameObject;
            }
        }

        if (_player != null)
        {
            // Математически (без коллизий!) проверяем, находится ли точка игрока внутри полигона комнаты
            if (roomCameraBounds.OverlapPoint(_player.transform.position))
            {
                if (CameraManager.Instance != null)
                {
                    CameraManager.Instance.SetRoomBounds(roomCameraBounds);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandlePlayerEntry(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandlePlayerEntry(collision);
    }

    private void HandlePlayerEntry(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.GetComponent<PlayerMovement>() != null)
        {
            if (CameraManager.Instance != null && roomCameraBounds != null)
            {
                CameraManager.Instance.SetRoomBounds(roomCameraBounds);
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Находим PolygonCollider2D для отрисовки точного контура
        PolygonCollider2D poly = roomCameraBounds != null ? roomCameraBounds : GetComponent<PolygonCollider2D>();
        if (poly == null) poly = GetComponentInChildren<PolygonCollider2D>();
        if (poly == null) poly = GetComponentInParent<PolygonCollider2D>();

        if (poly != null)
        {
            Gizmos.color = new Color(0f, 0.8f, 0.2f, 0.5f); // Яркий зеленый контур
            for (int i = 0; i < poly.pathCount; i++)
            {
                Vector2[] path = poly.GetPath(i);
                for (int j = 0; j < path.Length; j++)
                {
                    Vector3 p1 = transform.TransformPoint(path[j]);
                    Vector3 p2 = transform.TransformPoint(path[(j + 1) % path.Length]);
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }
    }
}
