using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Animator))]
public class Boss : MonoBehaviour, IHittable
{
    [Header("Boss Settings")]
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private float speed = 2.5f;

    [Header("Сюжетный Сбор Духов (Крафт)")]
    [Tooltip("Префаб сферы целительного духа (твоя монета/сфера с измененным скриптом Coin)")]
    [SerializeField] private GameObject spiritOrbPrefab;
    [Tooltip("Минимальное количество духов при гибели босса")]
    [SerializeField] private int minSpirits = 150;
    [Tooltip("Максимальное количество духов при гибели босса")]
    [SerializeField] private int maxSpirits = 200;
    
    [Header("Portal (Spawn on death)")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Transform portalSpawnPoint; 

    [Header("Attack Settings (Hitbox)")]
    [SerializeField] private float attackRange = 1.5f;     
    [SerializeField] private float attackCooldown = 1.5f;  
    
    [Header("Vision and Tethering")]
    [SerializeField] private float aggroRange = 10f;       
    [SerializeField] private float tetherRange = 15f;      
    
    [Header("Sword Hitbox")]
    [SerializeField] private Transform attackPoint;        
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 1f); 
    [SerializeField] private LayerMask playerLayer;        

    private bool _isAttacking;
    private float _attackTimer;
    private int _currentHealth;
    private bool _isDead;
    private Vector2 _startPosition;

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
        
        _bossColliders = GetComponentsInChildren<Collider2D>();
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _player = p.transform;
            _playerColliders = p.GetComponentsInChildren<Collider2D>();
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
                        // Это исключает резкое выталкивание игрока под землю при окончании рывка!
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

        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        float distanceFromStart = Vector2.Distance(transform.position, _startPosition);

        bool shouldChase = distanceToPlayer <= aggroRange && distanceFromStart <= tetherRange;

        if (shouldChase && distanceToPlayer <= attackRange && _attackTimer <= 0f && !_isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }
        else if (!_isAttacking)
        {
            if (shouldChase)
            {
                float dirX = Mathf.Sign(_player.position.x - transform.position.x);
                MoveBoss(dirX);
            }
            else
            {
                if (Vector2.Distance(transform.position, _startPosition) > 0.5f)
                {
                    float dirX = Mathf.Sign(_startPosition.x - transform.position.x);
                    MoveBoss(dirX);
                }
                else
                {
                    if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
                    if (_anim != null) _anim.SetFloat("Speed", 0f);
                }
            }
        }
    }

    private void MoveBoss(float dirX)
    {
        // Вместо SpriteRenderer.flipX используем transform.localScale, 
        // чтобы дочерние объекты (например, attackPoint) тоже поворачивались!
        Vector3 scale = transform.localScale;
        
        // Определяем направление флипа в зависимости от того, Голем это или Слизень
        float flipMultiplier = 1f;
        if (IsGolem())
        {
            // Голем по умолчанию смотрит вправо, поэтому при движении вправо (dirX > 0) масштаб X должен быть положительным
            flipMultiplier = (dirX > 0 ? 1f : -1f);
        }
        else
        {
            // Слизень по умолчанию смотрит влево, поэтому при движении вправо (dirX > 0) масштаб X должен быть отрицательным
            flipMultiplier = (dirX > 0 ? -1f : 1f);
        }
        
        scale.x = Mathf.Abs(scale.x) * flipMultiplier;
        transform.localScale = scale;
        
        if (_rb != null)
        {
            _rb.linearVelocity = new Vector2(dirX * speed, _rb.linearVelocity.y);
            if (_anim != null) _anim.SetFloat("Speed", Mathf.Abs(_rb.linearVelocity.x));
        }
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        
        if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        if (_anim != null) _anim.SetFloat("Speed", 0f);
        if (_anim != null) _anim.SetTrigger("Attack");

        yield return new WaitForSeconds(attackCooldown);

        _isAttacking = false;
    }

    // Добавляем контактный урон для Босса, как в Hollow Knight, с защитой от неуязвимости игрока
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

    // Called via Animation Event
    public void TriggerAttackHitbox()
    {
        if (_isDead || attackPoint == null) return;

        Collider2D[] hitPlayers = Physics2D.OverlapBoxAll(attackPoint.position, attackSize, 0f, playerLayer);

        foreach (Collider2D playerCollider in hitPlayers)
        {
            PlayerMovement pm = playerCollider.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.TakeDamage(transform.position);
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

    // Метод интерфейса IHittable для получения урона от меча игрока
    public void OnHit(bool isHeavyAttack = false)
    {
        TakeDamage(isHeavyAttack ? 2 : 1);
    }

    public void TakeDamage(int damage)
    {
        if (_isDead) return;

        _currentHealth -= damage;

        if (GameManager.Instance != null) GameManager.Instance.HitStop(0.05f);

        if (_currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Суперброня: не прерываем замах атаки, если босс бьет в данный момент
            if (_anim != null && !_isAttacking)
            {
                _anim.SetTrigger("Hit");
            }
            StartCoroutine(FlashRed());
        }
    }

    private IEnumerator FlashRed()
    {
        _spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        _spriteRenderer.color = Color.white;
    }

    private void Die()
    {
        _isDead = true;
        
        if (_rb != null) 
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = 0f; 
        }
        
        // Отключаем абсолютно все коллайдеры босса (включая HurtBox в дочерних объектах) при смерти
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        if (_anim != null)
        {
            _anim.SetBool("IsDead", true);
            _anim.ResetTrigger("Hit");
            _anim.ResetTrigger("Attack");
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
                        coinScript.popForce = 8f; // Пошире разлетаются!
                        coinScript.ApplyPopForce();
                    }
                }
            }
            Debug.Log($"[ДУХИ] Босс повержен! Освобожден салют из {totalSpirits} целительных духов в {numOrbs} сферах!");
        }
        else
        {
            if (GameManager.Instance != null)
            {
                int backupSpirits = Random.Range(minSpirits, maxSpirits + 1);
                GameManager.Instance.AddScore(backupSpirits);
                Debug.LogWarning($"[ДУХИ] spiritOrbPrefab не назначен в Boss! Начислено {backupSpirits} духов напрямую.");
            }
        }
        
        if (portalPrefab != null)
        {
            Vector3 spawnPos = portalSpawnPoint != null ? portalSpawnPoint.position : transform.position;
            Instantiate(portalPrefab, spawnPos, Quaternion.identity);
        }

        // Замораживаем аниматор перед уничтожением объекта
        StartCoroutine(DisableAnimatorDelayed(1.2f));

        Destroy(gameObject, 1.3f);
    }

    private IEnumerator DisableAnimatorDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_anim != null)
        {
            _anim.enabled = false;
        }
    }

    public bool IsGolem()
    {
        if (gameObject.name.ToLower().Contains("golem") || gameObject.name.ToLower().Contains("golum"))
            return true;

        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null && _spriteRenderer.sprite != null)
        {
            string spriteName = _spriteRenderer.sprite.name.ToLower();
            if (spriteName.Contains("golem") || spriteName.Contains("golum"))
                return true;
        }

        if (_anim == null) _anim = GetComponent<Animator>();
        if (_anim != null && _anim.runtimeAnimatorController != null)
        {
            string controllerName = _anim.runtimeAnimatorController.name.ToLower();
            if (controllerName.Contains("golem") || controllerName.Contains("golum"))
                return true;

            foreach (var clip in _anim.runtimeAnimatorController.animationClips)
            {
                if (clip.name.ToLower().Contains("golem") || clip.name.ToLower().Contains("golum"))
                    return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
    }
#endif
}
