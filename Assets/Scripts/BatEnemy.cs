using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(Collider2D))]
public class BatEnemy : MonoBehaviour, IHittable
{
    [Header("Характеристики летучей мыши")]
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private float speed = 3f;
    [Tooltip("Скорость преследования игрока (погони)")]
    [SerializeField] private float chaseSpeed = 2f;

    [Header("Аггро и Зона привязки (Tether Zone)")]
    [SerializeField] private float aggroRange = 8f;
    [Tooltip("Максимальное расстояние, на которое мышь может улететь от точки спавна перед возвратом.")]
    [SerializeField] private float tetherRange = 12f;
    
    [Header("Сюжетный Сбор Духов (Крафт)")]
    [Tooltip("Префаб сферы целительного духа (твоя монета/сфера с измененным скриптом Coin)")]
    [SerializeField] private GameObject spiritOrbPrefab;
    [SerializeField] private int minSpirits = 15;
    [SerializeField] private int maxSpirits = 25;

    [Header("Настройки Атаки (Анимационный радиус)")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.5f;
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask groundLayer = 1 << 7;

    [Header("Настройки Спрайта")]
    [Tooltip("Спрайт по умолчанию смотрит вправо? (Если нарисован смотрящим влево, сними галочку)")]
    [SerializeField] private bool faceRightByDefault = true;

    [Header("Настройки Патрулирования (Патруль по воздуху)")]
    [Tooltip("Радиус патрулирования туда-сюда относительно точки спавна, когда игрок не замечен.")]
    [SerializeField] private float patrolRange = 3f;
    [Tooltip("Множитель скорости во время патрулирования (чтобы мышь летала спокойнее).")]
    [SerializeField] private float patrolSpeedMultiplier = 0.5f;
    [Tooltip("Время паузы на крайних точках патрулирования перед поворотом.")]
    [SerializeField] private float patrolWaitTime = 1.2f;

    [Header("Настройки Отбрасывания (Knockback)")]
    [SerializeField] private Vector2 knockbackForce = new Vector2(6f, 2f);

    private int _currentHealth;
    private bool _isDead = false;
    private bool _isAttacking = false;
    private float _cooldownTimer = 0f;
    private int _facingDirection = 1; // 1 = Вправо, -1 = Влево
    private Vector2 _startPosition; // Стартовая точка спавна
    private float _hitStunTimer = 0f;

    // Состояние патруля
    private int _patrolDirection = 1; // 1 = Вправо, -1 = Влево
    private float _patrolWaitTimer = 0f;
    private bool _isReturning = false;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Transform _playerTransform;
    private Color _originalColor;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        
        _currentHealth = maxHealth;
        _originalColor = _spriteRenderer.color;

        // Летучая мышь летает — отключаем гравитацию
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;

        // Назначаем материал без трения, чтобы избежать застревания/прилипания к стенам
        PhysicsMaterial2D frictionless = new PhysicsMaterial2D("FrictionlessBat") { friction = 0f, bounciness = 0f };
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.sharedMaterial = frictionless;
        }

        // Инициализируем направление взгляда на старте
        _facingDirection = 1;
    }

    private void Start()
    {
        // Запоминаем точку спавна
        _startPosition = transform.position;

        // Ищем игрока на сцене
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _playerTransform = p.transform;

        // Случайное направление патруля при старте
        _patrolDirection = Random.value > 0.5f ? 1 : -1;
    }

    private void Update()
    {
        if (_isDead) return;

        if (_hitStunTimer > 0f)
        {
            _hitStunTimer -= Time.deltaTime;
            PlayAnim("BatHit");
            return;
        }

        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        if (_isAttacking)
        {
            // Во время анимации атаки зависаем
            _rb.linearVelocity = Vector2.zero;
            PlayAnim("BatAttack");
            return;
        }

        // По умолчанию проигрываем анимацию полета в воздухе
        PlayAnim("BatFlying");

        if (_playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
        float distanceFromStart = Vector2.Distance(transform.position, _startPosition);

        // Мышь преследует только если игрок близко, мышь не в режиме возврата, и она не улетела дальше зоны привязки
        bool shouldChase = distanceToPlayer <= aggroRange && distanceFromStart <= tetherRange && !_isReturning;

        if (shouldChase)
        {
            _isReturning = false; // Сбрасываем возврат при аггро
            _patrolWaitTimer = 0f; // Сбрасываем ожидание патруля

            // Поворот к игроку с порогом (deadzone), чтобы исключить мерцание при пересечении координат
            float xDiff = _playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > 0.15f)
            {
                int dirToPlayer = (int)Mathf.Sign(xDiff);
                if (dirToPlayer != _facingDirection)
                {
                    SetFacingDirection(dirToPlayer);
                }
            }

            Vector3 checkPos = attackPoint != null ? attackPoint.position : transform.position;
            bool isPlayerInStrikeZone = Physics2D.OverlapCircle(checkPos, attackRadius, playerLayer) != null;

            if (isPlayerInStrikeZone && _cooldownTimer <= 0f)
            {
                StartCoroutine(AttackRoutine());
            }
            else
            {
                // Преследуем по воздуху
                Vector2 flyDirection = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
                _rb.linearVelocity = AdjustVelocityForObstacles(flyDirection * chaseSpeed);
            }
        }
        else
        {
            // Если мы улетели слишком далеко от базы во время погони, включаем режим возврата
            if (distanceFromStart > tetherRange)
            {
                _isReturning = true;
            }

            if (_isReturning)
            {
                // Возвращаемся к стартовой позиции
                if (distanceFromStart > 0.5f)
                {
                    Vector2 returnDirection = (_startPosition - (Vector2)transform.position).normalized;
                    // Красивое волнообразное покачивание при полете назад
                    float bobbing = Mathf.Sin(Time.time * 6f) * 0.4f;
                    _rb.linearVelocity = AdjustVelocityForObstacles(new Vector2(returnDirection.x * speed, returnDirection.y * speed + bobbing));

                    // Поворачиваемся лицом к цели полета
                    float dirToTarget = Mathf.Sign(returnDirection.x);
                    if (Mathf.Abs(returnDirection.x) > 0.1f && (int)dirToTarget != _facingDirection)
                    {
                        SetFacingDirection((int)dirToTarget);
                    }
                }
                else
                {
                    // Вернулись домой — выключаем возврат и переходим к обычному патрулированию
                    _isReturning = false;
                    _rb.linearVelocity = Vector2.zero;
                }
            }
            else
            {
                // Режим патрулирования туда-сюда
                if (_patrolWaitTimer > 0f)
                {
                    _patrolWaitTimer -= Time.deltaTime;
                    // Зависаем на месте с легким вертикальным покачиванием
                    float bobbing = Mathf.Sin(Time.time * 5f) * 0.3f;
                    _rb.linearVelocity = new Vector2(0f, bobbing);

                    // Когда время ожидания вышло, меняем направление движения
                    if (_patrolWaitTimer <= 0f)
                    {
                        _patrolDirection *= -1;
                    }
                }
                else
                {
                    // Проверяем, не вышли ли мы за границы патрулирования
                    bool reachedRightBound = _patrolDirection == 1 && transform.position.x >= _startPosition.x + patrolRange;
                    bool reachedLeftBound = _patrolDirection == -1 && transform.position.x <= _startPosition.x - patrolRange;

                    if (reachedRightBound || reachedLeftBound)
                    {
                        // Начинаем паузу перед поворотом
                        _patrolWaitTimer = patrolWaitTime;
                        _rb.linearVelocity = Vector2.zero;
                    }
                    else
                    {
                        // Летим в направлении патрулирования
                        float patrolSpeed = speed * patrolSpeedMultiplier;
                        float bobbing = Mathf.Sin(Time.time * 6f) * 0.4f;
                        _rb.linearVelocity = AdjustVelocityForObstacles(new Vector2(_patrolDirection * patrolSpeed, bobbing));

                        // Поворачиваем спрайт по направлению полета
                        if (_patrolDirection != _facingDirection)
                        {
                            SetFacingDirection(_patrolDirection);
                        }
                    }
                }
            }
        }
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _rb.linearVelocity = Vector2.zero;

        // Запуск анимации атаки
        PlayAnim("BatAttack");

        // Ждем длительность анимации атаки
        yield return new WaitForSeconds(attackDuration);

        _isAttacking = false;
        _cooldownTimer = attackCooldown;
    }

    // ВЫЗЫВАЙ ЭТОТ МЕТОД ИЗ ANIMATION EVENT В СВОЕЙ АНИМАЦИИ АТАКИ!
    // Точно так же, как у голема (TriggerAttackHitbox), чтобы нанести урон в нужный кадр анимации звуковой волны.
    public void TriggerAttackHitbox()
    {
        if (_isDead) return;

        Vector3 checkPos = attackPoint != null ? attackPoint.position : transform.position;
        Collider2D playerCol = Physics2D.OverlapCircle(checkPos, attackRadius, playerLayer);

        if (playerCol != null)
        {
            PlayerMovement pm = playerCol.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.TakeDamage(transform.position);
                Debug.Log("[BAT] Урон звуковой волной из анимации успешно нанесен игроку!");
            }
        }
    }

    private void SetFacingDirection(int direction)
    {
        if (direction == 0) return;
        _facingDirection = direction;
        
        if (_spriteRenderer != null)
        {
            // Переворачиваем галочку Flip X прямо в компоненте Sprite Renderer.
            // Это самый простой, стандартный и надежный способ для 2D-спрайтов,
            // который никогда не конфликтует с физикой Rigidbody2D или масштабом Animator!
            _spriteRenderer.flipX = faceRightByDefault ? (_facingDirection == -1) : (_facingDirection == 1);
        }

        // Зеркалируем локальное положение attackPoint по оси X, чтобы хитбокс следовал за направлением мыши
        if (attackPoint != null)
        {
            Vector3 localPos = attackPoint.localPosition;
            localPos.x = Mathf.Abs(localPos.x) * _facingDirection;
            attackPoint.localPosition = localPos;
        }
    }

    // Совместимость с мечом игрока через интерфейс IHittable
    public void OnHit(bool isHeavyAttack = false)
    {
        TakeDamage(isHeavyAttack ? 2 : 1);
    }

    public void TakeDamage(int damage)
    {
        if (_isDead) return;

        _currentHealth -= damage;

        if (_currentHealth <= 0)
        {
            ChangeStateToDead();
        }
        else
        {
            // Отдача при ударе (Knockback)
            if (_playerTransform != null)
            {
                float knockbackDir = Mathf.Sign(transform.position.x - _playerTransform.position.x);
                _rb.linearVelocity = new Vector2(knockbackDir * knockbackForce.x, knockbackForce.y);
                _hitStunTimer = 0.25f; // Оглушение на 0.25 сек, чтобы дать мыши отлететь назад
            }
            PlayAnim("BatHit");
            StartCoroutine(FlashRed());
        }
    }

    private IEnumerator FlashRed()
    {
        _spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        _spriteRenderer.color = _originalColor;
    }

    private void ChangeStateToDead()
    {
        _isDead = true;
        StopAllCoroutines();
        _spriteRenderer.color = _originalColor;

        _rb.linearVelocity = Vector2.zero;
        // Включаем гравитацию, чтобы тушка мыши упала на землю при смерти
        _rb.gravityScale = 1.5f;

        // Отключаем физические коллайдеры
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        PlayAnim("BatDie");

        // Спавним сферы духов при гибели
        if (spiritOrbPrefab != null)
        {
            int totalSpirits = Random.Range(minSpirits, maxSpirits + 1);
            int numOrbs = Random.Range(2, 4); // от 2 до 3 сфер
            int spawnedSpirits = 0;

            for (int i = 0; i < numOrbs; i++)
            {
                int orbValue = (i == numOrbs - 1) ? (totalSpirits - spawnedSpirits) : (totalSpirits / numOrbs);
                spawnedSpirits += orbValue;

                if (orbValue > 0)
                {
                    GameObject orb = Instantiate(spiritOrbPrefab, transform.position, Quaternion.identity);
                    Coin coinScript = orb.GetComponent<Coin>();
                    if (coinScript != null)
                    {
                        coinScript.value = orbValue;
                        coinScript.ApplyPopForce();
                    }
                }
            }
        }
        else
        {
            if (GameManager.Instance != null)
            {
                int backupSpirits = Random.Range(minSpirits, maxSpirits + 1);
                GameManager.Instance.AddScore(backupSpirits);
            }
        }

        Destroy(gameObject, 2f);
    }

    private void OnDrawGizmosSelected()
    {
        // Зона агрессии (жёлтая)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        // Радиус атаки (красный)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Хитбокс удара звуковой волны (фиолетовый)
        Vector3 checkPos = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(checkPos, attackRadius);

        // Зона привязки к точке спавна (синяя)
        Gizmos.color = Color.blue;
        Vector3 spawnCenter = Application.isPlaying ? (Vector3)_startPosition : transform.position;
        Gizmos.DrawWireSphere(spawnCenter, tetherRange);
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (attackPoint == null) attackPoint = transform.Find("AttackPoint");
    }
    #endif

    private void OnCollisionEnter2D(Collision2D collision)
    {
        IgnoreHazardCollision(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        IgnoreHazardCollision(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IgnoreHazardCollision(other);
    }

    private void IgnoreHazardCollision(Collider2D otherCollider)
    {
        if (otherCollider == null) return;

        string name = otherCollider.gameObject.name.ToLower();
        bool isSpikes = otherCollider.GetComponent<SpikeTrap>() != null 
                     || name.Contains("spike") 
                     || name.Contains("trap");
                     
        bool isLadder = otherCollider.CompareTag("Ladder") 
                     || name.Contains("ladder");

        if (isSpikes || isLadder)
        {
            Collider2D[] myColliders = GetComponents<Collider2D>();
            foreach (var myCol in myColliders)
            {
                if (myCol.enabled && otherCollider.enabled)
                {
                    Physics2D.IgnoreCollision(myCol, otherCollider, true);
                }
            }
        }
    }

    private Vector2 AdjustVelocityForObstacles(Vector2 desiredVelocity)
    {
        if (desiredVelocity.magnitude < 0.05f) return desiredVelocity;

        Vector2 direction = desiredVelocity.normalized;
        float currentSpeed = desiredVelocity.magnitude;
        float radius = 0.4f; // Approximate radius of the bat
        float checkDistance = 0.6f; // Lookahead distance

        RaycastHit2D hit = Physics2D.CircleCast(transform.position, radius, direction, checkDistance, groundLayer);
        if (hit.collider != null && hit.collider.gameObject != gameObject)
        {
            Vector2 normal = hit.normal;
            
            // Project flight vector onto surface tangent plane (sliding)
            Vector2 slideDirection = direction - Vector2.Dot(direction, normal) * normal;
            
            if (slideDirection.magnitude > 0.05f)
            {
                return slideDirection.normalized * currentSpeed;
            }
            else
            {
                // Perpendicular collision (e.g., straight into wall/ceiling).
                // Determine tangent vector and pick the option that moves closer to target.
                Vector2 tangent = new Vector2(-normal.y, normal.x);
                Vector2 option1 = tangent;
                Vector2 option2 = -tangent;
                
                Vector2 targetDir = direction;
                if (_playerTransform != null && Vector2.Distance(transform.position, _playerTransform.position) < aggroRange)
                {
                    targetDir = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
                }
                else if (_isReturning)
                {
                    targetDir = (_startPosition - (Vector2)transform.position).normalized;
                }

                if (Vector2.Dot(option1, targetDir) >= Vector2.Dot(option2, targetDir))
                {
                    return option1.normalized * currentSpeed;
                }
                else
                {
                    return option2.normalized * currentSpeed;
                }
            }
        }

        return desiredVelocity;
    }

    private string _currentAnimState = "";
    private void PlayAnim(string stateName)
    {
        if (_animator == null) return;
        if (_currentAnimState == stateName) return;

        _animator.Play(stateName);
        _currentAnimState = stateName;
    }
}
