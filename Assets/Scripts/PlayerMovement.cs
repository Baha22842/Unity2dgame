using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class PlayerMovement : MonoBehaviour
{
    public enum PlayerState { Idle, Run, Jump, Fall, Dash, Climb, Crouch, Hit, Dead, Drink, PowerUp, LedgeGrab, LedgeClimb }
    
    public PlayerState CurrentState { get; private set; }

    // Properties for backward compatibility with other scripts
    public bool IsGrounded => _isGrounded;
    public bool IsClimbing => CurrentState == PlayerState.Climb;
    public bool IsDashing => CurrentState == PlayerState.Dash;
    public bool IsCrouching => CurrentState == PlayerState.Crouch;
    public bool IsDead => CurrentState == PlayerState.Dead;
    public bool IsPushing => _isPushing;
    public bool IsRollFalling => _isRollFalling;
    public bool IsShielding => Input.GetMouseButton(1) && _isGrounded && CurrentState != PlayerState.Dead && CurrentState != PlayerState.PowerUp && CurrentState != PlayerState.Hit;

    // Events for better architecture
    public event Action<PlayerState> OnStateChanged;

    [Header("Movement Stats")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float doubleJumpForce = 5f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.2f;

    [Header("Dash")]
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.45f;

    [Header("Climbing & Ledges")]
    [SerializeField] private float climbSpeed = 5f;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform ledgeCheck;
    [SerializeField] private float wallCheckDistance = 0.4f;
    [SerializeField] private float ledgeClimbDuration = 0.2f; // Время, пока игрок принудительно летит вперед
    [Tooltip("Максимальная скорость падения, при которой персонаж еще может зацепиться за уступ. Если скорость полета вниз выше этой величины, зацеп не сработает.")]
    [SerializeField] private float maxLedgeGrabFallSpeed = 6f;
    [SerializeField] private float ledgeGrabCooldown = 0.25f; // Задержка перед повторным зацепом

    [Header("Ledge Snap Offsets")]
    [Tooltip("Смещение по горизонтали от центра персонажа до угла уступа.")]
    [SerializeField] private float ledgeSnapOffsetX = 0.3f;
    [Tooltip("Смещение по вертикали от центра персонажа до верха уступа.")]
    [SerializeField] private float ledgeSnapOffsetY = 0.8f;
    [Tooltip("Сила толчка от стены при спрыгивании с уступа (на кнопку S).")]
    [SerializeField] private float ledgeReleaseHorizontalForce = 2f;

    [Header("Movement Physics (Inertia)")]
    [SerializeField] private float acceleration = 55f;
    [SerializeField] private float deceleration = 75f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float airMultiplier = 0.85f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;

    [Header("Crouch")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;

    [Header("i-frames")]
    [SerializeField] private float invincibilityDuration = 1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCollider;
    private PlayerCombat combat;
    private PlayerAnimator anim; 

    private Vector2 standingColliderSize;
    private Vector2 standingColliderOffset;
    private float defaultGravity;

    private float _invincibilityTimer;
    private float _freezeTimer;
    private float _dashCooldownTimer;
    private float _dashTimer;
    private float _jumpBufferTimer;
    private float _coyoteTimer;
    private float _ladderCooldownTimer;
    private float _ledgeClimbTimer;
    private float _ledgeGrabCooldownTimer;

    private int _remainingJumps;
    private int _facingDirection = 1;

    private bool _isGrounded;
    private bool _isNearLadder;
    private bool _isPushing;
    private bool _isRollFalling;
    private bool _canDashInAir = true;
    private bool _isDoubleJumping = false;

    // Ceiling prediction system
    private Vector2 _climbEndPos;
    private float _lastCeilingY;
    private bool _ceilingDetected;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<CapsuleCollider2D>();
        combat = GetComponent<PlayerCombat>();
        anim = GetComponent<PlayerAnimator>();

        defaultGravity = rb.gravityScale;
        
        if (playerCollider != null)
        {
            standingColliderSize = playerCollider.size;
            standingColliderOffset = playerCollider.offset;
        }

        // Превентивное обнаружение столкновений — предотвращает прохождение сквозь тонкие платформы/потолки
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private void Start()
    {
        ChangeState(PlayerState.Idle);

        // Гарантируем, что на игроке есть скрипт привязки камеры, 
        // независимо от того, какой префаб (Player или Player 1) выбран в GameManager
        if (GetComponent<CameraTarget>() == null)
        {
            gameObject.AddComponent<CameraTarget>();
        }
    }

    private void ChangeState(PlayerState newState)
    {
        if (CurrentState == newState || CurrentState == PlayerState.Dead) return;

        // Exit state logic
        if (CurrentState == PlayerState.Crouch)
        {
            // Не восстанавливаем коллайдер, если над головой потолок — иначе застрянем!
            if (!HasCeilingAbove())
            {
                RestoreCollider();
            }
        }
        else if (CurrentState == PlayerState.Climb)
        {
            rb.gravityScale = defaultGravity;
            _ladderCooldownTimer = 0.2f;
        }
        else if (CurrentState == PlayerState.Dash)
        {
            rb.gravityScale = defaultGravity;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        else if (CurrentState == PlayerState.LedgeGrab)
        {
            rb.gravityScale = defaultGravity;
            _coyoteTimer = 0f; // Clear coyote timer when exiting ledge grab to prevent double/triple jump glitch
            if (newState == PlayerState.Fall)
            {
                _ledgeGrabCooldownTimer = ledgeGrabCooldown;
                // Даем импульс в противоположную сторону от стены, чтобы персонаж не терся о стену
                rb.linearVelocity = new Vector2(-_facingDirection * ledgeReleaseHorizontalForce, 0f);
            }
        }
        else if (CurrentState == PlayerState.LedgeClimb)
        {
            // Возвращаем гравитацию при запрыгивании
        }

        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);

        // Enter state logic
        if (CurrentState == PlayerState.Crouch)
        {
            CrouchCollider();
        }
        else if (CurrentState == PlayerState.Climb)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
        else if (CurrentState == PlayerState.Dash)
        {
            rb.gravityScale = 0f;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;
            rb.linearVelocity = new Vector2(_facingDirection * dashForce, 0f);
        }
        else if (CurrentState == PlayerState.LedgeGrab)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;

            // Рассчитываем конечную позицию подъема сразу при зацепе,
            // чтобы превентивно сжать коллайдер если над платформой низкий потолок
            _climbEndPos = (Vector2)transform.position + new Vector2(
                _facingDirection * wallCheckDistance * 2f,
                standingColliderSize.y * 0.6f
            );

            // Проверяем, есть ли потолок над конечной точкой подъема
            if (HasCeilingAbovePosition(_climbEndPos))
            {
                CrouchCollider(); // Превентивно сжимаем коллайдер ещё на висении
            }
        }
        else if (CurrentState == PlayerState.LedgeClimb)
        {
            rb.gravityScale = defaultGravity;
            _jumpBufferTimer = 0f; // Clear jump buffer to prevent immediate double jump from climb input

            // Снижаем высоту траектории подъема если над конечной платформой низкий потолок
            float climbHeightMul = 0.85f;
            if (HasCeilingAbovePosition(_climbEndPos))
            {
                climbHeightMul = 0.35f; // Плоская траектория под потолком
                CrouchCollider();       // Гарантируем сжатый коллайдер
            }

            // Сохраняем исходную горизонтальную скорость (игрок должен сам зажимать кнопку направления, чтобы залететь на уступ)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * climbHeightMul);
            _ledgeClimbTimer = ledgeClimbDuration; // Засекаем время полета вперед
        }
        else if (CurrentState == PlayerState.Hit)
        {
            if (anim != null) anim.TriggerHit();
            FreezeMovement(0.25f); // Быстрое оглушение как в Hollow Knight
        }
    }

    public void FreezeMovement(float duration)
    {
        if (CurrentState == PlayerState.Dead) return;
        _freezeTimer = duration;
    }

    public void CollectPowerUp()
    {
        if (CurrentState == PlayerState.Dead || CurrentState == PlayerState.PowerUp) return;
        
        ChangeState(PlayerState.PowerUp);
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        
        if (combat != null)
        {
            combat.CancelAttack();
        }
        
        if (anim != null) anim.TriggerPowerUp();
        FreezeMovement(0.5f); // Время проигрывания анимации получения предмета (по просьбе - 0.5 сек)
    }

    public void Die()
    {
        ChangeState(PlayerState.Dead);
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        
        if (combat != null)
        {
            combat.CancelAttack();
            combat.enabled = false;
        }

        if (anim != null) anim.TriggerDie();
    }

    public void TakeDamage(Vector2 damageSourcePosition)
    {
        if (GameManager.isGodMode) return;
        if (_invincibilityTimer > 0f || CurrentState == PlayerState.Dead) return;
        if (combat != null && combat.IsThrustActive) return; // Не получаем урон во время выпада (lunge/thrust attack)

        // Обработка успешного блока щитом (ПКМ)
        if (IsShielding)
        {
            float dirToSource = Mathf.Sign(damageSourcePosition.x - transform.position.x);
            if (dirToSource == _facingDirection)
            {
                Debug.Log("[PlayerMovement] Удар успешно заблокирован щитом!");
                rb.linearVelocity = new Vector2(-_facingDirection * 3f, rb.linearVelocity.y); // Легкий отскок назад от силы удара
                if (anim != null) anim.SetTrigger("Block"); // Анимационный триггер блокирования (искры/звук)
                _invincibilityTimer = 0.2f; // Кратковременное бессмертие после успешного блока
                return; // Урон НЕ наносится!
            }
        }
        
        _invincibilityTimer = invincibilityDuration;

        float knockbackDir = Mathf.Sign(transform.position.x - damageSourcePosition.x);
        if (knockbackDir == 0) knockbackDir = 1f;

        // Отбрасывание в стиле Hollow Knight (сильно по горизонтали, но не слишком далеко, чуть-чуть вверх)
        rb.linearVelocity = new Vector2(knockbackDir * 4f, 1f);
        ChangeState(PlayerState.Hit);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TakeDamage(1);
        }
    }

    private void Update()
    {
        if (CurrentState == PlayerState.Dead || CurrentState == PlayerState.PowerUp) 
        {
            // Во время смерти или получения орба/скилла Игрок не должен двигаться
            // и таймеры не обновляются
            if (_freezeTimer > 0f) _freezeTimer -= Time.deltaTime;
            
            if (CurrentState == PlayerState.PowerUp && _freezeTimer <= 0f)
            {
                rb.gravityScale = defaultGravity;
                ChangeState(_isGrounded ? PlayerState.Idle : PlayerState.Fall);
            }
            return;
        }

        CheckGrounded();
        CheckLedge();
        UpdateTimers();

        if (_freezeTimer > 0f)
        {
            // В состоянии Hit мы сохраняем инерцию откидывания, 
            // в остальных станах (Смерть, PowerUp, Питье) - жестко стоим на месте
            if (CurrentState != PlayerState.Hit)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            return;
        }

        // Return from Hit state
        if (CurrentState == PlayerState.Hit && _freezeTimer <= 0f)
        {
            ChangeState(_isGrounded ? PlayerState.Idle : PlayerState.Fall);
        }

        if (Input.GetKeyDown(KeyCode.Q) && CurrentState != PlayerState.Climb && CurrentState != PlayerState.Dead && CurrentState != PlayerState.PowerUp && CurrentState != PlayerState.Drink && CurrentState != PlayerState.Hit)
        {
            if (GameManager.Instance != null && GameManager.Instance.potionsCount > 0 && GameManager.Instance.CurrentHealth < GameManager.Instance.maxHealth)
            {
                ChangeState(PlayerState.Drink);
                if (anim != null) anim.TriggerDrink();
                FreezeMovement(1.5f);
                GameManager.Instance.UsePotion();
                return;
            }
        }
        
        if (CurrentState == PlayerState.Drink && _freezeTimer <= 0f)
        {
             ChangeState(_isGrounded ? PlayerState.Idle : PlayerState.Fall);
        }

        HandleInputForState();
    }

    private void UpdateTimers()
    {
        if (_invincibilityTimer > 0f) _invincibilityTimer -= Time.deltaTime;
        if (_freezeTimer > 0f) _freezeTimer -= Time.deltaTime;
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
        if (_ladderCooldownTimer > 0f) _ladderCooldownTimer -= Time.deltaTime;
        if (_ledgeGrabCooldownTimer > 0f) _ledgeGrabCooldownTimer -= Time.deltaTime;

        bool resetJumps = _isGrounded || IsPhysicallyOnPlatform() || CurrentState == PlayerState.LedgeGrab;
        if (resetJumps)
        {
            _coyoteTimer = coyoteTime;
            _remainingJumps = (GameManager.Instance != null && GameManager.Instance.hasDoubleJump) ? 1 : 0;
            _isRollFalling = false;
            _canDashInAir = true; // Сбрасываем рывок только при касании земли или уступа
            _isDoubleJumping = false;
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump")) _jumpBufferTimer = jumpBufferTime;
        else _jumpBufferTimer -= Time.deltaTime;
    }

    private void HandleInputForState()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // Flip character logic
        bool isAttacking = combat != null && combat.IsAttacking;
        bool canFlip = !isAttacking && !_isPushing && CurrentState != PlayerState.LedgeGrab;
        if (canFlip)
        {
            if (moveX > 0.1f) { _facingDirection = 1; transform.localScale = new Vector3(1, 1, 1); }
            else if (moveX < -0.1f) { _facingDirection = -1; transform.localScale = new Vector3(-1, 1, 1); }
        }

        switch (CurrentState)
        {
            case PlayerState.LedgeGrab:
                // Игрок висит на уступе бесконечно долго.
                // При нажатии кнопки Прыжка (Jump / Space) он взбирается наверх.
                if (Input.GetButtonDown("Jump"))
                {
                    ChangeState(PlayerState.LedgeClimb);
                }
                // При нажатии кнопок Вниз/S/Control отпускает уступ и падает.
                else if (moveY < -0.1f || Input.GetKeyDown(KeyCode.S) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    ChangeState(PlayerState.Fall);
                }
                break;

            case PlayerState.LedgeClimb:
                _ledgeClimbTimer -= Time.deltaTime;
                // Позволяем игроку полностью контролировать направление движения во время прыжка
                HandleMovementAndJumps(moveX, moveY, isAttacking);
                if (_ledgeClimbTimer <= 0f)
                {
                    ChangeState(_isGrounded ? PlayerState.Idle : PlayerState.Fall);
                }
                break;

            case PlayerState.Dash:
                _dashTimer -= Time.deltaTime;
                rb.linearVelocity = new Vector2(_facingDirection * dashForce, 0f);
                if (_dashTimer <= 0f)
                {
                    ChangeState(_isGrounded ? PlayerState.Idle : PlayerState.Fall);
                }
                break;

            case PlayerState.Climb:
                rb.linearVelocity = new Vector2(moveX * speed, moveY * climbSpeed);
                if (Input.GetButtonDown("Jump"))
                {
                    ChangeState(PlayerState.Fall);
                }
                else if (!_isNearLadder)
                {
                     ChangeState(PlayerState.Fall);
                }
                break;

            case PlayerState.Idle:
            case PlayerState.Run:
            case PlayerState.Jump:
            case PlayerState.Fall:
            case PlayerState.Crouch:
                HandleMovementAndJumps(moveX, moveY, isAttacking);
                break;
        }
    }

    private void HandleMovementAndJumps(float moveX, float moveY, bool isAttacking)
    {
        // 1. Dash
        if (GameManager.Instance != null && (GameManager.Instance.hasDash || GameManager.isGodMode) && Input.GetKeyDown(KeyCode.LeftShift) && (_dashCooldownTimer <= 0f || GameManager.isGodMode))
        {
            if (_isGrounded || _canDashInAir || GameManager.isGodMode)
            {
                if (!_isGrounded && !GameManager.isGodMode) _canDashInAir = false; // Тратим воздушный рывок
                ChangeState(PlayerState.Dash);
                return;
            }
        }

        // 2. Climb
        if (_isNearLadder && _ladderCooldownTimer <= 0f && (Mathf.Abs(moveY) > 0.1f || rb.linearVelocity.y < -0.1f))
        {
            ChangeState(PlayerState.Climb);
            return;
        }

        // 3. Crouch
        bool isHoldingCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool crouchInput = _isGrounded && isHoldingCrouch;
        
        if (crouchInput && CurrentState != PlayerState.Crouch) 
        {
            ChangeState(PlayerState.Crouch);
        }
        else if (!isHoldingCrouch && CurrentState == PlayerState.Crouch && !HasCeilingAbove()) 
        {
            ChangeState(PlayerState.Idle);
        }

        // 4. Move (Inertia & Momentum)
        float currentSpeed = CurrentState == PlayerState.Crouch ? speed * crouchSpeedMultiplier : speed;
        
        // Во время толкания ящика немного замедляем игрока для ощущения веса ящика (Senior Touch)
        if (_isPushing)
        {
            currentSpeed *= 0.6f;
        }

        // При зажатом щите игрок ходит медленно и защищается
        if (IsShielding)
        {
            currentSpeed *= 0.35f;
        }

        float targetSpeed = moveX * currentSpeed;

        // Фикс глитча у стены: если впереди стена, не пытаемся идти в неё (предотвращает подпрыгивания капсулы)
        // Но игнорируем интерактивные ящики (Box), чтобы игрок мог их толкать!
        bool isWallAhead = false;
        if (wallCheck != null)
        {
            RaycastHit2D wallHit = Physics2D.Raycast(wallCheck.position, Vector2.right * Mathf.Sign(moveX), wallCheckDistance, groundLayer);
            if (wallHit.collider != null && !wallHit.collider.CompareTag("Box"))
            {
                isWallAhead = true;
            }
        }

        if (!isAttacking)
        {
            if (isWallAhead && Mathf.Abs(moveX) > 0.1f && Mathf.Sign(moveX) == _facingDirection)
            {
                targetSpeed = 0f; // Убираем физическое давление на стену, но анимация Run останется
            }

            float accelRate;
            if (_isGrounded)
            {
                if (Mathf.Abs(targetSpeed) > 0.01f)
                    accelRate = (Mathf.Sign(targetSpeed) == Mathf.Sign(rb.linearVelocity.x) || rb.linearVelocity.x == 0) ? acceleration : turnSpeed;
                else
                    accelRate = deceleration;
            }
            else
            {
                if (Mathf.Abs(targetSpeed) > 0.01f)
                    accelRate = (Mathf.Sign(targetSpeed) == Mathf.Sign(rb.linearVelocity.x) || rb.linearVelocity.x == 0) ? acceleration * airMultiplier : turnSpeed * airMultiplier;
                else
                    accelRate = deceleration * airMultiplier;
            }

            float newVelX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.deltaTime);
            rb.linearVelocity = new Vector2(newVelX, rb.linearVelocity.y);
        }

        // 5. Jump
        if (_jumpBufferTimer > 0f && (_coyoteTimer > 0f || (!_isGrounded && _remainingJumps > 0) || GameManager.isGodMode))
        {
            // Если у нас активно Coyote Time (персонаж только что сошел с края платформы),
            // этот прыжок считается полноценным первым прыжком с земли, а не двойным!
            bool wasCoyoteJump = (_coyoteTimer > 0f);
            _coyoteTimer = 0f;

            if (!wasCoyoteJump && !_isGrounded)
            {
                // Это второй (двойной) прыжок
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
                _isDoubleJumping = true; // Активируем флаг фиксированного прыжка
                if (!GameManager.isGodMode) _remainingJumps--;
            }
            else
            {
                // Это первый прыжок с земли
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                _isDoubleJumping = false;
            }

            _jumpBufferTimer = 0f;

            if (anim != null)
            {
                anim.TriggerJump();
            }
        }

        // Variable Jump Height (Короткий прыжок) — блокируется для LedgeClimb и двойного прыжка (делая его фиксированным)
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f && CurrentState != PlayerState.LedgeClimb && (!_isDoubleJumping || GameManager.isGodMode))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        // 6. Ceiling Bump Prevention — превентивное предотвращение клиппинга головы (только при полете вверх!)
        if (rb.linearVelocity.y > 0.01f)
        {
            float ceilingY;
            if (CheckCeilingPrediction(out ceilingY))
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

                // Математически ограничиваем Y-позицию — 0% проникновения в потолок!
                float maxAllowedY = GetMaxAllowedY(ceilingY);
                if (rb.position.y > maxAllowedY)
                {
                    rb.position = new Vector2(rb.position.x, maxAllowedY);
                }

                // Мгновенный приседание (даже в воздухе) — персонаж пригибается от удара об потолок
                if (CurrentState != PlayerState.Crouch)
                {
                    ChangeState(PlayerState.Crouch);
                }
            }
        }

        // State Update
        if (CurrentState != PlayerState.Crouch && CurrentState != PlayerState.Hit && CurrentState != PlayerState.LedgeGrab && CurrentState != PlayerState.LedgeClimb)
        {
            if (!_isGrounded) ChangeState(rb.linearVelocity.y > 0 ? PlayerState.Jump : PlayerState.Fall);
            else ChangeState(Mathf.Abs(moveX) > 0.1f ? PlayerState.Run : PlayerState.Idle);
        }
    }

    private void CheckGrounded()
    {
        if (groundCheckPoint != null)
        {
            // Сужаем ширину зоны проверки приземления до 60% от исходной,
            // чтобы она физически не могла пересекаться с вертикальными стенами при прижимании вплотную.
            Vector2 narrowedCheckSize = new Vector2(groundCheckSize.x * 0.6f, groundCheckSize.y);

            _isGrounded = Physics2D.OverlapBox(groundCheckPoint.position, narrowedCheckSize, 0f, groundLayer);
            
            if (CurrentState == PlayerState.Climb) _isGrounded = true; // Ladder hack
        }
    }

    private bool IsPhysicallyOnPlatform()
    {
        if (groundCheckPoint == null) return false;

        // Используем более широкую зону проверки (95% от исходной), чтобы определить,
        // находится ли физически край коллайдера персонажа на платформе.
        Vector2 widerCheckSize = new Vector2(groundCheckSize.x * 0.95f, groundCheckSize.y);
        bool isSupported = Physics2D.OverlapBox(groundCheckPoint.position, widerCheckSize, 0f, groundLayer);

        // Мы считаем персонажа на платформе, если он поддерживается землёй и не движется вертикально (стоит/бежит на краю)
        return isSupported && Mathf.Abs(rb.linearVelocity.y) < 0.05f;
    }

    private void CheckLedge()
    {
        if (_ledgeGrabCooldownTimer > 0f) return;

        if (wallCheck != null && ledgeCheck != null)
        {
            RaycastHit2D wallHit = Physics2D.Raycast(wallCheck.position, Vector2.right * _facingDirection, wallCheckDistance, groundLayer);
            
            // Фикс бага бесконечного лазания по плоской стене:
            // Обычный Raycast мог провалиться в микро-шов между тайлами (Unity Tilemap баг), 
            // из-за чего игра думала, что стена закончилась, и начинала LedgeGrab.
            // CircleCast имеет толщину и не проваливается в швы.
            RaycastHit2D ledgeHit = Physics2D.CircleCast(ledgeCheck.position, 0.05f, Vector2.right * _facingDirection, wallCheckDistance, groundLayer);
            bool isTouchingLedge = ledgeHit.collider != null;

            if (wallHit.collider != null && !isTouchingLedge && !_isGrounded && rb.linearVelocity.y < 0f && rb.linearVelocity.y >= -maxLedgeGrabFallSpeed && CurrentState != PlayerState.LedgeGrab && CurrentState != PlayerState.LedgeClimb)
            {
                // Архитектурно правильный (Senior) подход:
                // Вместо того чтобы хардкодить миллион тегов (Враги, Ящики, Боссы),
                // мы просто проверяем физику объекта. Мы цепляемся только за статические объекты (земля/стены)!
                Rigidbody2D hitRb = wallHit.collider.attachedRigidbody;
                bool isStaticTerrain = hitRb == null || hitRb.bodyType == RigidbodyType2D.Static;
                bool isSolid = !wallHit.collider.isTrigger;

                if (isStaticTerrain && isSolid)
                {
                    // Вычисляем точное положение угла платформы для pixel-perfect прилипания рук
                    Vector2 raycastOrigin = new Vector2(wallHit.point.x + _facingDirection * 0.05f, ledgeCheck.position.y);
                    RaycastHit2D ledgeYHit = Physics2D.Raycast(raycastOrigin, Vector2.down, 1.5f, groundLayer);
                    
                    float snapX = wallHit.point.x - _facingDirection * ledgeSnapOffsetX;
                    float snapY = (ledgeYHit.collider != null) ? ledgeYHit.point.y - ledgeSnapOffsetY : ledgeCheck.position.y - ledgeSnapOffsetY;
                    
                    transform.position = new Vector3(snapX, snapY, transform.position.z);
                    rb.linearVelocity = Vector2.zero;

                    ChangeState(PlayerState.LedgeGrab);
                }
            }
        }
    }

    // ======================== CEILING PREDICTION SYSTEM ========================

    /// <summary>
    /// Динамическое предсказание потолка с помощью BoxCast.
    /// Дистанция луча рассчитывается на основе Time.fixedDeltaTime * 2 для раннего обнаружения
    /// препятствий на высоких скоростях. Возвращает мировую Y-координату нижней грани потолка.
    /// </summary>
    private bool CheckCeilingPrediction(out float ceilingY)
    {
        ceilingY = float.MaxValue;
        if (playerCollider == null) return false;

        // Размер бокса = текущий размер коллайдера (учитывает присед)
        Vector2 boxSize = playerCollider.size * (Vector2)transform.localScale;
        boxSize.x *= 0.9f; // Чуть уже, чтобы не цеплять стены по бокам

        // Начальная точка — макушка текущего коллайдера
        Vector2 boxCenter = (Vector2)transform.position + playerCollider.offset * (Vector2)transform.localScale;
        float halfHeight = boxSize.y / 2f;
        Vector2 castOrigin = new Vector2(boxCenter.x, boxCenter.y + halfHeight);

        // Динамическая дистанция: чем быстрее летим, тем дальше смотрим (минимум 0.1 юнита)
        float predictedDistance = Mathf.Max(
            Mathf.Abs(rb.linearVelocity.y) * Time.fixedDeltaTime * 2.0f,
            0.1f
        );

        // BoxCast вверх от макушки
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            castOrigin,
            new Vector2(boxSize.x, 0.05f), // Тонкий горизонтальный бокс
            0f,
            Vector2.up,
            predictedDistance,
            groundLayer
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.gameObject == gameObject) continue; // Игнорируем себя

            // Нижняя грань потолка
            float hitCeilingY = hit.point.y;
            if (hitCeilingY < ceilingY)
            {
                ceilingY = hitCeilingY;
            }
        }

        _ceilingDetected = ceilingY < float.MaxValue;
        if (_ceilingDetected) _lastCeilingY = ceilingY;

        return _ceilingDetected;
    }

    /// <summary>
    /// Рассчитывает максимально допустимую Y-координату rb.position,
    /// чтобы макушка коллайдера не проникала в потолок.
    /// </summary>
    private float GetMaxAllowedY(float ceilingY)
    {
        if (playerCollider == null) return ceilingY;

        Vector2 currentSize = playerCollider.size * (Vector2)transform.localScale;
        Vector2 currentOffset = playerCollider.offset * (Vector2)transform.localScale;
        float topOfCollider = currentOffset.y + currentSize.y / 2f;

        // maxY = ceilingY - расстояние_от_центра_до_макушки - маленький зазор
        return ceilingY - topOfCollider - 0.01f;
    }

    /// <summary>
    /// Быстрая проверка потолка прямо над головой (используется для предотвращения
    /// восстановления коллайдера под потолком при попытке встать из приседа).
    /// </summary>
    private bool HasCeilingAbove()
    {
        if (playerCollider == null) return false;

        // Вычисляем мировую Y-координату макушки текущего (присевшего) коллайдера
        float currentTopY = transform.position.y + playerCollider.offset.y * transform.localScale.y + (playerCollider.size.y * transform.localScale.y) / 2f;

        // Вычисляем мировую Y-координату макушки стоячего коллайдера, если бы игрок выпрямился
        float standingTopY = transform.position.y + standingColliderOffset.y * transform.localScale.y + (standingColliderSize.y * transform.localScale.y) / 2f;

        // Разница в высоте, на которую вырастет коллайдер при выпрямлении
        float heightDifference = standingTopY - currentTopY;

        if (heightDifference <= 0.01f) return false; // Защита: разница ничтожно мала или мы уже стоим

        // Сужаем ширину проверки на 10%, чтобы не цеплять боковые стены при разворотах
        float boxWidth = standingColliderSize.x * Mathf.Abs(transform.localScale.x) * 0.9f;
        Vector2 boxSize = new Vector2(boxWidth, 0.05f);
        
        // Начинаем проверку чуть выше текущей макушки, чтобы не задеть собственный коллайдер
        Vector2 castOrigin = new Vector2(transform.position.x, currentTopY + 0.02f);

        // Стреляем тонкой коробкой вверх на высоту, которую займет голова стоячего персонажа (+ небольшой запас)
        RaycastHit2D hit = Physics2D.BoxCast(
            castOrigin,
            boxSize,
            0f,
            Vector2.up,
            heightDifference - 0.02f + 0.1f, // С запасом в 0.1 юнита для надежности
            groundLayer
        );

        return hit.collider != null;
    }

    /// <summary>
    /// Проверяет наличие потолка над указанной позицией (для LedgeGrab/LedgeClimb).
    /// Использует стоячий размер коллайдера для предсказания.
    /// </summary>
    private bool HasCeilingAbovePosition(Vector2 position)
    {
        Vector2 boxSize = standingColliderSize * (Vector2)transform.localScale;
        boxSize.x *= 0.9f;

        Vector2 castOrigin = new Vector2(
            position.x,
            position.y + standingColliderOffset.y * transform.localScale.y + boxSize.y / 2f
        );

        RaycastHit2D hit = Physics2D.BoxCast(
            castOrigin,
            new Vector2(boxSize.x, 0.05f),
            0f,
            Vector2.up,
            0.3f, // Короткая дистанция — просто проверяем, есть ли потолок прямо сверху
            groundLayer
        );

        return hit.collider != null && !hit.collider.isTrigger;
    }

    // ======================== COLLIDER MANAGEMENT ========================

    private void CrouchCollider()
    {
        if (playerCollider != null)
        {
            playerCollider.size = new Vector2(standingColliderSize.x, standingColliderSize.y / 2f);
            playerCollider.offset = new Vector2(standingColliderOffset.x, standingColliderOffset.y - (standingColliderSize.y / 4f));
        }
    }

    private void RestoreCollider()
    {
        if (playerCollider != null)
        {
            playerCollider.size = standingColliderSize;
            playerCollider.offset = standingColliderOffset;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckHazardCollision(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Ladder")) _isNearLadder = true;
        CheckHazardCollision(collider);
    }

    private void CheckHazardCollision(Collider2D otherCollider)
    {
        if (otherCollider == null) return;
        if (GameManager.isGodMode) return;

        string name = otherCollider.gameObject.name.ToLower();
        bool isHazard = name.Contains("spike")
                     || name.Contains("trap")
                     || name.Contains("hazard")
                     || otherCollider.GetComponent<SpikeTrap>() != null
                     || otherCollider.GetComponent<Trap>() != null;

        if (isHazard)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Ladder"))
        {
            _isNearLadder = false;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Box"))
        {
            bool isTouchingSide = false;
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (Mathf.Abs(contact.normal.x) > 0.5f) { isTouchingSide = true; break; }
            }

            if (_isGrounded && CurrentState != PlayerState.Crouch && isTouchingSide && Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f)
            {
                _isPushing = true;
            }
            else
            {
                _isPushing = false;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Box")) _isPushing = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
        }
        if (wallCheck != null && ledgeCheck != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * _facingDirection * wallCheckDistance);
            Gizmos.DrawLine(ledgeCheck.position, ledgeCheck.position + Vector3.right * _facingDirection * wallCheckDistance);
        }

        // Визуализация ceiling prediction
        if (_ceilingDetected)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                new Vector3(transform.position.x - 0.5f, _lastCeilingY, 0f),
                new Vector3(transform.position.x + 0.5f, _lastCeilingY, 0f)
            );
        }
    }
}
