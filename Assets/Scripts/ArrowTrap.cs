using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ArrowTrap : MonoBehaviour
{
    [Header("Настройки Стрельбы")]
    [Tooltip("Префаб стрелы (должен иметь компонент Projectile)")]
    [SerializeField] private GameObject arrowPrefab;

    [Tooltip("Точка, откуда вылетает стрела")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Направление стрельбы")]
    [SerializeField] private Vector2 shootDirection = Vector2.left;

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

    private void Start()
    {
        _anim = GetComponent<Animator>();
        
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
        if (_player == null) return;

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
        float dotProduct = Vector2.Dot(toPlayer, shootDirection.normalized);

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

        // Запускаем анимацию открытия и выстрела ловушки
        if (_anim != null)
        {
            _anim.SetTrigger("Shoot");
        }
    }

    /// <summary>
    /// Этот метод вызывается через Animation Event на конкретном кадре анимации PopUpTrap!
    /// </summary>
    public void ShootArrow()
    {
        if (arrowPrefab == null || firePoint == null) return;

        // Создаем стрелу в точке вылета без ручного вращения (его сделает сам Projectile)
        GameObject arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);

        // Задаем направление полета через компонент Projectile
        Projectile proj = arrow.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.SetDirection(shootDirection);
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
            Gizmos.color = Color.red;
            Gizmos.DrawRay(firePoint.position, shootDirection.normalized * detectionRange);
            
            // Рисуем конус видимости
            Gizmos.color = Color.yellow;
            Vector3 leftBoundary = Quaternion.Euler(0, 0, 45) * shootDirection.normalized;
            Vector3 rightBoundary = Quaternion.Euler(0, 0, -45) * shootDirection.normalized;
            Gizmos.DrawRay(firePoint.position, leftBoundary * detectionRange);
            Gizmos.DrawRay(firePoint.position, rightBoundary * detectionRange);
        }
    }
}
