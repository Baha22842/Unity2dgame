using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("Режимы Движения")]
    [Tooltip("Если этот массив заполнен, платформа будет двигаться по этим точкам. Если пуст - используется локальное смещение (Move Offset).")]
    public Transform[] waypoints;
    
    [Tooltip("Локальное смещение относительно начальной точки (используется, если массив точек выше пуст).")]
    public Vector2 moveOffset = new Vector2(5f, 0f);

    [Header("Параметры Движения")]
    [Tooltip("Скорость перемещения платформы.")]
    public float speed = 2.5f;
    
    [Tooltip("Время ожидания на крайних точках перед началом обратного пути (в секундах).")]
    public float waitTime = 1f;
    
    [Tooltip("Плавное замедление и ускорение при приближении к точкам (SmoothStep).")]
    public bool smoothMovement = true;

    private Rigidbody2D rb;
    private Vector3[] _calculatedPoints;
    private int _currentPointIndex = 0;
    private int _nextPointIndex = 1;
    private float _waitTimer = 0f;
    private float _t = 0f; // Параметр интерполяции для плавного движения
    private Vector3 _startPosition;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Настраиваем Rigidbody2D для правильного и плавного кинематического движения
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Полностью убирает дрожание камеры/персонажа!
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Блокируем вращение

        _startPosition = transform.position;

        // Инициализируем точки пути
        if (waypoints != null && waypoints.Length > 0)
        {
            _calculatedPoints = new Vector3[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    _calculatedPoints[i] = waypoints[i].position;
                }
                else
                {
                    _calculatedPoints[i] = _startPosition;
                }
            }
        }
        else
        {
            // Если массив точек пуст, создаем простой путь туда-обратно на основе смещения
            _calculatedPoints = new Vector3[2];
            _calculatedPoints[0] = _startPosition;
            _calculatedPoints[1] = _startPosition + (Vector3)moveOffset;
        }

        transform.position = _calculatedPoints[0];
        _currentPointIndex = 0;
        _nextPointIndex = 1;
    }

    private void FixedUpdate()
    {
        if (_calculatedPoints == null || _calculatedPoints.Length < 2) return;

        // Обработка задержки на точках
        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 currentTarget = _calculatedPoints[_nextPointIndex];
        Vector3 startPos = _calculatedPoints[_currentPointIndex];
        
        float distance = Vector3.Distance(startPos, currentTarget);
        if (distance < 0.01f)
        {
            AdvanceWaypoint();
            return;
        }

        float step = speed * Time.fixedDeltaTime;
        Vector3 nextPos;

        if (smoothMovement)
        {
            // Вычисляем интерполяцию с плавным ускорением и замедлением (SmoothStep)
            _t += step / distance;
            _t = Mathf.Clamp01(_t);
            
            float smoothT = _t * _t * (3f - 2f * _t); // Магическая формула SmoothStep
            nextPos = Vector3.Lerp(startPos, currentTarget, smoothT);

            if (_t >= 1f)
            {
                nextPos = currentTarget;
                _t = 0f;
                _waitTimer = waitTime;
                AdvanceWaypoint();
            }
        }
        else
        {
            // Обычное линейное движение
            nextPos = Vector3.MoveTowards(transform.position, currentTarget, step);

            if (Vector3.Distance(nextPos, currentTarget) < 0.01f)
            {
                nextPos = currentTarget;
                _waitTimer = waitTime;
                AdvanceWaypoint();
            }
        }

        // Перемещаем платформу через физический движок (rb.MovePosition)
        rb.MovePosition(nextPos);
    }

    private void AdvanceWaypoint()
    {
        _currentPointIndex = _nextPointIndex;
        _nextPointIndex = (_nextPointIndex + 1) % _calculatedPoints.Length;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Проверяем, что объект приземлился сверху на платформу, а не ударился снизу/сбоку
        foreach (ContactPoint2D contact in collision.contacts)
        {
            // Нормаль направлена вниз относительно объекта на платформе (нормаль y близка к -1)
            if (contact.normal.y < -0.5f)
            {
                if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Box"))
                {
                    collision.transform.SetParent(transform);
                }
                break;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Box"))
        {
            // Открепляем объект только если его родителем в данный момент является эта платформа
            if (collision.transform.parent == transform)
            {
                collision.transform.SetParent(null);
            }
        }
    }

    // Отрисовка пути платформы в окне сцены Unity
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        if (waypoints != null && waypoints.Length > 0)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawSphere(waypoints[i].position, 0.15f);
                    int next = (i + 1) % waypoints.Length;
                    if (waypoints[next] != null)
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
                    }
                }
            }
        }
        else
        {
            // Рисуем линию смещения от начальной точки
            Vector3 start = Application.isPlaying ? _startPosition : transform.position;
            Vector3 end = start + (Vector3)moveOffset;

            Gizmos.DrawSphere(start, 0.15f);
            Gizmos.DrawSphere(end, 0.15f);
            Gizmos.DrawLine(start, end);
        }
    }
}
