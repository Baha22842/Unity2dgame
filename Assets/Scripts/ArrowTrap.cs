using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ArrowTrap : MonoBehaviour
{
    [Header("Настройки Стрельбы")]
    [Tooltip("Префаб стрелы (должен иметь компонент Projectile)")]
    [SerializeField] private GameObject arrowPrefab;

    [Tooltip("Точка, откуда вылетает стрела")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Направление стрельбы в локальных координатах (по умолчанию влево, так как спрайт смотрит влево)")]
    [SerializeField] private Vector2 shootDirection = Vector2.left;

    [Tooltip("Использовать поворот (Rotation/Scale) объекта для направления стрельбы? (Рекомендуется, чтобы просто крутить ловушку на сцене)")]
    [SerializeField] private bool useRotationAsDirection = true;

    [Tooltip("Перезарядка между выстрелами")]
    [SerializeField] private float shootCooldown = 2.5f;

    [Header("Обнаружение игрока")]
    [Tooltip("Дистанция обнаружения игрока")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Слои препятствий (стены), блокирующие видимость")]
    [SerializeField] private LayerMask obstacleLayers;

    private Animator _anim;
    private Transform _player;
    private float _cooldownTimer;
    private Coroutine _trapCoroutine;

    // Свойство для получения актуального направления стрельбы
    public Vector2 CurrentShootDirection
    {
        get
        {
            if (useRotationAsDirection)
            {
                // Переводим локальный вектор "влево" (по умолчанию для ловушки) в мировое пространство.
                // Это автоматически учитывает Z-поворот (rotation) и зеркальное отражение (scale.x = -1) в Unity!
                return ((Vector2)transform.TransformDirection(Vector3.left)).normalized;
            }
            return shootDirection.normalized;
        }
    }

    private void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (_anim != null)
        {
            _anim.speed = 0f; // Приостанавливаем аниматор на старте в закрытом состоянии
            _anim.Play("PopUpTrap", 0, 0f);
        }
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _player = p.transform;
        }

        if (firePoint == null)
        {
            firePoint = transform; // Fallback
        }
    }

    private void Update()
    {
        if (_player == null)
        {
            // Динамический поиск игрока, если на старте его не было
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
            else return;
        }

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }

        // Если перезарядка прошла и игрок находится в зоне видимости
        if (_cooldownTimer <= 0f && IsPlayerInSight())
        {
            TriggerTrap();
        }
    }

    private bool IsPlayerInSight()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        if (distanceToPlayer > detectionRange) return false;

        // Проверяем направление: находится ли игрок в секторе обстрела ловушки
        Vector2 toPlayer = (_player.position - transform.position).normalized;
        float dotProduct = Vector2.Dot(toPlayer, CurrentShootDirection);

        // Угол конуса видимости ~90 градусов (dot > 0.7)
        if (dotProduct < 0.7f) return false;

        // Проверяем прямую линию видимости (нет ли стен между ловушкой и игроком)
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            toPlayer,
            distanceToPlayer,
            obstacleLayers
        );

        return hit.collider == null; // Игрок виден, если луч ни во что не врезался
    }

    private void TriggerTrap()
    {
        _cooldownTimer = shootCooldown;

        if (_trapCoroutine != null)
        {
            StopCoroutine(_trapCoroutine);
        }
        _trapCoroutine = StartCoroutine(PlayTrapRoutine());
    }

    private System.Collections.IEnumerator PlayTrapRoutine()
    {
        if (_anim != null)
        {
            _anim.speed = 1f;
            _anim.Play("PopUpTrap", 0, 0f);

            // Ждем один кадр для обновления стейта аниматора
            yield return null;

            float duration = _anim.GetCurrentAnimatorStateInfo(0).length;
            if (duration <= 0.05f) duration = 1.0f; // Безопасный дефолт

            yield return new WaitForSeconds(duration);

            // По завершении залпа возвращаем ловушку к первому закрытому кадру и останавливаем
            _anim.speed = 0f;
            _anim.Play("PopUpTrap", 0, 0f);
        }
    }

    private void OnDisable()
    {
        if (_trapCoroutine != null)
        {
            StopCoroutine(_trapCoroutine);
            _trapCoroutine = null;
        }
    }

    /// <summary>
    /// Этот метод вызывается через Animation Event на конкретном кадре анимации PopUpTrap!
    /// </summary>
    public void ShootArrow()
    {
        // Каждая выпущенная стрела обновляет таймер перезарядки.
        // Это гарантирует, что кулдаун начнется строго после ПОСЛЕДНЕГО выстрела в залпе!
        _cooldownTimer = shootCooldown;

        if (arrowPrefab == null || firePoint == null) return;

        // Создаем стрелу в точке вылета без ручного вращения (его сделает сам Projectile)
        GameObject arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);

        // Задаем направление полета через компонент Projectile
        Projectile proj = arrow.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.SetDirection(CurrentShootDirection);
        }
        else
        {
            Debug.LogWarning("[ArrowTrap] Префаб стрелы не имеет компонента Projectile!", this);
        }

        Debug.Log("[ArrowTrap] Стрела выпущена через Animation Event!");
    }

    private void OnDrawGizmosSelected()
    {
        // Отрисовка линии стрельбы в редакторе для удобства настройки
        if (firePoint != null)
        {
            Vector2 dir = CurrentShootDirection;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(firePoint.position, dir * detectionRange);
            
            // Рисуем конус видимости
            Gizmos.color = Color.yellow;
            Vector3 leftBoundary = Quaternion.Euler(0, 0, 45) * dir;
            Vector3 rightBoundary = Quaternion.Euler(0, 0, -45) * dir;
            Gizmos.DrawRay(firePoint.position, leftBoundary * detectionRange);
            Gizmos.DrawRay(firePoint.position, rightBoundary * detectionRange);
        }
    }
}
