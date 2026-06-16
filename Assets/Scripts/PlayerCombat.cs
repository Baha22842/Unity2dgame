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
    [SerializeField] private float thrustDuration = 0.5f; 

    [Header("Combo System")]
    [SerializeField] private float comboWindow = 0.6f;

    public bool IsAttacking { get; private set; }
    public bool IsThrustActive { get; private set; }

    [SerializeField] private float thrustCooldown = 0.8f; // Небольшая задержка перед следующим выпадом
    private float _thrustCooldownTimer;

    [Header("Thrust Lunge Timing")]
    [SerializeField] private float thrustLungeDelay = 0.3f; // Время до начала рывка (замах на 0.3 сек)
    [SerializeField] private float thrustLungeDuration = 0.1f; // Длительность рывка

    private float _attackTimer;
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
        if (pm.IsDead || pm.CurrentState == PlayerMovement.PlayerState.PowerUp || pm.IsMovementFrozen) return;

        UpdateLungeAndCombo();

        if (pm.IsClimbing || pm.CurrentState == PlayerMovement.PlayerState.LedgeGrab || pm.CurrentState == PlayerMovement.PlayerState.LedgeClimb) return; 

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }

        if (_thrustCooldownTimer > 0f)
        {
            _thrustCooldownTimer -= Time.deltaTime;
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
        if (IsThrustActive && IsAttacking)
        {
            float elapsed = thrustDuration - _attackTimer;
            float facingDir = Mathf.Sign(transform.localScale.x);

            if (elapsed >= thrustLungeDelay && elapsed < (thrustLungeDelay + thrustLungeDuration))
            {
                // Активная фаза рывка
                float lungeSpeed = facingDir * thrustAttackLunge;
                
                // Проверяем наличие стены
                RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.right * facingDir, 1.0f, LayerMask.GetMask("Ground"));
                if (hit.collider == null)
                {
                    rb.linearVelocity = new Vector2(lungeSpeed, rb.linearVelocity.y);
                }
                else
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
            else
            {
                // Фаза подготовки или восстановления (тормозим)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
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
            IsThrustActive = false;
        }
    }

    private void HandleAttackInput()
    {
        if (pm.IsCrouching) return; // Запрещаем атаковать в приседе
        if (_cooldownTimer > 0f) return; // Блокировка спама

        if (Input.GetKeyDown(attackKey))
        {
            StartAttack(_comboStep == 0 ? 1 : 2);
        }
        else if (Input.GetKeyDown(heavyAttackKey) && GameManager.Instance != null && GameManager.Instance.hasHeavyAttack)
        {
            StartAttack(3);
        }
        else if (Input.GetKeyDown(thrustKey) && _thrustCooldownTimer <= 0f && GameManager.Instance != null && GameManager.Instance.hasThrust)
        {
            StartAttack(4); 
        }
    }

    private void StartAttack(int attackType)
    {
        IsAttacking = true;
        _comboResetTimer = comboWindow;
        _cooldownTimer = globalAttackCooldown; // Устанавливаем кулдаун

        if (attackType == 4)
        {
            _thrustCooldownTimer = thrustCooldown; // Запуск кулдауна выпада
        }

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
        IsThrustActive = false; // Сброс по умолчанию для всех атак
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
                _comboStep = 0;
                IsThrustActive = true;
                break;
        }
    }

    private void CheckHitboxes(bool isHeavy)
    {
        if (attackPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, attackLayers);
        bool hasHitSomething = false;
        System.Collections.Generic.HashSet<GameObject> damagedParents = new System.Collections.Generic.HashSet<GameObject>();

        foreach (Collider2D hit in hits)
        {
            // Если у объекта (самого коллайдера или его родителя) есть дочерний HurtBox,
            // мы должны наносить урон только при попадании по этому HurtBox!
            Transform targetObj = hit.transform;
            Transform hurtBox = targetObj.Find("HurtBox");
            if (hurtBox == null && targetObj.parent != null)
            {
                hurtBox = targetObj.parent.Find("HurtBox");
            }

            if (hurtBox != null && hit.transform != hurtBox)
            {
                continue; // Пропускаем удар, так как у цели есть HurtBox, но удар пришелся не по нему!
            }

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (!damagedParents.Contains(enemy.gameObject))
                {
                    enemy.TakeDamage(isHeavy ? 2 : 1);
                    damagedParents.Add(enemy.gameObject);
                    hasHitSomething = true;
                }
            }
            else
            {
                Boss boss = hit.GetComponentInParent<Boss>();
                if (boss != null)
                {
                    if (!damagedParents.Contains(boss.gameObject))
                    {
                        boss.TakeDamage(isHeavy ? 2 : 1);
                        damagedParents.Add(boss.gameObject);
                        hasHitSomething = true;
                    }
                }
                else
                {
                    IHittable hittableObj = hit.GetComponentInParent<IHittable>();
                    if (hittableObj != null)
                    {
                        MonoBehaviour mb = hittableObj as MonoBehaviour;
                        if (mb != null && !damagedParents.Contains(mb.gameObject))
                        {
                            hittableObj.OnHit(isHeavy);
                            damagedParents.Add(mb.gameObject);
                            hasHitSomething = true;
                        }
                    }
                }
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
            
            // Отдача (Recoil) только для обычных и тяжелых атак (НЕ для выпада/lunge!)
            // Это полностью устраняет физический сбой (втискивание капсулы в тайлы под воздействием отдачи)
            if (!IsThrustActive)
            {
                ApplyRecoil();
            }
        }
    }

    private void ApplyRecoil()
    {
        float facingDir = Mathf.Sign(transform.localScale.x);
 
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
        IsThrustActive = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}
