using UnityEngine;

public class Boss : MonoBehaviour
{
    [Header("Настройки Босса")]
    public int maxHealth = 5;
    public float speed = 2.5f;
    
    [Header("Портал (Спавн при смерти)")]
    public GameObject portalPrefab;
    public Transform portalSpawnPoint; // Точка, где появится портал

    [Header("Настройки Атаки (Hitbox)")]
    public float attackRange = 1.5f;     // Дистанция, с которой босс РЕШАЕТ ударить
    public float attackCooldown = 1.5f;  // Перерыв между ударами
    
    [Header("Зона видимости и Привязка")]
    public float aggroRange = 10f;       // Дистанция, с которой босс замечает игрока
    public float tetherRange = 15f;      // Максимальное расстояние, на которое босс может отойти от своей точки спавна
    
    [Header("Физический Хитбокс меча")]
    public Transform attackPoint;        // Пустой объект, центр удара меча
    public Vector2 attackSize = new Vector2(1f, 1f); // Размер прямоугольника удара
    public LayerMask playerLayer;        // Слой игрока, чтобы не бить воздух

    private bool isAttacking = false;
    private float attackTimer = 0f;

    private int currentHealth;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Animator anim;

    private bool isDead = false;
    private Vector2 startPosition; // Запоминаем точку, где босс появился

    private void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        
        // Пытаемся найти игрока на сцене
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        startPosition = transform.position; // Запоминаем родной дом босса
    }

    private void Update()
    {
        if (isDead || player == null) return;

        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        float distanceFromStart = Vector2.Distance(transform.position, startPosition);

        // Босс агрится только если игрок достаточно близко, И босс не ушел слишком далеко от дома
        bool shouldChase = distanceToPlayer <= aggroRange && distanceFromStart <= tetherRange;

        // Если игрок в радиусе атаки и атака готова — бьем!
        if (shouldChase && distanceToPlayer <= attackRange && attackTimer <= 0f && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }
        else if (!isAttacking)
        {
            if (shouldChase)
            {
                // Идем в сторону игрока
                float dirX = Mathf.Sign(player.position.x - transform.position.x);
                MoveBoss(dirX);
            }
            else
            {
                // Игрок убежал или босс зашел слишком далеко. Возвращаемся на старт!
                if (Vector2.Distance(transform.position, startPosition) > 0.5f)
                {
                    float dirX = Mathf.Sign(startPosition.x - transform.position.x);
                    MoveBoss(dirX);
                }
                else
                {
                    // Дошли до старта — просто стоим
                    if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    if (anim != null) anim.SetFloat("Speed", 0f);
                }
            }
        }
    }

    private void MoveBoss(float dirX)
    {
        // Поворачиваем спрайт
        if (dirX > 0) spriteRenderer.flipX = true;
        else if (dirX < 0) spriteRenderer.flipX = false;

        // Двигаем босса через физику
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
            // Анимация ходьбы
            if (anim != null) anim.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        }
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        isAttacking = true;
        
        // Останавливаемся для удара
        if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (anim != null) anim.SetFloat("Speed", 0f);

        // Запускаем анимацию атаки. УРОН ЗДЕСЬ НЕ НАНОСИМ!
        if (anim != null) anim.SetTrigger("Attack");

        // Мы просто ждем кулдаун. Сам урон нанесется через функцию TriggerAttackHitbox(), 
        // которую вызовет Animation Event прямо из анимации!
        yield return new WaitForSeconds(attackCooldown);

        isAttacking = false;
    }

    // ЭТУ ФУНКЦИЮ БУДЕТ ВЫЗЫВАТЬ ANIMATION EVENT НА КАДРЕ УДАРА
    public void TriggerAttackHitbox()
    {
        if (isDead || attackPoint == null) return;

        // Создаем физический прямоугольник ровно в месте удара меча
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

    // Рисуем рамки в редакторе для удобной настройки
    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(attackPoint.position, attackSize);
        }

        // Рисуем зону видимости (желтым)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        // Рисуем зону привязки от старта (синим). 
        // Если игра запущена, рисуем от startPosition, иначе от текущей позиции
        Gizmos.color = Color.cyan;
        Vector2 center = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(center, tetherRange);
    }

    // Эту функцию должен вызывать меч/атака Игрока
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log("Босс получил урон! Осталось ХП: " + currentHealth);

        // Включаем эффект задержки времени (Hit Stop), который у нас уже есть в GameManager!
        if (GameManager.Instance != null) GameManager.Instance.HitStop(0.05f);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Триггер анимации получения урона
            if (anim != null) anim.SetTrigger("Hit");
            
            // Эффект мигания красным
            StartCoroutine(FlashRed());
        }
    }

    private System.Collections.IEnumerator FlashRed()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        spriteRenderer.color = Color.white;
    }

    private void Die()
    {
        isDead = true;
        
        // Отключаем физику, чтобы мертвый босс не толкал игрока и не проваливался под карту
        if (rb != null) 
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f; // Отключаем гравитацию
        }
        GetComponent<Collider2D>().enabled = false;

        // Триггер анимации смерти
        if (anim != null) anim.SetBool("IsDead", true);
        
        // Спавним портал на месте трупа босса (или в специальной точке)
        if (portalPrefab != null)
        {
            Vector3 spawnPos = portalSpawnPoint != null ? portalSpawnPoint.position : transform.position;
            Instantiate(portalPrefab, spawnPos, Quaternion.identity);
            Debug.Log("Портал появился!");
        }

        // Удаляем босса через 1.3 секунды
        Destroy(gameObject, 1.3f);
    }

    // Метод OnCollisionEnter2D полностью удален!
    // Теперь босс наносит урон не касанием пуза, а только своим мечом через AttackRoutine.
}
