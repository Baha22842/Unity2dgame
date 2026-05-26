using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerAnimator), typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private KeyCode heavyAttackKey = KeyCode.K;
    [SerializeField] private KeyCode thrustKey = KeyCode.L; 

    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.5f;
    [SerializeField] private LayerMask attackLayers;

    [Header("Movement Impulses (Lunge)")]
    // Рывки вперед для обычных атак полностью удалены по просьбе пользователя
    [SerializeField] private float thrustAttackLunge = 8f; 

    [Header("Recoil (Hollow Knight Style)")]
    [SerializeField] private float recoilForceX = 7f; // Сила отскока назад
    [SerializeField] private float recoilForceY = 10f; // Для будущего Pogo Jump (удара вниз)

    [Header("Attack Settings")]
    [SerializeField] private float globalAttackCooldown = 0.5f; // Ограничитель спама (Hollow Knight style)
    private float _cooldownTimer;

    [Header("Attack Durations")]
    [SerializeField] private float light1Duration = 0.4f;
    [SerializeField] private float light2Duration = 0.4f;
    [SerializeField] private float heavyDuration = 0.3f; 
    [SerializeField] private float thrustDuration = 0.6f; 

    [Header("Combo System")]
    [SerializeField] private float comboWindow = 0.6f;

    public bool IsAttacking { get; private set; }

    private float _attackTimer;
    private float _lungeDelayTimer = 0f;
    private float _pendingLungeForce = 0f;
    private int _comboStep = 0;
    private float _comboResetTimer = 0f;

    private Rigidbody2D rb;
    private PlayerAnimator pa;
    private PlayerMovement pm;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pa = GetComponent<PlayerAnimator>();
        pm = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (pm.IsDead || pm.CurrentState == PlayerMovement.PlayerState.PowerUp) return;

        UpdateLungeAndCombo();

        if (pm.IsClimbing || pm.CurrentState == PlayerMovement.PlayerState.LedgeGrab || pm.CurrentState == PlayerMovement.PlayerState.LedgeClimb) return; 

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }

        if (IsAttacking)
        {
            HandleAttackState();
            return; 
        }

        HandleAttackInput();
    }

    private void UpdateLungeAndCombo()
    {
        if (_lungeDelayTimer > 0f)
        {
            _lungeDelayTimer -= Time.deltaTime;
            if (_lungeDelayTimer <= 0f && _pendingLungeForce != 0f)
            {
                rb.linearVelocity = new Vector2(_pendingLungeForce, rb.linearVelocity.y);
                _pendingLungeForce = 0f;
            }
        }

        if (_comboResetTimer > 0f && !IsAttacking) 
        {
            _comboResetTimer -= Time.deltaTime;
            if (_comboResetTimer <= 0f) _comboStep = 0;
        }
    }

    private void HandleAttackState()
    {
        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            IsAttacking = false;
        }
    }

    private void HandleAttackInput()
    {
        if (_cooldownTimer > 0f) return; // Блокировка спама

        if (Input.GetKeyDown(attackKey))
        {
            StartAttack(_comboStep == 0 ? 1 : 2);
        }
        else if (Input.GetKeyDown(heavyAttackKey) && GameManager.Instance != null && GameManager.Instance.hasHeavyAttack)
        {
            StartAttack(3);
        }
        else if (Input.GetKeyDown(thrustKey))
        {
            StartAttack(4); 
        }
    }

    private void StartAttack(int attackType)
    {
        IsAttacking = true;
        _comboResetTimer = comboWindow;
        _cooldownTimer = globalAttackCooldown; // Устанавливаем кулдаун

        // Поворачиваем персонажа ПЕРЕД ударом
        float moveX = Input.GetAxisRaw("Horizontal");
        if (moveX > 0.1f) transform.localScale = new Vector3(1, 1, 1);
        else if (moveX < -0.1f) transform.localScale = new Vector3(-1, 1, 1);

        float facingDir = Mathf.Sign(transform.localScale.x);
        bool isHeavy = (attackType == 3);

        ApplyAttackEffects(attackType, facingDir);
        CheckHitboxes(isHeavy);
    }

    private void ApplyAttackEffects(int attackType, float facingDir)
    {
        switch (attackType)
        {
            case 1:
                _attackTimer = light1Duration;
                if (pa != null) pa.TriggerAttack1();
                // Рывок вперед удален
                _comboStep = 1;
                break;
            case 2:
                _attackTimer = light2Duration;
                if (pa != null) pa.TriggerAttack2();
                // Рывок вперед удален
                _comboStep = 0;
                break;
            case 3:
                _attackTimer = heavyDuration;
                if (pa != null) pa.TriggerHeavyAttack();
                // Рывок вперед удален
                _comboStep = 0;
                break;
            case 4:
                _attackTimer = thrustDuration;
                if (pa != null) pa.TriggerThrustAttack();
                _lungeDelayTimer = 0.30f;
                _pendingLungeForce = facingDir * thrustAttackLunge; 
                _comboStep = 0;
                break;
        }
    }

    private void CheckHitboxes(bool isHeavy)
    {
        if (attackPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, attackLayers);
        bool hasHitSomething = false;

        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(isHeavy ? 2 : 1);
                hasHitSomething = true;
            }

            Boss boss = hit.GetComponentInParent<Boss>();
            if (boss != null)
            {
                boss.TakeDamage(isHeavy ? 2 : 1);
                hasHitSomething = true;
            }

            IHittable hittableObj = hit.GetComponentInParent<IHittable>();
            if (hittableObj != null)
            {
                hittableObj.OnHit(isHeavy);
                hasHitSomething = true;
            }

            // Проверка на удар об стену/пол (если слой объекта называется "Ground")
            if (hit.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                hasHitSomething = true;
            }
        }

        if (hasHitSomething)
        {
            if (GameManager.Instance != null) GameManager.Instance.HitStop(0.04f);
            
            // Отдача (Recoil) как в Hollow Knight
            ApplyRecoil();
        }
    }

    private void ApplyRecoil()
    {
        float facingDir = Mathf.Sign(transform.localScale.x);
        
        // Сбрасываем pending lunge (если был рывок), чтобы отдача была приоритетнее
        _pendingLungeForce = 0f;
        _lungeDelayTimer = 0f;

        // Если игрок нажал ВНИЗ в воздухе (Pogo Jump)
        if (Input.GetAxisRaw("Vertical") < -0.1f && !pm.IsGrounded)
        {
            // Подкидываем вверх
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, recoilForceY);
        }
        else
        {
            // Откидываем назад
            rb.linearVelocity = new Vector2(-facingDir * recoilForceX, rb.linearVelocity.y);
        }
    }

    public void CancelAttack()
    {
        IsAttacking = false;
        _attackTimer = 0f;
        _lungeDelayTimer = 0f;
        _pendingLungeForce = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}
