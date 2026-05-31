using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Слежение за целью")]
    public Transform target;
    
    [Tooltip("Насколько плавно камера следует за игроком (чем меньше, тем быстрее).")]
    public float smoothTime = 0.12f;

    [Header("Смещение камеры")]
    [Tooltip("Смещение камеры относительно игрока (например, для того, чтобы смотреть чуть вперед или вверх).")]
    public Vector2 offset = Vector2.zero;

    private PolygonCollider2D _bounds;
    private Camera _cam;
    private Vector3 _currentVelocity = Vector3.zero;

    private void Start()
    {
        _cam = GetComponent<Camera>();
    }

    /// <summary>
    /// Задает границы комнаты для ограничения камеры
    /// </summary>
    public void SetBounds(PolygonCollider2D newBounds)
    {
        _bounds = newBounds;
    }

    private void LateUpdate()
    {
        // Сбрасываем цель, если она уничтожена или указывает на префаб
        if (target != null && !target.gameObject.scene.IsValid())
        {
            target = null;
        }

        // Если цель не назначена — ищем игрока
        if (target == null)
        {
            PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
            if (player == null)
            {
                #pragma warning disable CS0618
                player = FindObjectOfType<PlayerMovement>();
                #pragma warning restore CS0618
            }

            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }
        }

        if (target == null) return;

        // Если у нас еще нет границ комнаты, пытаемся найти их автоматически
        if (_bounds == null)
        {
            AutoDetectBounds();
        }

        // Целевая позиция камеры (с учетом смещения offset)
        Vector3 targetPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        // Ограничиваем целевую позицию в пределах полигона комнаты
        if (_bounds != null && _cam != null)
        {
            Bounds bounds = _bounds.bounds;

            // Вычисляем размеры экрана камеры в мировых координатах
            float camHeight = _cam.orthographicSize;
            float camWidth = camHeight * _cam.aspect;

            // Рассчитываем допустимые границы положения центра камеры
            float minX = bounds.min.x + camWidth;
            float maxX = bounds.max.x - camWidth;
            float minY = bounds.min.y + camHeight;
            float maxY = bounds.max.y - camHeight;

            // Если размер экрана камеры больше, чем сама комната — центрируем её по X
            if (minX > maxX)
            {
                targetPos.x = bounds.center.x;
            }
            else
            {
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
            }

            // То же самое по Y
            if (minY > maxY)
            {
                targetPos.y = bounds.center.y;
            }
            else
            {
                targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
            }
        }

        // Если камера находится слишком далеко от целевой позиции (например, при спавне или телепортации),
        // мгновенно переносим её туда без долгого скольжения
        if (Vector3.Distance(transform.position, targetPos) > 15f)
        {
            transform.position = targetPos;
            _currentVelocity = Vector3.zero;
        }
        else
        {
            // Плавное следование с использованием SmoothDamp (очень плавно и без дерганий!)
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _currentVelocity, smoothTime);
        }
    }

    private void AutoDetectBounds()
    {
        #pragma warning disable CS0618
        MapRoomTrigger[] triggers = FindObjectsOfType<MapRoomTrigger>();
        #pragma warning restore CS0618

        if (triggers.Length > 0 && target != null)
        {
            // 1. Ищем комнату, в которой стоит игрок
            foreach (var trigger in triggers)
            {
                if (trigger != null && trigger.roomCameraBounds != null)
                {
                    if (trigger.roomCameraBounds.OverlapPoint(target.position))
                    {
                        _bounds = trigger.roomCameraBounds;
                        Debug.Log($"[CameraFollow] Автоматически определили комнату: {_bounds.gameObject.name}");
                        return;
                    }
                }
            }

            // 2. Резервный вариант: выбираем ближайшую к игроку комнату
            float minDst = float.MaxValue;
            MapRoomTrigger closest = null;
            foreach (var trigger in triggers)
            {
                if (trigger != null && trigger.roomCameraBounds != null)
                {
                    float dst = Vector3.Distance(target.position, trigger.transform.position);
                    if (dst < minDst)
                    {
                        minDst = dst;
                        closest = trigger;
                    }
                }
            }

            if (closest != null)
            {
                _bounds = closest.roomCameraBounds;
                Debug.Log($"[CameraFollow] Игрок вне комнат, выбрали ближайшую: {_bounds.gameObject.name}");
            }
        }
    }
}
