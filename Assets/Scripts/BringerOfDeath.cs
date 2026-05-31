using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Animator))]
public class BringerOfDeath : MonoBehaviour, IHittable
{
    public enum SpellSpawnMode
    {
        SpawnAtPlayerFeet,      // Призыв заклинания под ногами игрока (например, темный портал/столб магии)
        SpawnAsProjectile,     // Запуск снаряда вперед в сторону игрока (например, череп/сгусток тьмы)
        SpawnAbovePlayerHead    // Призыв тучи/портала НАД головой игрока, чтобы снаряд/молния падали сверху вниз
    }

    [Header("Основные характеристики Босса")]
    [SerializeField] private int maxHealth = 15;
    [SerializeField] private float speed = 2f;

    [Header("Сюжетный Сбор Духов (Крафт)")]
    [Tooltip("Префаб сферы целительного духа (твоя монета/сфера с измененным скриптом Coin)")]
    [SerializeField] private GameObject spiritOrbPrefab;
    [Tooltip("Минимальное количество духов при гибели босса")]
    [SerializeField] private int minSpirits = 150;
    [Tooltip("Максимальное количество духов при гибели босса")]
    [SerializeField] private int maxSpirits = 200;

    [Header("Дистанция атаки")]
    [Tooltip("Расстояние для обычной атаки мечом")]
    [SerializeField] private float meleeRange = 3.8f;
    [Tooltip("Расстояние для начала каста дальней атаки")]
    [SerializeField] private float rangedRange = 9f;
    [Tooltip("Общий кулдаун между атаками")]
    [SerializeField] private float attackCooldown = 3f;
    [Tooltip("Длительность анимации ближней атаки (время неподвижности босса)")]
    [SerializeField] private float meleeAttackAnimationDuration = 0.8f;
    [Tooltip("Длительность анимации дальней атаки (время неподвижности босса)")]
    [SerializeField] private float rangedAttackAnimationDuration = 1.2f;

    [Header("Настройки Дальней атаки (Заклинания)")]
    [Tooltip("Префаб заклинания (снаряд или столб тьмы)")]
    [SerializeField] private GameObject spellPrefab;
    [Tooltip("Режим спавна заклинания")]
    [SerializeField] private SpellSpawnMode spellSpawnMode = SpellSpawnMode.SpawnAtPlayerFeet;
    [Tooltip("Высота спавна заклинания над головой игрока (применяется только в режиме SpawnAbovePlayerHead)")]
    [SerializeField] private float spellSpawnHeight = 3.5f;
    [Tooltip("Скорость снаряда (применяется только в режиме SpawnAsProjectile)")]
    [SerializeField] private float projectileSpeed = 7f;
    [Tooltip("Время каста (задержка перед PurpleBossRangeAttack2 и спавном магии) в секундах, если не используется Animation Event")]
    [SerializeField] private float spellCastDuration = 0.8f;
    [Tooltip("Слой земли (для точного призыва на пол под ноги игрока)")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Портал (Спавн при смерти)")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;

    [Header("Хитбокс Ближнего боя")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Vector2 attackSize = new Vector2(3.8f, 3.8f);
    [SerializeField] private LayerMask playerLayer;

    [Header("Аггро и Возврат")]
    [SerializeField] private float aggroRange = 12f;
    [SerializeField] private float tetherRange = 18f;

    [Header("Настройки Спрайта")]
    [Tooltip("Спрайт по умолчанию смотрит вправо? (Для Bringer of Death обычно false, он смотрит влево)")]
    [SerializeField] private bool faceRightByDefault = false;

    private int _currentHealth;
    private bool _isDead;
    private bool _isAttacking;
    private float _attackTimer;
    private bool _hasSpawnedSpellThisAttack;
    private Vector2 _startPosition;
    
    [Header("Баланс Дальней Атаки")]
    [Tooltip("Максимальное количество дальних атак подряд перед тем, как босс побежит за игроком")]
    [SerializeField] private int maxConsecutiveRangedCasts = 2;
    [Tooltip("Время погони за игроком (в секундах) после лимита кастов, прежде чем босс сможет снова кастовать")]
    [SerializeField] private float chaseDurationBeforeRangedReset = 4f;

    private int _consecutiveRangedCastsCount;
    private float _chaseTimerCount;

    private Transform _player;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;
    private Animator _anim;
    private Collider2D[] _bossColliders;
    private Collider2D[] _playerColliders;

    private void Start()
    {
        _currentHealth = maxHealth;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();

        if (_anim == null)
        {
            Debug.LogError("[BringerOfDeath] Компонент Animator не найден на объекте! Анимации не будут проигрываться.", this);
        }
        else if (_anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[BringerOfDeath] Animator Controller не назначен в компоненте Animator! Анимации не будут работать.", this);
        }

        _bossColliders = GetComponentsInChildren<Collider2D>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _player = p.transform;
            _playerColliders = p.GetComponentsInChildren<Collider2D>();
        }
        else
        {
            Debug.LogError("[BringerOfDeath] Игрок с тегом 'Player' не найден на сцене! Босс не сможет атаковать или преследовать.", this);
        }

        _startPosition = transform.position;
    }

    private void Update()
    {
        if (_isDead) return;

        // Если игрок еще не найден или переродился (старый объект удален), ищем его динамически!
        if (_player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                _player = p.transform;
                _playerColliders = p.GetComponentsInChildren<Collider2D>();
            }
            else
            {
                return; // Игрока нет на сцене, ждем следующего кадра
            }
        }

        // Динамическое отключение коллизии во время дэша, атак или при непосредственном пересечении тел
        if (_playerColliders != null && _bossColliders != null)
        {
            PlayerMovement pm = _player.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                PlayerCombat pc = _player.GetComponent<PlayerCombat>();
                bool isAttackingOrDashing = pm.IsDashing || (pc != null && pc.IsAttacking);
                
                foreach (var bossCol in _bossColliders)
                {
                    if (bossCol == null) continue;
                    foreach (var playerCol in _playerColliders)
                    {
                        if (playerCol == null) continue;
                        
                        // Игнорируем коллизию, если игрок в рывке/атаке ИЛИ если они уже пересекаются.
                        // Это исключает резкое выталкивание игрока под землю при окончании анимации!
                        bool shouldIgnore = isAttackingOrDashing || bossCol.IsTouching(playerCol);
                        Physics2D.IgnoreCollision(bossCol, playerCol, shouldIgnore);
                    }
                }
            }
        }

        // Проверка и мгновенное нанесение контактного урона игроку вплотную (поскольку физическая коллизия игнорируется)
        if (_playerColliders != null && _bossColliders != null && !_isDead)
        {
            PlayerMovement pm = _player.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                PlayerCombat pc = _player.GetComponent<PlayerCombat>();
                bool isInvulnerable = pm.IsDashing || (pc != null && pc.IsThrustActive);
                
                if (!isInvulnerable)
                {
                    bool isTouching = false;
                    foreach (var bossCol in _bossColliders)
                    {
                        if (bossCol == null || bossCol.gameObject.name == "HurtBox") continue; // Игнорируем HurtBox для контактного урона
                        foreach (var playerCol in _playerColliders)
                        {
                            if (playerCol == null) continue;
                            if (bossCol.IsTouching(playerCol))
                            {
                                isTouching = true;
                                break;
                            }
                        }
                        if (isTouching) break;
                    }
                    
                    if (isTouching)
                    {
                        pm.TakeDamage(transform.position);
                    }
                }
            }
        }

        if (_attackTimer > 0) _attackTimer -= Time.deltaTime;

        // Если лимит дальних атак подряд превышен, босс должен погоняться за игроком определенное время
        if (_consecutiveRangedCastsCount >= maxConsecutiveRangedCasts)
        {
            _chaseTimerCount += Time.deltaTime;
            if (_chaseTimerCount >= chaseDurationBeforeRangedReset)
            {
                _consecutiveRangedCastsCount = 0;
                _chaseTimerCount = 0f;
                Debug.Log("[BringerOfDeath] Босс достаточно долго гонялся за игроком. Дальняя атака снова доступна!");
            }
        }
        else
        {
            _chaseTimerCount = 0f;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        float distanceFromStart = Vector2.Distance(transform.position, _startPosition);

        // Босс преследует игрока, если он в зоне аггро и босс не ушел слишком далеко от своей зоны обитания (tether)
        bool shouldChase = distanceToPlayer <= aggroRange && distanceFromStart <= tetherRange;

        if (shouldChase && _attackTimer <= 0f && !_isAttacking)
        {
            if (distanceToPlayer <= meleeRange)
            {
                _attackTimer = attackCooldown; // Запуск кулдауна
                StartCoroutine(MeleeAttackRoutine());
            }
            else if (distanceToPlayer <= rangedRange && _consecutiveRangedCastsCount < maxConsecutiveRangedCasts)
            {
                _attackTimer = attackCooldown; // Запуск кулдауна
                StartCoroutine(RangedAttackRoutine());
            }
        }
        else if (!_isAttacking)
        {
            if (shouldChase)
            {
                float xDiff = _player.position.x - transform.position.x;
                if (Mathf.Abs(xDiff) > 0.4f) // Мертвая зона в 0.4 юнита для предотвращения тряски (jitter)
                {
                    float dirX = Mathf.Sign(xDiff);
                    MoveBoss(dirX);
                }
                else
                {
                    // Если подошли вплотную по горизонтали, плавно останавливаемся
                    if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    if (_anim != null) _anim.SetFloat("Speed", 0f);
                }
            }
            else
            {
                // Возврат на стартовую позицию, если игрок убежал слишком далеко
                float xDiffStart = _startPosition.x - transform.position.x;
                if (Mathf.Abs(xDiffStart) > 0.4f)
                {
                    float dirX = Mathf.Sign(xDiffStart);
                    MoveBoss(dirX);
                }
                else
                {
                    if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    if (_anim != null) _anim.SetFloat("Speed", 0f);
                }
            }
        }
    }

    private void FlipBoss(float dirX)
    {
        // В зависимости от настроек спрайта поворачиваем босса лицом к цели
        Vector3 scale = transform.localScale;
        float flipMultiplier = faceRightByDefault ? (dirX > 0 ? 1f : -1f) : (dirX > 0 ? -1f : 1f);
        scale.x = Mathf.Abs(scale.x) * flipMultiplier;
        transform.localScale = scale;
    }

    private void MoveBoss(float dirX)
    {
        FlipBoss(dirX);

        if (_rb != null)
        {
            _rb.linearVelocity = new Vector2(dirX * speed, _rb.linearVelocity.y);
            if (_anim != null) _anim.SetFloat("Speed", Mathf.Abs(_rb.linearVelocity.x));
        }
    }

    /// <summary>
    /// Обычная атака ближнего боя (PurpleBossAttack)
    /// </summary>
    private IEnumerator MeleeAttackRoutine()
    {
        _isAttacking = true;
        _consecutiveRangedCastsCount = 0; // Сброс счетчика кастов при входе в ближний бой
        _chaseTimerCount = 0f;

        if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        if (_anim != null) _anim.SetFloat("Speed", 0f);

        // Поворот лицом к игроку перед ударом (без движения!) с защитой от разворотов вплотную
        float xDiff = _player.position.x - transform.position.x;
        if (Mathf.Abs(xDiff) > 0.4f)
        {
            float dirX = Mathf.Sign(xDiff);
            FlipBoss(dirX);
        }

        Debug.Log("[BringerOfDeath] Запуск ближней атаки! Отправка триггера 'Attack' в Animator.", this);
        if (_anim != null) _anim.SetTrigger("Attack");

        // Блокируем движение босса только на время самой анимации удара!
        yield return new WaitForSeconds(meleeAttackAnimationDuration);

        _isAttacking = false;
    }

    /// <summary>
    /// Дальняя атака (PurpleBossRangeAttack -> PurpleBossRangeAttack2 -> Спавн магии)
    /// </summary>
    private IEnumerator RangedAttackRoutine()
    {
        _isAttacking = true;
        _consecutiveRangedCastsCount++; // Увеличиваем счетчик дальних атак подряд
        Debug.Log($"[BringerOfDeath] Босс совершил дальнюю атаку. Подряд кастов: {_consecutiveRangedCastsCount}/{maxConsecutiveRangedCasts}", this);
        _hasSpawnedSpellThisAttack = false;

        if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        if (_anim != null) _anim.SetFloat("Speed", 0f);

        // Поворот лицом к игроку перед кастом (без движения!) с защитой от разворотов вплотную
        float xDiff = _player.position.x - transform.position.x;
        if (Mathf.Abs(xDiff) > 0.4f)
        {
            float dirX = Mathf.Sign(xDiff);
            FlipBoss(dirX);
        }

        // МГНОВЕННЫЙ спавн заклинания в момент начала каста (поднятия руки)
        ExecuteSpellSpawning();

        Debug.Log("[BringerOfDeath] Начало дальней атаки! Анимация подготовки. Триггер 'RangeAttack'.", this);
        if (_anim != null) _anim.SetTrigger("RangeAttack");

        // Блокируем движение босса только на время самой анимации каста!
        yield return new WaitForSeconds(rangedAttackAnimationDuration);

        _isAttacking = false;
    }

    /// <summary>
    /// Непосредственный спавн заклинания
    /// </summary>
    private void ExecuteSpellSpawning()
    {
        _hasSpawnedSpellThisAttack = true;

        if (spellPrefab == null)
        {
            Debug.LogWarning("[BringerOfDeath] Не могу заспавнить заклинание: 'Spell Prefab' не назначен в Инспекторе!", this);
            return;
        }

        float facingDir = GetFacingDirection();
        Vector3 spawnPos = transform.position;

        if (spellSpawnMode == SpellSpawnMode.SpawnAtPlayerFeet && _player != null)
        {
            // Призываем заклинание прямо под ноги игроку
            spawnPos = new Vector3(_player.position.x, _player.position.y, _player.position.z);
            
            // Находим поверхность пола
            RaycastHit2D hit = Physics2D.Raycast(_player.position, Vector2.down, 4f, groundLayer);
            if (hit.collider != null)
            {
                spawnPos.y = hit.point.y;
            }

            Debug.Log($"[BringerOfDeath] Спавн заклинания ПОД НОГАМИ игрока в точке {spawnPos}", this);
            Instantiate(spellPrefab, spawnPos, Quaternion.identity);
        }
        else if (spellSpawnMode == SpellSpawnMode.SpawnAsProjectile)
        {
            // Запуск снаряда вперед от босса
            spawnPos = transform.position + new Vector3(facingDir * 1.2f, 0.5f, 0f);
            Debug.Log($"[BringerOfDeath] Запуск заклинания как СНАРЯД вперед из точки {spawnPos} со скоростью {projectileSpeed}", this);
            GameObject proj = Instantiate(spellPrefab, spawnPos, Quaternion.identity);

            // Задаем скорость снаряду
            Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
            if (projRb != null)
            {
                projRb.linearVelocity = new Vector2(facingDir * projectileSpeed, 0f);
            }

            // Разворачиваем спрайт снаряда в сторону полета
            proj.transform.localScale = new Vector3(facingDir, 1f, 1f);
        }
        else if (spellSpawnMode == SpellSpawnMode.SpawnAbovePlayerHead && _player != null)
        {
            // Призываем тучку/портал ровно над головой игрока на заданной высоте
            spawnPos = new Vector3(_player.position.x, _player.position.y + spellSpawnHeight, _player.position.z);
            Debug.Log($"[BringerOfDeath] Спавн заклинания НАД ГОЛОВОЙ игрока в точке {spawnPos} (высота над игроком: {spellSpawnHeight})", this);
            Instantiate(spellPrefab, spawnPos, Quaternion.identity);
        }
    }

    /// <summary>
    /// Возвращает реальное горизонтальное направление взгляда босса (1 = Вправо, -1 = Влево)
    /// </summary>
    private float GetFacingDirection()
    {
        float scaleXSign = Mathf.Sign(transform.localScale.x);
        return faceRightByDefault ? scaleXSign : -scaleXSign;
    }

    /// <summary>
    /// Публичный метод, который можно повесить на Animation Event в анимации PurpleBossRangeAttack2,
    /// чтобы магия появлялась ровно в нужный кадр анимации.
    /// </summary>
    public void TriggerSpellSpawn()
    {
        if (_isDead) return;
        Debug.Log("[BringerOfDeath] TriggerSpellSpawn() вызван через Animation Event!", this);
        if (_hasSpawnedSpellThisAttack) return;
        ExecuteSpellSpawning();
    }

    /// <summary>
    /// Вызывается через Animation Event в анимации PurpleBossAttack (удар мечом)
    /// </summary>
    public void TriggerMeleeHitbox()
    {
        if (_isDead) return;
        if (attackPoint == null)
        {
            Debug.LogWarning("[BringerOfDeath] Метод TriggerMeleeHitbox() вызван, но 'Attack Point' (Transform) не назначен!", this);
            return;
        }

        Collider2D[] hitPlayers = Physics2D.OverlapBoxAll(attackPoint.position, attackSize, 0f, playerLayer);
        Debug.Log($"[BringerOfDeath] Проверка хитбокса меча. Найдено объектов игрока: {hitPlayers.Length}", this);
        foreach (Collider2D col in hitPlayers)
        {
            PlayerMovement pm = col.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                Debug.Log("[BringerOfDeath] Нанесен урон игроку через хитбокс ближней атаки!", this);
                pm.TakeDamage(transform.position);
            }
        }
    }

    /// <summary>
    /// Контактный урон при соприкосновении с игроком (как в Hollow Knight)
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (_isDead) return;

        // Игнорируем контактный урон через HurtBox
        if (collision.otherCollider != null && collision.otherCollider.gameObject.name == "HurtBox") return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement pm = collision.gameObject.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                PlayerCombat pc = pm.GetComponent<PlayerCombat>();
                bool isInvulnerable = pm.IsDashing || (pc != null && pc.IsThrustActive);

                if (!isInvulnerable)
                {
                    pm.TakeDamage(transform.position);
                }
            }
        }
    }

    public void HealToFull()
    {
        _currentHealth = maxHealth;
        _isDead = false;
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _spriteRenderer.color = Color.white;
    }

    /// <summary>
    /// Метод интерфейса IHittable для получения урона от игрока
    /// </summary>
    public void OnHit(bool isHeavyAttack = false)
    {
        Debug.Log($"[BringerOfDeath] OnHit() вызван через IHittable! Сильный удар: {isHeavyAttack}", this);
        TakeDamage(isHeavyAttack ? 2 : 1);
    }

    public void TakeDamage(int damage)
    {
        if (_isDead) return;

        _currentHealth -= damage;
        Debug.Log($"[BringerOfDeath] Босс получил {damage} урона. Текущее здоровье: {_currentHealth}/{maxHealth}", this);

        // Эффект микро-стоп кадра (HitStop) для сочности удара
        if (GameManager.Instance != null) GameManager.Instance.HitStop(0.05f);

        if (_currentHealth <= 0)
        {
            Die();
        }
        else
        {
            Debug.Log("[BringerOfDeath] Активация триггера 'Hit' в Animator.", this);
            if (_anim != null) _anim.SetTrigger("Hit");
        }
    }

    private void Die()
    {
        _isDead = true;
        StopAllCoroutines(); // Предотвращаем любые корутины атак после смерти

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = 0f;
        }

        // Отключаем физические коллайдеры (включая все дочерние хитбоксы)
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Запуск анимации смерти (PurpleBossDie)
        if (_anim != null)
        {
            _anim.SetBool("IsDead", true);
            _anim.SetFloat("Speed", 0f); // Сбрасываем скорость, чтобы исключить переход в анимацию ходьбы
            _anim.Play("PurpleBossDie"); // Принудительный прямой запуск стейта смерти, если граф переходов сломан
            _anim.ResetTrigger("Hit");
            _anim.ResetTrigger("Attack");
            _anim.ResetTrigger("RangeAttack");
            _anim.ResetTrigger("RangeAttack2"); // Сбрасываем все атаки
        }

        // Спавним сферы духов (королевский салют из 8-12 сфер)
        if (spiritOrbPrefab != null)
        {
            int totalSpirits = Random.Range(minSpirits, maxSpirits + 1);
            int numOrbs = Random.Range(8, 13); // от 8 до 12 сфер для босса
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
                        // Сила разброса для босса чуть больше, чтобы разлеталось шире!
                        coinScript.popForce = 8f; 
                        coinScript.ApplyPopForce();
                    }
                }
            }
            Debug.Log($"[ДУХИ] Босс Bringer of Death повержен! Освобожден салют из {totalSpirits} целительных духов в {numOrbs} сферах!");
        }
        else
        {
            if (GameManager.Instance != null)
            {
                int backupSpirits = Random.Range(minSpirits, maxSpirits + 1);
                GameManager.Instance.AddScore(backupSpirits);
                Debug.LogWarning($"[ДУХИ] spiritOrbPrefab не назначен в BringerOfDeath! Начислено {backupSpirits} духов напрямую.");
            }
        }

        // Спавн портала на следующий уровень
        if (portalPrefab != null)
        {
            Vector3 spawnPos = portalSpawnPoint != null ? portalSpawnPoint.position : transform.position;
            Instantiate(portalPrefab, spawnPos, Quaternion.identity);
        }

        Destroy(gameObject, 1.3f); // Даем 1.3 секунды на проигрывание анимации смерти
    }

    private void OnDrawGizmosSelected()
    {
        // Отрисовка радиуса ближней атаки
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(attackPoint.position, attackSize);
        }

        // Отрисовка зон аггро
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, tetherRange);
    }
}
