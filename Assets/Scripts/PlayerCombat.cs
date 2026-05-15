using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Attack Settings")]
    public KeyCode attackKey = KeyCode.J;
    public KeyCode heavyAttackKey = KeyCode.K;
    public KeyCode thrustKey = KeyCode.L; // Отдельная кнопка для колющей атаки

    public Transform attackPoint;
    public float attackRadius = 0.5f;
    [Tooltip("Не забудь добавить слой, на котором находятся Враги и Рычаги!")]
    public LayerMask attackLayers;

    [Header("Hollow Knight Style Movement")]
    public float lightAttack1Lunge = 2f; 
    public float lightAttack2Lunge = 4f; 
    public float heavyAttackLunge = 10f; // Огромный рывок вперед для SlashWide
    public float thrustAttackLunge = 8f; // Резкий выпад для колющей

    [Header("Attack Durations (Чтобы анимация успевала)")]
    public float light1Duration = 0.4f;
    public float light2Duration = 0.4f;
    public float heavyDuration = 0.3f; // Изменено на 0.30 по твоей длине анимации!
    public float thrustDuration = 0.6f; // Чуть дольше, потому что выпад срабатывает с задержкой

    public bool IsAttacking => isAttacking;

    private bool isAttacking;
    private float attackTimer;
    private int queuedAttackType = 0; 
    
    // Переменные для отложенного рывка (Wind-up)
    private float lungeDelayTimer = 0f;
    private float pendingLungeForce = 0f;

    // Комбо-система
    private int comboStep = 0;
    private float comboResetTimer = 0f;
    public float comboWindow = 0.6f; // Сколько секунд есть на 2й удар

    private Rigidbody2D rb;
    private PlayerAnimator pa;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pa = GetComponent<PlayerAnimator>();
    }

    private void Update()
    {
        // Таймер для отложенного рывка (например, колющий удар ждет 0.3 сек)
        if (lungeDelayTimer > 0f)
        {
            lungeDelayTimer -= Time.deltaTime;
            if (lungeDelayTimer <= 0f && pendingLungeForce != 0f)
            {
                rb.linearVelocity = new Vector2(pendingLungeForce, rb.linearVelocity.y);
                pendingLungeForce = 0f;
            }
        }

        // Сброс комбо, если долго не били
        if (comboResetTimer > 0f && !isAttacking) 
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f) comboStep = 0;
        }

        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null && pm.IsClimbing) return; // Нельзя бить мечом на лестнице!

        // Если мы УЖЕ бьем, но игрок жмет кнопку еще раз — ставим удар в очередь (Input Buffering)
        if (isAttacking)
        {
            if (Input.GetKeyDown(attackKey)) 
            {
                queuedAttackType = (comboStep == 0) ? 1 : 2;
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
                // Если был удар в очереди - запускаем его сразу!
                if (queuedAttackType != 0)
                {
                    int nextAttack = queuedAttackType;
                    queuedAttackType = 0;
                    StartAttack(nextAttack);
                }
            }
            return; // Во время удара новые удары с нуля не начинаем
        }

        // Если мы НЕ бьем, читаем обычные нажатия
        if (Input.GetKeyDown(attackKey))
        {
            if (comboStep == 0) StartAttack(1); // Старая атака
            else StartAttack(2); // Анимация HeavyAttack
        }
        else if (Input.GetKeyDown(heavyAttackKey) && GameManager.Instance != null && GameManager.Instance.hasHeavyAttack)
        {
            StartAttack(3); // SlashWide (в коде это Heavy)
        }
        else if (Input.GetKeyDown(thrustKey))
        {
            StartAttack(4); // Колющая атака (Thrust)
        }
    }

    private void StartAttack(int attackType)
    {
        isAttacking = true;
        comboResetTimer = comboWindow;

        // Позволяем мгновенно развернуться ПЕРЕД самым ударом, если игрок жмет стрелочку
        float moveX = Input.GetAxisRaw("Horizontal");
        if (moveX > 0.1f) transform.localScale = new Vector3(1, 1, 1);
        else if (moveX < -0.1f) transform.localScale = new Vector3(-1, 1, 1);

        // Направление теперь берем из Scale
        float facingDir = Mathf.Sign(transform.localScale.x);
        bool isHeavy = (attackType == 3);

        // Распределяем импульсы, анимации и таймеры
        if (attackType == 1)
        {
            attackTimer = light1Duration;
            if (pa != null) pa.TriggerAttack1();
            rb.linearVelocity = new Vector2(facingDir * lightAttack1Lunge, rb.linearVelocity.y);
            comboStep = 1;
        }
        else if (attackType == 2)
        {
            attackTimer = light2Duration;
            if (pa != null) pa.TriggerAttack2();
            rb.linearVelocity = new Vector2(facingDir * lightAttack2Lunge, rb.linearVelocity.y);
            comboStep = 0;
        }
        else if (attackType == 3) // Тяжелая (SlashWide)
        {
            attackTimer = heavyDuration;
            if (pa != null) pa.TriggerHeavyAttack();
            rb.linearVelocity = new Vector2(facingDir * heavyAttackLunge, rb.linearVelocity.y); // Бьет вперед мгновенно
            comboStep = 0;
        }
        else if (attackType == 4) // Колющая (Thrust)
        {
            attackTimer = thrustDuration;
            if (pa != null) pa.TriggerThrustAttack();
            // ВМЕСТО мгновенного рывка, ставим задержку 0.30 секунд!
            lungeDelayTimer = 0.30f;
            pendingLungeForce = facingDir * thrustAttackLunge; 
            comboStep = 0;
        }

        if (attackPoint == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, attackLayers);

        bool hasHitSomething = false;

        foreach (Collider2D hit in hits)
        {
            // 1. Проверяем, враг ли это (обычный)
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                int damage = isHeavy ? 2 : 1;
                enemy.TakeDamage(damage);
                hasHitSomething = true;
            }

            // Проверяем, босс ли это
            Boss boss = hit.GetComponentInParent<Boss>();
            if (boss != null)
            {
                int damage = isHeavy ? 2 : 1; 
                boss.TakeDamage(damage);
                hasHitSomething = true;
            }

            // 2. Проверяем, можно ли по этому ударить (например, Рычаг или Стена)
            IHittable hittableObj = hit.GetComponentInParent<IHittable>();
            if (hittableObj != null)
            {
                hittableObj.OnHit(isHeavy);
                hasHitSomething = true;
            }
        }

        // Если мы по чему-то попали, вызываем остановку времени (Hit Stop)
        if (hasHitSomething && GameManager.Instance != null)
        {
            GameManager.Instance.HitStop(0.04f); // Останавливаем время на 0.04 сек для крутого эффекта
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }

    public void CancelAttack()
    {
        isAttacking = false;
        attackTimer = 0f;
        lungeDelayTimer = 0f;
        pendingLungeForce = 0f;
    }
}
