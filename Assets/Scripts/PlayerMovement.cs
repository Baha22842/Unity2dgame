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

    // Events for better architecture
    public event Action<PlayerState> OnStateChanged;

    [Header("Movement Stats")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.1f;

    [Header("Dash")]
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Climbing & Ledges")]
    [SerializeField] private float climbSpeed = 5f;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform ledgeCheck;
    [SerializeField] private float wallCheckDistance = 0.4f;
    [SerializeField] private float autoLedgeClimbDelay = 0.1f; // Время зависания перед авто-прыжком
    [SerializeField] private float ledgeClimbDuration = 0.2f; // Время, пока игрок принудительно летит вперед

    [Header("Movement Physics (Inertia)")]
    [SerializeField] private float acceleration = 13f;
    [SerializeField] private float deceleration = 16f;
    [SerializeField] private float turnSpeed = 25f;
    [SerializeField] private float airMultiplier = 0.8f;
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

    private int _remainingJumps;
    private int _facingDirection = 1;

    private bool _isGrounded;
    private bool _isNearLadder;
    private bool _isPushing;
    private bool _isRollFalling;
    private bool _canDashInAir = true;

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
            _ledgeClimbTimer = autoLedgeClimbDelay; // Засекаем время для авто-прыжка

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

            // Снижаем высоту траектории подъема если над конечной платформой низкий потолок
            float climbHeightMul = 0.85f;
            if (HasCeilingAbovePosition(_climbEndPos))
            {
                climbHeightMul = 0.35f; // Плоская траектория под потолком
                CrouchCollider();       // Гарантируем сжатый коллайдер
            }

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
        if (_invincibilityTimer > 0f || CurrentState == PlayerState.Dead) return;
        
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

        if (Input.GetKeyDown(KeyCode.Q) && CurrentState != PlayerState.Climb)
        {
            ChangeState(PlayerState.Drink);
            if (anim != null) anim.TriggerDrink();
            FreezeMovement(1.5f);
            return;
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

        if (_isGrounded)
        {
            _coyoteTimer = coyoteTime;
            _remainingJumps = (GameManager.Instance != null && GameManager.Instance.hasDoubleJump) ? 1 : 0;
            _isRollFalling = false;
            _canDashInAir = true; // Сбрасываем рывок только при касании земли
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
        if (!isAttacking && !_isPushing)
        {
            if (moveX > 0.1f) { _facingDirection = 1; transform.localScale = new Vector3(1, 1, 1); }
            else if (moveX < -0.1f) { _facingDirection = -1; transform.localScale = new Vector3(-1, 1, 1); }
        }

        switch (CurrentState)
        {
            case PlayerState.LedgeGrab:
                _ledgeClimbTimer -= Time.deltaTime;
                if (_ledgeClimbTimer <= 0f)
                {
                    ChangeState(PlayerState.LedgeClimb);
                }
                break;

            case PlayerState.LedgeClimb:
                _ledgeClimbTimer -= Time.deltaTime;
                // Принудительно толкаем вперед игнорируя трение
                rb.linearVelocity = new Vector2(_facingDirection * speed, rb.linearVelocity.y);
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
        if (GameManager.Instance != null && GameManager.Instance.hasDash && Input.GetKeyDown(KeyCode.LeftShift) && _dashCooldownTimer <= 0f)
        {
            if (_isGrounded || _canDashInAir)
            {
                if (!_isGrounded) _canDashInAir = false; // Тратим воздушный рывок
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
        bool crouchInput = _isGrounded && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        if (crouchInput && CurrentState != PlayerState.Crouch) ChangeState(PlayerState.Crouch);
        else if (!crouchInput && CurrentState == PlayerState.Crouch && !HasCeilingAbove()) ChangeState(PlayerState.Idle);

        // 4. Move (Inertia & Momentum)
        float currentSpeed = CurrentState == PlayerState.Crouch ? speed * crouchSpeedMultiplier : speed;
        
        // Во время толкания ящика немного замедляем игрока для ощущения веса ящика (Senior Touch)
        if (_isPushing)
        {
            currentSpeed *= 0.6f;
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
        if (_jumpBufferTimer > 0f && (_coyoteTimer > 0f || (!_isGrounded && _remainingJumps > 0)))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            if (!_isGrounded) _remainingJumps--;
        }

        // Variable Jump Height (Короткий прыжок)
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        // 6. Ceiling Bump Prevention — превентивное предотвращение клиппинга головы
        float ceilingY;
        if (CheckCeilingPrediction(out ceilingY))
        {
            // Сбрасываем вертикальную скорость если летим вверх
            if (rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            }

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
            _isGrounded = Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0f, groundLayer);
            if (CurrentState == PlayerState.Climb) _isGrounded = true; // Ladder hack
        }
    }

    private void CheckLedge()
    {
        if (wallCheck != null && ledgeCheck != null)
        {
            RaycastHit2D wallHit = Physics2D.Raycast(wallCheck.position, Vector2.right * _facingDirection, wallCheckDistance, groundLayer);
            
            // Фикс бага бесконечного лазания по плоской стене:
            // Обычный Raycast мог провалиться в микро-шов между тайлами (Unity Tilemap баг), 
            // из-за чего игра думала, что стена закончилась, и начинала LedgeGrab.
            // CircleCast имеет толщину и не проваливается в швы.
            RaycastHit2D ledgeHit = Physics2D.CircleCast(ledgeCheck.position, 0.05f, Vector2.right * _facingDirection, wallCheckDistance, groundLayer);
            bool isTouchingLedge = ledgeHit.collider != null;

            if (wallHit.collider != null && !isTouchingLedge && !_isGrounded && rb.linearVelocity.y < 0f && CurrentState != PlayerState.LedgeGrab && CurrentState != PlayerState.LedgeClimb)
            {
                // Архитектурно правильный (Senior) подход:
                // Вместо того чтобы хардкодить миллион тегов (Враги, Ящики, Боссы),
                // мы просто проверяем физику объекта. Мы цепляемся только за статические объекты (земля/стены)!
                Rigidbody2D hitRb = wallHit.collider.attachedRigidbody;
                bool isStaticTerrain = hitRb == null || hitRb.bodyType == RigidbodyType2D.Static;
                bool isSolid = !wallHit.collider.isTrigger;

                if (isStaticTerrain && isSolid)
                {
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
    /// восстановления коллайдера под потолком).
    /// </summary>
    private bool HasCeilingAbove()
    {
        float dummy;
        return CheckCeilingPrediction(out dummy);
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

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Ladder")) _isNearLadder = true;
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
