using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(Collider2D))]
public class Enemy : MonoBehaviour
{
    public enum EnemyType { PatrolGround, FlyChase, StaticShooter }
    public enum EnemyState { Idle, Move, Hit, Dead, Attack, Chase }

    [Header("Base Settings")]
    [SerializeField] private EnemyType enemyType = EnemyType.PatrolGround;
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int attackDamage = 1;

    [Header("Knockback Settings")]
    [SerializeField] private Vector2 knockbackForce = new Vector2(2f, 2f);
    [Tooltip("Если враг улетает слишком далеко, поставь значения меньше (например 1, 1). Если улетает в бездну (в яму), сделай Y побольше, а X поменьше.")]

    [Header("Movement Settings")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float idleTimeAtEdge = 1f;
    [SerializeField] private float aggroRange = 6f;
    
    [Header("Ground Patrol (Only for PatrolGround)")]
    [Tooltip("Пустой объект (Дочерний), находящийся впереди и внизу врага (УСТАРЕЛО, теперь рассчитывается автоматически!)")]
    [SerializeField] private Transform edgeCheck;
    [Tooltip("Пустой объект (Дочерний), находящийся перед лицом врага (УСТАРЕЛО, теперь рассчитывается автоматически!)")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float checkDistance = 0.5f;

    [Header("Melee Attack Settings")]
    [SerializeField] private Transform meleeAttackPoint;
    [SerializeField] private Vector2 meleeAttackSize = new Vector2(1.2f, 0.7f);
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 2f;
    [Tooltip("Время, которое враг стоит на месте во время удара. Должно быть чуть больше или равно длине анимации GolemAttack.")]
    [SerializeField] private float attackDuration = 1f;
    [SerializeField] private LayerMask playerLayer;
    private float _attackCooldownTimer;

    [Header("Fly Chase (Only for FlyChase)")]
    [SerializeField] private float chaseRange = 10f;

    [Header("Static Shooter (Only for StaticShooter)")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 2f;
    private float _fireTimer;

    // Имена стейтов в аниматоре Голема
    private const string ANIM_IDLE  = "GolemIdle";
    private const string ANIM_WALK  = "GolemWalk";
    private const string ANIM_HIT   = "GolemHit";
    private const string ANIM_DEATH = "GolemDeath";
    private const string ANIM_ATTACK = "GolemAttack";

    private EnemyState _currentState;
    private int _currentHealth;
    private float _stateTimer;
    private int _facingDirection = 1; // 1 = Право, -1 = Лево
    
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Transform _playerTransform;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        _currentHealth = maxHealth;

        if (_animator != null)
        {
            _animator.applyRootMotion = false; // Фикс бага: аниматор блокировал движение физики!
        }

        // Синхронизируем направление с начальным масштабом
        _facingDirection = (transform.localScale.x >= 0) ? 1 : -1;

        // Задаем скольжение по умолчанию, если есть Rigidbody
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null && _rb != null && _rb.sharedMaterial != null)
        {
            boxCol.sharedMaterial = _rb.sharedMaterial;
        }
    }

    private void Start()
    {
        // Ищем игрока — нужен всем типам врагов для преследования и атак
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _playerTransform = p.transform;

        if (enemyType == EnemyType.FlyChase) _rb.gravityScale = 0f; // Летающие враги не падают

        ChangeState(EnemyState.Move);
    }

    private void ChangeState(EnemyState newState)
    {
        if (_currentState == EnemyState.Dead) return;

        _currentState = newState;
        _stateTimer = 0f;

        if (_currentState == EnemyState.Idle)
        {
            // Останавливаемся
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            _stateTimer = idleTimeAtEdge;
            PlayAnim(ANIM_IDLE);
        }
        else if (_currentState == EnemyState.Move)
        {
            PlayAnim(ANIM_WALK);
        }
        else if (_currentState == EnemyState.Hit)
        {
            _stateTimer = 0.4f; // Время "оглушения" после получения урона
            
            // Если отбрасывание отключено, полностью обнуляем скорость, 
            // чтобы враг стоял как вкопанный и не сдвигался от коллайдера игрока
            if (knockbackForce == Vector2.zero)
            {
                _rb.linearVelocity = Vector2.zero;
            }
            
            PlayAnim(ANIM_HIT);
        }
        else if (_currentState == EnemyState.Attack)
        {
            _stateTimer = attackDuration; // Ждем полное время анимации атаки
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            PlayAnim(ANIM_ATTACK);
        }
        else if (_currentState == EnemyState.Chase)
        {
            PlayAnim(ANIM_WALK);
        }
        else if (_currentState == EnemyState.Dead)
        {
            Die();
        }
    }

    private void Update()
    {
        if (_currentState == EnemyState.Dead) return;

        if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.deltaTime;

        if (_currentState == EnemyState.Hit || _currentState == EnemyState.Attack)
        {
            // Находимся в состоянии стана (отбрасывания) или бьем игрока
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f) ChangeState(EnemyState.Move);
            return;
        }

        if (_currentState == EnemyState.Idle)
        {
            // Стоим на краю обрыва или у стены
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                Flip();
                ChangeState(EnemyState.Move);
            }
            return;
        }

        if (_currentState == EnemyState.Move)
        {
            if (enemyType == EnemyType.PatrolGround) UpdateGroundPatrol();
            else if (enemyType == EnemyType.FlyChase) UpdateFlyChase();
            else if (enemyType == EnemyType.StaticShooter) UpdateStaticShooter();
        }
        else if (_currentState == EnemyState.Chase)
        {
            if (enemyType == EnemyType.PatrolGround) UpdateGroundChase();
        }
    }

    private void UpdateGroundPatrol()
    {
        // 1. Проверяем, не в зоне ли агрессии игрок — переходим в Chase
        if (_playerTransform != null)
        {
            float distToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            if (distToPlayer <= aggroRange)
            {
                ChangeState(EnemyState.Chase);
                return;
            }
        }

        // 2. Обычный патруль
        _rb.linearVelocity = new Vector2(_facingDirection * speed, _rb.linearVelocity.y);

        bool isGroundAhead = CheckGroundAhead();
        bool isWallAhead = CheckWallAhead();

        if (!isGroundAhead || isWallAhead)
        {
            ChangeState(EnemyState.Idle);
        }
    }

    private void UpdateGroundChase()
    {
        if (_playerTransform == null)
        {
            ChangeState(EnemyState.Move);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, _playerTransform.position);

        // Игрок убежал слишком далеко — возвращаемся к патрулю
        if (distToPlayer > aggroRange * 1.5f)
        {
            ChangeState(EnemyState.Move);
            return;
        }

        // Проверяем атаку ближнего боя
        if (_attackCooldownTimer <= 0f && meleeAttackPoint != null)
        {
            RaycastHit2D playerHit = Physics2D.BoxCast(meleeAttackPoint.position, meleeAttackSize, 0f, Vector2.right * _facingDirection, attackRange, playerLayer);
            if (playerHit.collider != null)
            {
                ChangeState(EnemyState.Attack);
                _attackCooldownTimer = attackCooldown;
                return;
            }
        }

        // Поворачиваемся к игроку
        float dirToPlayer = Mathf.Sign(_playerTransform.position.x - transform.position.x);
        if ((int)dirToPlayer != _facingDirection)
        {
            Flip();
        }

        // Проверяем пропасть/стену перед тем как бежать
        bool isGroundAhead = CheckGroundAhead();
        bool isWallAhead = CheckWallAhead();

        if (!isGroundAhead || isWallAhead)
        {
            // Враг не самоубийца — стоим у края и ждем
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        // Бежим к игроку быстрее, чем при патруле
        _rb.linearVelocity = new Vector2(_facingDirection * chaseSpeed, _rb.linearVelocity.y);
    }

    // --- Вспомогательные методы для проверки земли и стен ---
    private Collider2D GetActiveCollider()
    {
        // Сначала пытаемся получить BoxCollider2D (наш основной стабильный коллайдер)
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null && boxCol.enabled) return boxCol;

        // Если его нет или он выключен, ищем любой другой активный неколлайдер-триггер
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            if (col.enabled && !col.isTrigger)
            {
                return col;
            }
        }
        return null;
    }

    private bool CheckGroundAhead()
    {
        Collider2D col = GetActiveCollider();
        if (col == null) return true;

        // Находим реальную нижнюю грань активного коллайдера
        float bottomY = col.bounds.min.y;
        float halfWidth = col.bounds.size.x / 2f;
        
        // Проверяем точку впереди границы коллайдера голема
        float checkX = transform.position.x + _facingDirection * (halfWidth + 0.15f);
        // Запускаем луч чуть выше нижней грани коллайдера (на 0.05f), чтобы он гарантированно попадал по земле
        Vector2 checkStart = new Vector2(checkX, bottomY + 0.05f);
        
        // Проверяем землю на checkDistance + 0.3f вниз для 100% надежности
        RaycastHit2D[] hits = Physics2D.RaycastAll(checkStart, Vector2.down, checkDistance + 0.3f, groundLayer);
        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
            {
                return true; // Земля есть!
            }
        }
        return false; // Край платформы!
    }

    private bool CheckWallAhead()
    {
        Collider2D col = GetActiveCollider();
        if (col == null) return false;

        float halfWidth = col.bounds.size.x / 2f;
        float checkX = transform.position.x + _facingDirection * (halfWidth + 0.15f);
        
        // Проверяем на высоте середины коллайдера
        float middleY = col.bounds.center.y;
        Vector2 checkStart = new Vector2(checkX, middleY);
        
        // Проверяем стену на checkDistance + 0.3f вперед
        RaycastHit2D[] hits = Physics2D.RaycastAll(checkStart, Vector2.right * _facingDirection, checkDistance + 0.3f, groundLayer);
        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger)
            {
                return true; // Стена!
            }
        }
        return false;
    }

    private void UpdateFlyChase()
    {
        if (_playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, _playerTransform.position);
        
        if (distance <= chaseRange)
        {
            // Летим к игроку
            Vector2 direction = (_playerTransform.position - transform.position).normalized;
            _rb.linearVelocity = direction * speed;

            // Поворачиваем спрайт
            if (direction.x > 0 && _facingDirection == -1) Flip();
            else if (direction.x < 0 && _facingDirection == 1) Flip();
        }
        else
        {
            // Игрок ушел слишком далеко
            _rb.linearVelocity = Vector2.zero;
        }
    }

    private void UpdateStaticShooter()
    {
        if (_playerTransform == null) return;
        
        // Стрелок стоит на месте, гравитация работает как обычно
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        float distance = Vector2.Distance(transform.position, _playerTransform.position);
        if (distance <= attackRange)
        {
            // Поворачиваемся к игроку
            float dir = Mathf.Sign(_playerTransform.position.x - transform.position.x);
            if (dir > 0 && _facingDirection == -1) Flip();
            else if (dir < 0 && _facingDirection == 1) Flip();

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                _fireTimer = fireRate;
                ShootProjectile(dir);
            }
        }
    }

    private void ShootProjectile(float direction)
    {
        if (projectilePrefab == null || firePoint == null) return;
        
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        
        Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
        if (projRb != null)
        {
            projRb.linearVelocity = new Vector2(direction * 7f, 0f);
        }
        
        Destroy(proj, 3f); 
    }

    private void Flip()
    {
        _facingDirection *= -1;
        // Переворачиваем через localScale, чтобы дочерние объекты тоже переместились на другую сторону
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * _facingDirection;
        transform.localScale = scale;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (_currentState == EnemyState.Dead || _currentState == EnemyState.Hit) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement pm = collision.gameObject.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.TakeDamage(transform.position);
            }
        }
    }

    // ВЫЗЫВАТЬ ИЗ ANIMATION EVENT В АНИМАЦИИ GolemAttack!
    public void TriggerAttackHitbox()
    {
        if (_currentState == EnemyState.Dead || meleeAttackPoint == null) return;

        Collider2D[] hitPlayers = Physics2D.OverlapBoxAll(meleeAttackPoint.position, meleeAttackSize, 0f, playerLayer);

        foreach (Collider2D playerCollider in hitPlayers)
        {
            PlayerMovement pm = playerCollider.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.TakeDamage(transform.position);
            }
        }
    }

    // Метод вызывается мечом игрока
    public void TakeDamage(int damage)
    {
        if (_currentState == EnemyState.Dead) return;

        _currentHealth -= damage;

        if (_currentHealth <= 0)
        {
            ChangeState(EnemyState.Dead);
        }
        else
        {
            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player != null)
            {
                float knockbackDir = Mathf.Sign(transform.position.x - player.position.x);
                _rb.linearVelocity = new Vector2(knockbackDir * knockbackForce.x, knockbackForce.y);
            }

            ChangeState(EnemyState.Hit);
        }
    }

    private void PlayAnim(string stateName)
    {
        if (_animator == null || string.IsNullOrEmpty(stateName)) return;
        _animator.Play(stateName);
    }

    private void Die()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic; // Труп не должен скользить
        
        // Отключаем абсолютно все коллайдеры при смерти врага
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
        
        // Задаем параметры аниматора для предотвращения ложных переходов
        if (_animator != null)
        {
            _animator.SetBool("IsDead", true);
            _animator.ResetTrigger("Hit");
            _animator.ResetTrigger("Attack");
        }
        
        PlayAnim(ANIM_DEATH);
        
        // Замораживаем аниматор перед уничтожением объекта
        StartCoroutine(DisableAnimatorDelayed(1.1f));
        
        Destroy(gameObject, 1.5f); // Даём анимации смерти время доиграться
    }

    private IEnumerator DisableAnimatorDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_animator != null)
        {
            _animator.enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (enemyType == EnemyType.FlyChase)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, chaseRange);
        }
        else if (enemyType == EnemyType.PatrolGround)
        {
            Gizmos.color = Color.red;
            
            // Рисуем математический луч проверки земли
            Collider2D col = GetActiveCollider();
            if (col != null)
            {
                float bottomY = col.bounds.min.y;
                float halfWidth = col.bounds.size.x / 2f;
                float checkX = transform.position.x + _facingDirection * (halfWidth + 0.15f);
                Vector2 checkStart = new Vector2(checkX, bottomY + 0.05f);
                Gizmos.DrawLine(checkStart, checkStart + Vector2.down * (checkDistance + 0.3f));
            }
            
            // Рисуем зону агрессии (жёлтый)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);

            if (meleeAttackPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(meleeAttackPoint.position, meleeAttackSize);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(meleeAttackPoint.position + (Vector3)(Vector2.right * _facingDirection * attackRange), meleeAttackSize);
                Gizmos.DrawLine(meleeAttackPoint.position, meleeAttackPoint.position + (Vector3)(Vector2.right * _facingDirection * attackRange));
            }
        }
        else if (enemyType == EnemyType.StaticShooter)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (speed < 0) speed = 0;
        if (aggroRange < 0) aggroRange = 0;
        if (firePoint == null) firePoint = transform.Find("FirePoint");
        if (meleeAttackPoint == null) meleeAttackPoint = transform.Find("AttackPoint");
    }
#endif
}

