using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Animator))]
public class Boss : MonoBehaviour
{
    [Header("Boss Settings")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private float speed = 2.5f;
    
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

    private void Start()
    {
        _currentHealth = maxHealth;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;

        _startPosition = transform.position; 
    }

    private void Update()
    {
        if (_isDead || _player == null) return;

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
        scale.x = Mathf.Abs(scale.x) * (dirX > 0 ? -1f : 1f); // У босса может быть инвертирован скейл в зависимости от арта
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

    // Добавляем контактный урон для Босса, как в Hollow Knight
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (_isDead) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement pm = collision.gameObject.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.TakeDamage(transform.position);
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
            if (_anim != null) _anim.SetTrigger("Hit");
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
        GetComponent<Collider2D>().enabled = false;

        if (_anim != null) _anim.SetBool("IsDead", true);
        
        if (portalPrefab != null)
        {
            Vector3 spawnPos = portalSpawnPoint != null ? portalSpawnPoint.position : transform.position;
            Instantiate(portalPrefab, spawnPos, Quaternion.identity);
        }

        Destroy(gameObject, 1.3f);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(attackPoint.position, attackSize);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.cyan;
        Vector2 center = Application.isPlaying ? _startPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(center, tetherRange);
    }
}
